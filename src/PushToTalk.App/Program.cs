using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Olbrasoft.PushToTalk.App;
using Olbrasoft.PushToTalk.App.Hubs;
using Olbrasoft.PushToTalk.App.Services;
using Olbrasoft.PushToTalk.App.Tray;
using Olbrasoft.PushToTalk.Core.Configuration;
using Olbrasoft.PushToTalk.Core.Extensions;
using Olbrasoft.PushToTalk.TextInput;

// Single instance check
using var instanceLock = SingleInstanceLock.TryAcquire();
if (!instanceLock.IsAcquired)
{
    Console.WriteLine("ERROR: PushToTalk is already running!");
    Console.WriteLine("Only one instance is allowed.");
    Environment.Exit(1);
    return;
}

// Load configuration
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .Build();

var options = new DictationOptions();
config.GetSection(DictationOptions.SectionName).Bind(options);

// Get port from config or use default
var webPort = config.GetValue<int>("WebServer:Port", ServiceEndpoints.DefaultWebServerPort);

// Setup logging
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

var logger = loggerFactory.CreateLogger<Program>();

// Print banner
var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown";
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║              PushToTalk Desktop Application                ║");
Console.WriteLine($"║                      Version: {version,-25}       ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();
logger.LogInformation("PushToTalk version {Version} starting", version);

// Find icons path
var iconsPath = options.IconsPath ?? IconsPathResolver.FindIconsPath(logger);

// Build services using standard ServiceCollection
var services = new ServiceCollection();
services.AddSingleton(loggerFactory);
services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
services.AddDictationServices(options, config);
services.AddTrayServices(options, iconsPath);

using var serviceProvider = services.BuildServiceProvider();

// Get services
var dictationService = serviceProvider.GetRequiredService<DictationService>();
var trayService = serviceProvider.GetRequiredService<PushToTalkTrayService>();
var sttServiceManager = serviceProvider.GetRequiredService<SpeechToTextServiceManager>();

var textTyperFactory = serviceProvider.GetRequiredService<Olbrasoft.PushToTalk.TextInput.ITextTyperFactory>();
logger.LogInformation("Text typer: {DisplayServer}", textTyperFactory.GetDisplayServerName());

// Set service availability check for DictationService
dictationService.SetServiceAvailabilityCheck(async () => await sttServiceManager.IsRunningAsync());

var cts = new CancellationTokenSource();

// Build web application for SignalR and remote control
var webBuilder = WebApplication.CreateBuilder();
// Configure Kestrel endpoints (HTTP + HTTPS for PWA)
webBuilder.WebHost.ConfigureKestrel((context, serverOptions) =>
{
    serverOptions.ListenAnyIP(5050); // HTTP

    // HTTPS only if certificate exists (for PWA installation)
    var certPath = Path.Combine(AppContext.BaseDirectory, "certs", "192.168.0.182+3.p12");
    if (File.Exists(certPath))
    {
        serverOptions.ListenAnyIP(5051, listenOptions =>
        {
            listenOptions.UseHttps(certPath, "changeit");
        });
    }
});
webBuilder.Logging.ClearProviders();
webBuilder.Logging.AddConsole();
webBuilder.Logging.SetMinimumLevel(LogLevel.Warning); // Reduce web server noise

// Add SignalR
webBuilder.Services.AddSignalR();

// Add CORS for remote access
webBuilder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register DictationService as singleton (same instance)
webBuilder.Services.AddSingleton(dictationService);

// Register ITextTyper for the SignalR hub
var textTyper = serviceProvider.GetRequiredService<ITextTyper>();
webBuilder.Services.AddSingleton(textTyper);

var webApp = webBuilder.Build();

// Configure middleware
webApp.UseCors();

// Serve static files from wwwroot
var wwwrootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
if (Directory.Exists(wwwrootPath))
{
    var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
    provider.Mappings[".pem"] = "application/x-pem-file"; // Add MIME type for .pem files

    webApp.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(wwwrootPath),
        ContentTypeProvider = provider
    });
}

// Map SignalR hub
webApp.MapHub<DictationHub>("/hubs/dictation");

// Map status endpoint
webApp.MapGet("/api/status", () => new
{
    IsRecording = dictationService.State == DictationState.Recording,
    IsTranscribing = dictationService.State == DictationState.Transcribing,
    State = dictationService.State.ToString()
});

