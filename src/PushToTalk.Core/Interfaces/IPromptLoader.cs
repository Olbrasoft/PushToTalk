namespace Olbrasoft.PushToTalk.Core.Interfaces;

/// <summary>
/// Interface for loading prompt templates from various sources.
/// </summary>
public interface IPromptLoader
{
    /// <summary>
    /// Loads a prompt template by its name.
    /// </summary>
    /// <param name="promptName">The name of the prompt to load (without extension).</param>
    /// <returns>The prompt content as a string.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the prompt file is not found.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the prompt content cannot be read.</exception>
    string LoadPrompt(string promptName);
}
