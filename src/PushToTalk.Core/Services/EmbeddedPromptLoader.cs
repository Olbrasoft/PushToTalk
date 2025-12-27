using System.Reflection;
using Olbrasoft.PushToTalk.Core.Interfaces;

namespace Olbrasoft.PushToTalk.Core.Services;

/// <summary>
/// Loads prompt templates from embedded resources in the assembly.
/// </summary>
public class EmbeddedPromptLoader : IPromptLoader
{
    private readonly Assembly _assembly;
    private readonly string _baseNamespace;

    public EmbeddedPromptLoader()
        : this(Assembly.GetExecutingAssembly(), "Olbrasoft.PushToTalk.Core.Prompts")
    {
    }

    public EmbeddedPromptLoader(Assembly assembly, string baseNamespace)
    {
        _assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        _baseNamespace = baseNamespace ?? throw new ArgumentNullException(nameof(baseNamespace));
    }

    public string LoadPrompt(string promptName)
    {
        if (string.IsNullOrWhiteSpace(promptName))
        {
            throw new ArgumentException("Prompt name cannot be null or whitespace.", nameof(promptName));
        }

        var resourceName = $"{_baseNamespace}.{promptName}.md";

        using var stream = _assembly.GetManifestResourceStream(resourceName);

        if (stream == null)
        {
            var availableResources = string.Join(", ", _assembly.GetManifestResourceNames());
            throw new FileNotFoundException(
                $"Embedded resource '{resourceName}' not found. Available resources: {availableResources}",
                resourceName);
        }

        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException($"Prompt '{resourceName}' is empty or contains only whitespace.");
        }

        return content;
    }
}
