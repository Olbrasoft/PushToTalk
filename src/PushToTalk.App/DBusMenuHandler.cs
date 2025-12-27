using Microsoft.Extensions.Logging;
using Olbrasoft.SystemTray.Linux;
using Tmds.DBus.Protocol;
using Tmds.DBus.SourceGenerator;

namespace Olbrasoft.PushToTalk.App;

/// <summary>
/// D-Bus handler for com.canonical.dbusmenu interface.
/// Provides context menu for the tray icon with Quit and About items.
/// </summary>
internal class DBusMenuHandler : ComCanonicalDbusmenuHandler, ITrayMenuHandler
{
    private Connection? _connection;
    private readonly ILogger _logger;
    private uint _revision = 1;
    private PathHandler? _menuPathHandler;

    // Menu item IDs
    private const int RootId = 0;
    private const int AboutId = 1;
    private const int Separator1Id = 2;
    private const int SpeechToTextServiceId = 3;
    private const int Separator2Id = 4;
    private const int LlmCorrectionId = 5;
    private const int ReloadPromptId = 6;
    private const int Separator3Id = 7;
    private const int QuitId = 8;

    /// <summary>
    /// Event fired when user selects Quit from the menu.
    /// </summary>
    public event Action? OnQuitRequested;

    /// <summary>
    /// Event fired when user selects About from the menu.
    /// </summary>
    public event Action? OnAboutRequested;

    /// <summary>
    /// Event fired when user wants to stop SpeechToText service.
    /// </summary>
    public event Action? OnStopSpeechToTextRequested;

    /// <summary>
    /// Event fired when user wants to start SpeechToText service.
    /// </summary>
    public event Action? OnStartSpeechToTextRequested;

    /// <summary>
    /// Event fired when user toggles LLM correction.
    /// </summary>
    public event Action<bool>? OnLlmCorrectionToggled;

    /// <summary>
    /// Event fired when user wants to reload the Mistral prompt.
    /// </summary>
    public event Action? OnReloadPromptRequested;

    private string _sttServiceStatus = "Checking...";
    private string _sttServiceVersion = "Unknown";
    private bool _llmCorrectionEnabled = true;

    public DBusMenuHandler(ILogger logger) : base(emitOnCapturedContext: false)
    {
        _logger = logger;

        // Set D-Bus properties
        Version = 3; // dbusmenu protocol version
        TextDirection = "ltr";
        Status = "normal";
        IconThemePath = Array.Empty<string>();
    }

    public override Connection Connection => _connection ?? throw new InvalidOperationException("Connection not set. Call RegisterWithDbus first.");