// Map control endpoints
webApp.MapPost("/api/recording/start", async () =>
{
    if (dictationService.State == DictationState.Idle)
    {
        await dictationService.StartDictationAsync();
        return Results.Ok(new { Success = true });
    }
    return Results.BadRequest(new { Success = false, Error = "Already recording or transcribing" });
});

webApp.MapPost("/api/recording/stop", async () =>
{
    if (dictationService.State == DictationState.Recording)
    {
        await dictationService.StopDictationAsync();
        return Results.Ok(new { Success = true });
    }
    return Results.BadRequest(new { Success = false, Error = "Not recording" });
});

webApp.MapPost("/api/recording/toggle", async () =>
{
    if (dictationService.State == DictationState.Idle)
    {
        await dictationService.StartDictationAsync();
        return Results.Ok(new { Success = true, IsRecording = true });
    }
    else if (dictationService.State == DictationState.Recording)
    {
        await dictationService.StopDictationAsync();
        return Results.Ok(new { Success = true, IsRecording = false });
    }
    return Results.BadRequest(new { Success = false, Error = "Transcription in progress" });
});

webApp.MapPost("/api/recording/cancel", () =>
{
    if (dictationService.State == DictationState.Transcribing)
    {
        dictationService.CancelTranscription();
        return Results.Ok(new { Success = true });
    }
    return Results.BadRequest(new { Success = false, Error = "Not transcribing" });
});

// Get SignalR hub context for broadcasting
var hubContext = webApp.Services.GetRequiredService<IHubContext<DictationHub>>();

