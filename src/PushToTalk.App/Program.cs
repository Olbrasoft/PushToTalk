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
using Olbrasoft.PushToTalk.App.Api;
using Olbrasoft.PushToTalk.App.Configuration;
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

// Load web server configuration
var webServerOptions = new WebServerOptions();
config.GetSection("WebServer").Bind(webServerOptions);

// Load tray icon configuration
var trayIconOptions = new TrayIconOptions();
config.GetSection("TrayIcon").Bind(trayIconOptions);

// Validate configuration
ConfigurationValidator.ValidateWebServerOptions(webServerOptions);
ConfigurationValidator.ValidateTrayIconOptions(trayIconOptions);

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
services.AddTrayServices(options, iconsPath, trayIconOptions);

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
    serverOptions.ListenAnyIP(webServerOptions.Port); // HTTP

    // HTTPS only if configured and certificate exists (for PWA installation)
    if (webServerOptions.Https != null)
    {
        var certPath = Path.IsPathRooted(webServerOptions.Https.CertificatePath)
            ? webServerOptions.Https.CertificatePath
            : Path.Combine(AppContext.BaseDirectory, webServerOptions.Https.CertificatePath);

        if (File.Exists(certPath))
        {
            serverOptions.ListenAnyIP(webServerOptions.Https.Port, listenOptions =>
            {
                listenOptions.UseHttps(certPath, webServerOptions.Https.CertificatePassword);
            });
        }
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

// Map dictation API endpoints
webApp.MapDictationEndpoints();

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

        // Get state machine for event handling
        var stateMachine = serviceProvider.GetRequiredService<Olbrasoft.PushToTalk.App.StateMachine.IDictationStateMachine>();

        // Get Mistral provider for LLM correction toggle
        var mistralProvider = serviceProvider.GetRequiredService<Olbrasoft.PushToTalk.Core.Services.MistralProvider>();

        // Register all event handlers via EventHandlerRegistry
        var eventRegistry = new EventHandlerRegistry(
            loggerFactory.CreateLogger<EventHandlerRegistry>(),
            dictationService,
            stateMachine,
            trayService,
            hubContext,
            sttServiceManager,
            mistralProvider,
            version,
            cts);
        eventRegistry.RegisterHandlers();
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
    Console.WriteLine($"  HTTP:  http://localhost:{webServerOptions.Port}/remote.html");
    if (webServerOptions.Https != null)
    {
        Console.WriteLine($"  HTTPS: https://localhost:{webServerOptions.Https.Port}/remote.html (for PWA installation)");
    }

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

// Make implicit Program class explicit to avoid conflicts with referenced projects
namespace Olbrasoft.PushToTalk.App
{
    public partial class Program { }
}