    /// <summary>
    /// Registers the menu handler with D-Bus connection.
    /// Creates a PathHandler in this assembly and registers itself.
    /// </summary>
    public void RegisterWithDbus(Connection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));

        // Create PathHandler in THIS assembly (PushToTalk.App)
        // This avoids cross-assembly type incompatibility with PathHandler in SystemTray.Linux
        _menuPathHandler = new PathHandler("/MenuBar");

        // Set the PathHandler property (types match because both are from PushToTalk.App)
        PathHandler = _menuPathHandler;

        // Add ourselves to the handler
        _menuPathHandler.Add(this);

        // Register with D-Bus connection
        connection.AddMethodHandler(_menuPathHandler);

        _logger.LogDebug("Menu handler registered at /MenuBar in PushToTalk.App assembly");
    }

    /// <summary>
    /// Unregisters the menu handler from D-Bus connection.
    /// </summary>
    public void UnregisterFromDbus(Connection connection)
    {
        if (_menuPathHandler is not null)
        {
            _menuPathHandler.Remove(this);
            connection.RemoveMethodHandler(_menuPathHandler.Path);
            _menuPathHandler = null;
            _logger.LogDebug("Menu handler unregistered from /MenuBar");
        }
    }

    /// <summary>
    /// Updates SpeechToText service status and version in menu.
    /// </summary>
    public void UpdateSpeechToTextStatus(bool isRunning, string version)
    {
        _sttServiceStatus = isRunning ? "Running" : "Stopped";
        _sttServiceVersion = version;
        _revision++;

        // Emit LayoutUpdated signal only if connection and PathHandler are initialized
        // PathHandler is set by TrayIcon when registering the menu handler
        if (_connection != null && PathHandler != null)
        {
            EmitLayoutUpdated(_revision, RootId);
        }
    }

    /// <summary>
    /// Updates LLM correction enabled status in menu.
    /// </summary>
    public void UpdateLlmCorrectionStatus(bool enabled)
    {
        _llmCorrectionEnabled = enabled;
        _revision++;

        // Emit LayoutUpdated signal only if connection and PathHandler are initialized
        if (_connection != null && PathHandler != null)
        {
            EmitLayoutUpdated(_revision, RootId);
        }
    }

    /// <summary>
    /// Returns the menu layout starting from the specified parent ID.
    /// </summary>
    protected override ValueTask<(uint Revision, (int, Dictionary<string, VariantValue>, VariantValue[]) Layout)> OnGetLayoutAsync(
        Message request, int parentId, int recursionDepth, string[] propertyNames)
    {
        _logger.LogDebug("GetLayout: parentId={ParentId}, depth={Depth}", parentId, recursionDepth);

        var layout = BuildMenuLayout(parentId, recursionDepth);
        return ValueTask.FromResult((_revision, layout));
    }

    private (int, Dictionary<string, VariantValue>, VariantValue[]) BuildMenuLayout(int parentId, int recursionDepth)
    {
        if (parentId == RootId)
        {
            // Root menu with children
            var rootProps = new Dictionary<string, VariantValue>
            {
                ["children-display"] = VariantValue.String("submenu")
            };

            // For the root menu, return children as variants containing menu item structs
            // Each child is serialized as (ia{sv}av) wrapped in a variant
            VariantValue[] children;
            if (recursionDepth == 0)
            {
                children = Array.Empty<VariantValue>();
            }
            else
            {
                // Build child menu items
                // Since we can't easily create nested struct variants, we'll use a workaround:
                // Return child IDs only and let the shell query individual items
                children = new VariantValue[]
                {
                    CreateChildVariant(AboutId, "About", false),
                    CreateChildVariant(Separator1Id, "", true),
                    CreateChildVariant(SpeechToTextServiceId, $"STT Service: {_sttServiceStatus} (v{_sttServiceVersion})", false),
                    CreateChildVariant(Separator2Id, "", true),
                    CreateChildVariant(LlmCorrectionId, GetLlmCorrectionLabel(), false),
                    CreateChildVariant(ReloadPromptId, "🔄 Reload LLM Prompt", false),
                    CreateChildVariant(Separator3Id, "", true),
                    CreateChildVariant(QuitId, "Quit", false)
                };
            }

            return (RootId, rootProps, children);
        }

        // For non-root items, return the specific item
        return GetMenuItemLayout(parentId);
    }

    private VariantValue CreateChildVariant(int id, string label, bool isSeparator)
    {
        // Create a struct variant for menu item: (ia{sv}av)
        // We need to create this as a D-Bus struct variant

        // Build properties dictionary
        var props = new Dict<string, VariantValue>();
        if (isSeparator)
        {
            props.Add("type", VariantValue.String("separator"));
            props.Add("visible", VariantValue.Bool(true));
        }
        else
        {
            props.Add("label", VariantValue.String(label));
            props.Add("enabled", VariantValue.Bool(true));
            props.Add("visible", VariantValue.Bool(true));
        }

        // Empty children array for leaf items
        var children = new Array<VariantValue>();

        // Create the struct (ia{sv}av)
        return Struct.Create(id, props, children);
    }

    private string GetLlmCorrectionLabel()
    {
        return _llmCorrectionEnabled
            ? "✅ Posílání do LLM - Vypnout"
            : "❌ Posílání do LLM - Zapnout";
    }

    private (int, Dictionary<string, VariantValue>, VariantValue[]) GetMenuItemLayout(int id)
    {
        var props = new Dictionary<string, VariantValue>();

        switch (id)
        {
            case AboutId:
                props["label"] = VariantValue.String("About");
                props["enabled"] = VariantValue.Bool(true);
                props["visible"] = VariantValue.Bool(true);
                break;
            case Separator1Id:
                props["type"] = VariantValue.String("separator");
                props["visible"] = VariantValue.Bool(true);
                break;
            case SpeechToTextServiceId:
                props["label"] = VariantValue.String($"STT Service: {_sttServiceStatus} (v{_sttServiceVersion})");
                props["enabled"] = VariantValue.Bool(_sttServiceStatus == "Running");
                props["visible"] = VariantValue.Bool(true);
                break;
            case Separator2Id:
                props["type"] = VariantValue.String("separator");
                props["visible"] = VariantValue.Bool(true);
                break;
            case LlmCorrectionId:
                props["label"] = VariantValue.String(GetLlmCorrectionLabel());
                props["enabled"] = VariantValue.Bool(true);
                props["visible"] = VariantValue.Bool(true);
                break;
            case ReloadPromptId:
                props["label"] = VariantValue.String("🔄 Reload LLM Prompt");
                props["enabled"] = VariantValue.Bool(true);
                props["visible"] = VariantValue.Bool(true);
                break;
            case Separator3Id:
                props["type"] = VariantValue.String("separator");
                props["visible"] = VariantValue.Bool(true);
                break;
            case QuitId:
                props["label"] = VariantValue.String("Quit");
                props["enabled"] = VariantValue.Bool(true);
                props["visible"] = VariantValue.Bool(true);
                break;
        }

        return (id, props, Array.Empty<VariantValue>());
    }

    /// <summary>
    /// Returns properties for multiple menu items.
    /// </summary>
    protected override ValueTask<(int, Dictionary<string, VariantValue>)[]> OnGetGroupPropertiesAsync(
        Message request, int[] ids, string[] propertyNames)
    {
        _logger.LogDebug("GetGroupProperties: ids=[{Ids}]", string.Join(",", ids));

        var results = ids.Select(id => GetItemProperties(id)).ToArray();
        return ValueTask.FromResult(results);
    }

    private (int, Dictionary<string, VariantValue>) GetItemProperties(int id)
    {
        return id switch
        {
            RootId => (id, new Dictionary<string, VariantValue>
            {
                ["children-display"] = VariantValue.String("submenu")
            }),
            AboutId => (id, new Dictionary<string, VariantValue>
            {
                ["label"] = VariantValue.String("About"),
                ["enabled"] = VariantValue.Bool(true),
                ["visible"] = VariantValue.Bool(true)
            }),
            Separator1Id => (id, new Dictionary<string, VariantValue>
            {
                ["type"] = VariantValue.String("separator"),
                ["visible"] = VariantValue.Bool(true)
            }),
            SpeechToTextServiceId => (id, new Dictionary<string, VariantValue>
            {
                ["label"] = VariantValue.String($"STT Service: {_sttServiceStatus} (v{_sttServiceVersion})"),
                ["enabled"] = VariantValue.Bool(_sttServiceStatus == "Running"),
                ["visible"] = VariantValue.Bool(true)
            }),
            Separator2Id => (id, new Dictionary<string, VariantValue>
            {
                ["type"] = VariantValue.String("separator"),
                ["visible"] = VariantValue.Bool(true)
            }),
            LlmCorrectionId => (id, new Dictionary<string, VariantValue>
            {
                ["label"] = VariantValue.String(GetLlmCorrectionLabel()),
                ["enabled"] = VariantValue.Bool(true),
                ["visible"] = VariantValue.Bool(true)
            }),
            ReloadPromptId => (id, new Dictionary<string, VariantValue>
            {
                ["label"] = VariantValue.String("🔄 Reload LLM Prompt"),
                ["enabled"] = VariantValue.Bool(true),
                ["visible"] = VariantValue.Bool(true)
            }),
            Separator3Id => (id, new Dictionary<string, VariantValue>
            {
                ["type"] = VariantValue.String("separator"),
                ["visible"] = VariantValue.Bool(true)
            }),
            QuitId => (id, new Dictionary<string, VariantValue>
            {
                ["label"] = VariantValue.String("Quit"),
                ["enabled"] = VariantValue.Bool(true),
                ["visible"] = VariantValue.Bool(true)
            }),
            _ => (id, new Dictionary<string, VariantValue>())
        };
    }

    /// <summary>
    /// Returns a single property of a menu item.
    /// </summary>
    protected override ValueTask<VariantValue> OnGetPropertyAsync(Message request, int id, string name)
    {
        _logger.LogDebug("GetProperty: id={Id}, name={Name}", id, name);

        var props = GetItemProperties(id).Item2;
        if (props.TryGetValue(name, out var value))
        {
            return ValueTask.FromResult(value);
        }

        // Return empty string for unknown properties
        return ValueTask.FromResult(VariantValue.String(""));
    }

    /// <summary>
    /// Handles menu events (clicks).
    /// </summary>
    protected override ValueTask OnEventAsync(Message request, int id, string eventId, VariantValue data, uint timestamp)
    {
        _logger.LogDebug("Event: id={Id}, eventId={EventId}", id, eventId);

        if (eventId == "clicked")
        {
            switch (id)
            {
                case QuitId:
                    _logger.LogInformation("Quit menu item clicked");
                    OnQuitRequested?.Invoke();
                    break;
                case AboutId:
                    _logger.LogInformation("About menu item clicked");
                    OnAboutRequested?.Invoke();
                    break;
                case SpeechToTextServiceId:
                    _logger.LogInformation("SpeechToText service menu item clicked (status: {Status})", _sttServiceStatus);
                    // Toggle service based on current status
                    if (_sttServiceStatus == "Stopped")
                    {
                        OnStartSpeechToTextRequested?.Invoke();
                    }
                    else
                    {
                        OnStopSpeechToTextRequested?.Invoke();
                    }
                    break;
                case LlmCorrectionId:
                    _logger.LogInformation("LLM Correction menu item clicked (current: {Enabled})", _llmCorrectionEnabled);
                    // Toggle LLM correction
                    _llmCorrectionEnabled = !_llmCorrectionEnabled;
                    OnLlmCorrectionToggled?.Invoke(_llmCorrectionEnabled);
                    // Update menu to reflect new state
                    UpdateLlmCorrectionStatus(_llmCorrectionEnabled);
                    break;
                case ReloadPromptId:
                    _logger.LogInformation("Reload LLM Prompt menu item clicked");
                    OnReloadPromptRequested?.Invoke();
                    break;
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Handles batch menu events.
    /// </summary>
    protected override ValueTask<int[]> OnEventGroupAsync(Message request, (int, string, VariantValue, uint)[] events)
    {
        _logger.LogDebug("EventGroup: {Count} events", events.Length);

        foreach (var (id, eventId, data, timestamp) in events)
        {
            _ = OnEventAsync(request, id, eventId, data, timestamp);
        }

        return ValueTask.FromResult(Array.Empty<int>());
    }

    /// <summary>
    /// Called before showing a menu item. Returns whether the menu needs update.
    /// </summary>
    protected override ValueTask<bool> OnAboutToShowAsync(Message request, int id)
    {
        _logger.LogDebug("AboutToShow: id={Id}", id);
        return ValueTask.FromResult(false); // No update needed
    }

    /// <summary>
    /// Called before showing multiple menu items.
    /// </summary>
    protected override ValueTask<(int[] UpdatesNeeded, int[] IdErrors)> OnAboutToShowGroupAsync(Message request, int[] ids)
    {
        _logger.LogDebug("AboutToShowGroup: ids=[{Ids}]", string.Join(",", ids));
        return ValueTask.FromResult((Array.Empty<int>(), Array.Empty<int>()));
    }
}