try
{
    // Initialize tray icon service
    await trayService.InitializeMainIconAsync();
    trayService.SetIcon("push-to-talk");
    trayService.SetTooltip("Push To Talk - Idle");

    if (trayService.IsActive)
    {
        Console.WriteLine("D-Bus tray icon initialized");

        // Handle state changes from DictationService
        // Main icon stays visible, animated icon shows NEXT TO it during transcription (issue #62)
        dictationService.StateChanged += async (_, state) =>
        {
            // Update tray icon
            switch (state)
            {
                case DictationState.Idle:
                    trayService.HideAnimatedIcon();
                    // Check if SpeechToText service is running and show appropriate icon
                    var isRunning = await sttServiceManager.IsRunningAsync();
                    if (isRunning)
                    {
                        trayService.SetIcon("push-to-talk");
                        trayService.SetTooltip("Push To Talk - Idle");
                    }
                    else
                    {
                        trayService.SetIcon("push-to-talk-off");
                        trayService.SetTooltip("Push To Talk - Service Stopped");
                    }
                    break;
                case DictationState.Recording:
                    trayService.HideAnimatedIcon();
                    trayService.SetIcon("push-to-talk-recording");
                    trayService.SetTooltip("Push To Talk - Recording...");
                    break;
                case DictationState.Transcribing:
                    // Change icon back to white immediately when recording stops (issue #28)
                    trayService.SetIcon("push-to-talk");
                    trayService.SetTooltip("Push To Talk - Transcribing...");
                    // Show animated icon NEXT TO main icon (main stays visible)
                    await trayService.ShowAnimatedIconAsync();
                    break;
            }

            // Broadcast state change to SignalR clients
            var eventType = state switch
            {
                DictationState.Recording => DictationEventType.RecordingStarted,
                DictationState.Transcribing => DictationEventType.TranscriptionStarted,
                _ => DictationEventType.RecordingStopped
            };

            await hubContext.Clients.All.SendAsync("DictationEvent", new DictationEvent
            {
                EventType = eventType,
                Text = null
            });
        };

        // Handle transcription completion - send result to SignalR clients
        dictationService.TranscriptionCompleted += async (_, text) =>
        {
            await hubContext.Clients.All.SendAsync("DictationEvent", new DictationEvent
            {
                EventType = DictationEventType.TranscriptionCompleted,
                Text = text
            });
        };

        // Handle click on tray icon
        trayService.OnClicked += () =>
        {
            logger.LogInformation("Tray icon clicked");
        };

        // Handle Quit menu item
        trayService.OnQuitRequested += () =>
        {
            logger.LogInformation("Quit requested from tray menu");
            Console.WriteLine("\nQuit requested - shutting down...");
            cts.Cancel();
        };

        // Handle About menu item
        trayService.OnAboutRequested += () =>
        {
            logger.LogInformation("About dialog requested");
            AboutDialog.Show(version);
        };

        // Handle SpeechToText service stop request
        trayService.OnStopSpeechToTextRequested += async () =>
        {
            logger.LogInformation("Stopping SpeechToText service...");
            var stopped = await sttServiceManager.StopAsync();
            if (stopped)
            {
                logger.LogInformation("SpeechToText service stopped successfully");
                trayService.UpdateSpeechToTextStatus(false, sttServiceManager.GetVersion());
                trayService.SetIcon("push-to-talk-off");
                trayService.SetTooltip("Push To Talk (Service Stopped)");
            }
        };

        // Handle SpeechToText service start request
        trayService.OnStartSpeechToTextRequested += async () =>
        {
            logger.LogInformation("Starting SpeechToText service...");
            var started = await sttServiceManager.StartAsync();
            if (started)
            {
                logger.LogInformation("SpeechToText service started successfully");
                // Wait a moment for service to fully start
                await Task.Delay(1000);
                var isRunning = await sttServiceManager.IsRunningAsync();
                trayService.UpdateSpeechToTextStatus(isRunning, sttServiceManager.GetVersion());
                trayService.SetIcon("push-to-talk");
                trayService.SetTooltip("Push To Talk");
            }
            else
            {
                logger.LogError("Failed to start SpeechToText service");
            }
        };

        // Check SpeechToText service status on startup
        Task.Run(async () =>
        {
            try
            {
                logger.LogInformation("Checking SpeechToText service status...");
                var isRunning = await sttServiceManager.IsRunningAsync();
                var version = sttServiceManager.GetVersion();

                if (!isRunning)
                {
                    logger.LogInformation("SpeechToText service is not running, attempting to start...");
                    var started = await sttServiceManager.StartAsync();
                    if (started)
                    {
                        // Wait a moment for service to fully start
                        await Task.Delay(1000);
                        isRunning = await sttServiceManager.IsRunningAsync();
                    }
                }

                logger.LogInformation("SpeechToText service status: {Status}, version: {Version}",
                    isRunning ? "Running" : "Stopped", version);
                trayService.UpdateSpeechToTextStatus(isRunning, version);

                // Set icon based on service status
                if (isRunning)
                {
                    trayService.SetIcon("push-to-talk");
                    trayService.SetTooltip("Push To Talk");
                }
                else
                {
                    trayService.SetIcon("push-to-talk-off");
                    trayService.SetTooltip("Push To Talk (Service Stopped)");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to check/start SpeechToText service");
                trayService.UpdateSpeechToTextStatus(false, "Unknown");
                trayService.SetIcon("push-to-talk-off");
                trayService.SetTooltip("Push To Talk (Service Error)");
            }
        }).FireAndForget(logger, "SttServiceCheck");
    }
    else
    {
        logger.LogWarning("D-Bus tray icon failed to initialize, continuing without tray icon");
    }

    // Start web server in background
    Task.Run(async () =>
    {
        try
        {
            await webApp.StartAsync(cts.Token);
            // Wait until cancellation is requested
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }).FireAndForget(logger, "WebServer");

    Console.WriteLine("Web server started:");
    Console.WriteLine("  HTTP:  http://localhost:5050/remote.html");
    Console.WriteLine("  HTTPS: https://localhost:5051/remote.html (for PWA installation)");

    // Start keyboard monitoring in background
    Task.Run(async () =>
    {
        try
        {
            await dictationService.StartMonitoringAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Keyboard monitoring failed");
        }
    }).FireAndForget(logger, "KeyboardMonitoring");

    var triggerKey = options.GetTriggerKeyCode();
    Console.WriteLine($"Keyboard monitoring started ({triggerKey} to trigger)");
    Console.WriteLine("Press Ctrl+C to exit");
    Console.WriteLine();

    // Handle Ctrl+C (SIGINT)
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        Console.WriteLine("\nCtrl+C pressed - shutting down...");
        cts.Cancel();
    };

    // Handle SIGTERM (systemd shutdown, GNOME logout, etc.)
    using var sigtermRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
    {
        Console.WriteLine("\nSIGTERM received - shutting down...");
        context.Cancel = true; // Prevent default termination, let us clean up
        cts.Cancel();
    });

    // Keep the application running
    try
    {
        await Task.Delay(Timeout.Infinite, cts.Token);
    }
    catch (OperationCanceledException)
    {
        // Normal shutdown
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "Application error");
    Console.WriteLine($"Error: {ex.Message}");
    Environment.Exit(1);
}
finally
{
    cts.Cancel();
    await webApp.DisposeAsync();
    dictationService.Dispose();
    trayService.Dispose();
}

Console.WriteLine("PushToTalk stopped");
