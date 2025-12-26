using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.PushToTalk.Core.Configuration;
using Olbrasoft.PushToTalk.Core.Interfaces;

namespace Olbrasoft.PushToTalk.Core.Services;

/// <summary>
/// Mistral AI provider for correcting Czech ASR transcriptions.
/// </summary>
public class MistralProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly MistralOptions _options;
    private readonly ILogger<MistralProvider> _logger;
    private Dictionary<string, string> _lastRateLimitHeaders = new();

    private const string SystemPrompt = @"Jsi expert na opravu českých ASR (Automatic Speech Recognition) transkripce z Whisper modelu.

**DŮLEŽITÉ: VRAŤ POUZE OPRAVENOU TRANSKRIPCI. ŽÁDNÉ <think> tagy, žádné vysvětlení, jen opravený text.**

## Kontext systému

### Adresářová struktura

**Bash skripty:**
- Umístění: `~/.local/bin/`

**Repozitáře:**
- Hlavní: `~/Olbrasoft/` a `~/GitHub/Olbrasoft/`
- Aktivní projekty: **PushToTalk**, **VirtualAssistant**, **GitHub.Issues**

**Deployment:**
- `/opt/olbrasoft/virtual-assistant/`
- `/opt/olbrasoft/push-to-talk/`

### Databáze (PostgreSQL)

**Dostupné databáze:**
1. `push_to_talk` - Tabulky: whisper_transcriptions, transcription_corrections, llm_corrections
2. `virtual_assistant` - Tabulky: notifications, github_issues, embeddings
3. `github_issues` - Tabulky: issues, embeddings, repositories

### Technologie

- .NET 10, Python 3.13
- PostgreSQL, Ollama
- Whisper, Azure TTS
- Docker, systemd

## Pravidla korekce

### 1. Názvy projektů (dle kontextu)

**Repozitář/Projekt → PascalCase:**
- **PushToTalk**, **VirtualAssistant**, **GitHub.Issues**

**Databáze/Tabulka → snake_case:**
- push_to_talk, virtual_assistant, github_issues, whisper_transcriptions, llm_corrections

**Deployment cesta → kebab-case:**
- /opt/olbrasoft/push-to-talk/
- /opt/olbrasoft/virtual-assistant/

### 2. Technické termíny

**Whisper:**
- wis, whisp, výšpel, vyspra, sprem → Whisper

**Ostatní:**
- github → GitHub
- docker → Docker
- postgres → PostgreSQL
- ola, olla → Ollama

### 3. Časté chyby češtiny

**Imperativ:**
- spust, spuš → spusť
- projdí, projď → projdi

**Diakritika:**
- zapl → zapnul
- vzadím → vsadím
- pšu → píšu
- bít → být
- tabúku → tabulku

**Fonetické:**
- viky → wiki
- konhonem → konečně
- soubody → soubory
- potržítko → pomlčka
- bešový → bashové
- nejrých → nejprve/nejdříve
- obrazovt → projekt/adresář (dle kontextu)

**Gramatika:**
- jaký modely → jaké modely
- který jsou → které jsou
- nesnáš → nesnažíš/nesnažím
- bysme → bychom

**Anglicismy:**
- i shoes → issues
- requestů → požadavků

### 4. ZLEPŠENÍ ČEŠTINY

**Odstraň opakování slov:**
- ""kde máme uložený ty... kde máme uložený repozitáře"" → odstranit opakování
- ""který mu, který mu"" → ""který mu""
- ""můžeme vzít, můžeme"" → ""můžeme vzít""

**Odstraň mluvené výplně:**
- ""teda"" → ""tedy"" nebo vypustit
- ""prostě"" → vypustit
- ""žeho"" → vypustit nebo nahradit vhodným slovem
- ""jako"" → vypustit pokud není nutné

**Zlepši strukturu:**
- Přidej interpunkci (čárky) kde chybí
- Oprav slovosled pokud je neobvyklý
- Zpřesni význam vágních výrazů

**Zachovej smysl:**
- NEMĚŇ význam původního textu
- Pouze zpřesni a zlepši čitelnost

## VÝSTUP

**VRAŤ JEN OPRAVENOU TRANSKRIPCI. ŽÁDNÉ <think>, ŽÁDNÉ KOMENTÁŘE.**";

    public string ProviderName => "mistral";

    public MistralProvider(
        HttpClient httpClient,
        IOptions<MistralOptions> options,
        ILogger<MistralProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    public async Task<string> CorrectTextAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new
            {
                model = _options.Model,
                messages = new[]
                {
                    new { role = "system", content = SystemPrompt },
                    new { role = "user", content = text }
                },
                temperature = _options.Temperature,
                max_tokens = _options.MaxTokens
            };

            var response = await _httpClient.PostAsJsonAsync("/v1/chat/completions", request, cancellationToken);

            // Capture rate limit headers
            _lastRateLimitHeaders = new Dictionary<string, string>();
            foreach (var header in response.Headers.Where(h => h.Key.StartsWith("x-ratelimit-", StringComparison.OrdinalIgnoreCase)))
            {
                _lastRateLimitHeaders[header.Key] = string.Join(", ", header.Value);
            }

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<MistralResponse>(cancellationToken);

            if (result?.Choices == null || result.Choices.Length == 0)
            {
                throw new InvalidOperationException("Mistral API returned empty response");
            }

            var correctedText = result.Choices[0].Message.Content.Trim();

            _logger.LogInformation("Mistral correction completed. Original length: {OriginalLength}, Corrected length: {CorrectedLength}",
                text.Length, correctedText.Length);

            return correctedText;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling Mistral API: {Message}", ex.Message);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Mistral API request timeout");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling Mistral API: {Message}", ex.Message);
            throw;
        }
    }

    public Dictionary<string, string> GetLastRateLimitHeaders()
    {
        return new Dictionary<string, string>(_lastRateLimitHeaders);
    }

    private class MistralResponse
    {
        public MistralChoice[] Choices { get; set; } = Array.Empty<MistralChoice>();
    }

    private class MistralChoice
    {
        public MistralMessage Message { get; set; } = new();
    }

    private class MistralMessage
    {
        public string Content { get; set; } = string.Empty;
    }
}
