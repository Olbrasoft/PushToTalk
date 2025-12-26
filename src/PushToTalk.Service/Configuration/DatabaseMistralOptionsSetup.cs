using Microsoft.Extensions.Options;
using Olbrasoft.PushToTalk.Core.Configuration;
using PushToTalk.Data.EntityFrameworkCore;

namespace Olbrasoft.PushToTalk.Service.Configuration;

/// <summary>
/// Configures MistralOptions from database instead of appsettings.json or user secrets.
/// Loads active Mistral configuration from mistral_configs table.
/// </summary>
public class DatabaseMistralOptionsSetup : IConfigureOptions<MistralOptions>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseMistralOptionsSetup> _logger;

    public DatabaseMistralOptionsSetup(
        IServiceProvider serviceProvider,
        ILogger<DatabaseMistralOptionsSetup> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public void Configure(MistralOptions options)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PushToTalkDbContext>();

        var config = dbContext.MistralConfigs
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefault();

        if (config == null)
        {
            _logger.LogError("No active Mistral configuration found in database. Please insert a record into mistral_configs table.");
            throw new InvalidOperationException("No active Mistral configuration found in database");
        }

        _logger.LogInformation("Loading Mistral configuration from database (ID: {ConfigId}, Label: {Label})",
            config.Id, config.Label ?? "N/A");

        options.ApiKey = config.ApiKey;
        options.Model = config.Model;
        options.BaseUrl = config.BaseUrl;
        options.TimeoutSeconds = config.TimeoutSeconds;
        options.MaxTokens = config.MaxTokens;
        options.Temperature = config.Temperature;
    }
}
