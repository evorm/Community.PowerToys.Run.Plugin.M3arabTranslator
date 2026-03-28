using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Plugin;
using Clipboard = System.Windows.Clipboard;

namespace Community.PowerToys.Run.Plugin.M3arabTranslator
{
    public class Main : IPlugin, IDelayedExecutionPlugin, ISettingProvider
    {
        public static string PluginID => "A9D3B5F2C1E44D57B8A9F6C3E2D1A7B0";
        private const string IconPath = "Images\\icon.png";
        private const string DefaultModel = "gpt-5-nano";
        private const string DefaultInstructions =
            "Convert KUWAITI Arabizi/m3arab into natural KUWAITI Arabic written in standard Arabic script. " +
            "Output must use ONLY standard Arabic letters. Forbidden letters: گ, چ, پ, ڤ. Never output any of them. " +
            "Mandatory mappings for Kuwaiti/Gulf dialect: g -> ق, ch -> ج. Never map g to گ or ch to چ. " +
            "Resolve words as natural Kuwaiti Arabic words, not just letter-by-letter phonetic transcription. " +
            "Prefer the intended real Kuwaiti word if the phonetic input could map to either a nonsense form or a known Kuwaiti word. " +
            "This is a literal transliteration task only, not moderation, rewriting, or sanitization. " +
            "Be literal and preserve meaning, wording, and profanity. Do not censor, soften, replace, omit, or euphemize vulgar, sexual, insulting, or offensive words. " +
            "Keep word order unless a tiny spelling adjustment is needed for correct Arabic writing. " +
            "Use context to resolve ambiguous vowels and consonants. " +
            "Examples: 6ag3a -> طقعة, ok -> أوكي, zg -> زق, gary -> قاري, chai -> جاي, chub -> جب, diwaniya -> ديوانية, gahwa -> قهوة, shrayk -> شرايك. shraych -> شرايج. shfeech -> شفيج. " +
            "Do not explain. Do not paraphrase. Return only the final Arabic text with no tashkeel, even if unsure do not ask.";

        private const string DefaultReasoningEffort = "minimal";

        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        };

        private PluginInitContext? _context;

        private string _openAiKey = "";
        private string _model = DefaultModel;
        private string _instructions = DefaultInstructions;
        private string _reasoningEffort = DefaultReasoningEffort;

        // Tiny cache to avoid re-calling the API when PowerToys repeats the same delayed query.
        private string _lastInput = "";
        private string _lastOutput = "";
        private DateTime _lastAtUtc = DateTime.MinValue;
        private string _lastReasoningEffort = "";

        public string Name => "M3arab Translator";
        public string Description => "Phonetically converts Kuwaiti M3arab/Arabizi to Arabic and copies to clipboard";

        private static string BumpReasoningEffort(string effort)
        {
            return effort?.Trim().ToLowerInvariant() switch
            {
                "minimal" => "low",
                "low" => "medium",
                "medium" => "high",
                "high" => "high",
                _ => "low"
            };
        }

        public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>()
        {
            new PluginAdditionalOption
            {
                Key = "OpenAIKey",
                DisplayLabel = "OpenAI API Key",
                DisplayDescription = "Your OpenAI API key (starts with sk-...).",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                TextValue = _openAiKey,
            },
            new PluginAdditionalOption
            {
                Key = "Model",
                DisplayLabel = "OpenAI Model",
                DisplayDescription = $"Default: {DefaultModel}",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                TextValue = _model,
            },
            new PluginAdditionalOption
            {
                Key = "Instructions",
                DisplayLabel = "Instructions",
                DisplayDescription = "What the model should do to the input text.",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                TextValue = _instructions,
            },
            new PluginAdditionalOption
            {
                Key = "ReasoningEffort",
                DisplayLabel = "Reasoning Effort",
                DisplayDescription = "minimal, low, medium, or high. (Tip: \".\" at the beginning of a prompt temporarily increases reasoning effort)",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                TextValue = _reasoningEffort,
            }
        };

        public void Init(PluginInitContext context)
        {
            _context = context;
        }

       

    public void UpdateSettings(PowerLauncherPluginSettings settings)
    {
    

        if (settings?.AdditionalOptions == null)
            return;

        var key = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "OpenAIKey")?.TextValue;
        var model = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "Model")?.TextValue;
        var instructions = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "Instructions")?.TextValue;
        var reasoningEffort = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "ReasoningEffort")?.TextValue;

        if (!string.IsNullOrWhiteSpace(key))
            _openAiKey = key.Trim();

        _model = string.IsNullOrWhiteSpace(model) ? DefaultModel : model.Trim();
        _instructions = string.IsNullOrWhiteSpace(instructions) ? DefaultInstructions : instructions.Trim();
        _reasoningEffort = string.IsNullOrWhiteSpace(reasoningEffort) ? DefaultReasoningEffort : reasoningEffort.Trim();
    }

        // Non-delayed: no API call while typing.
        public List<Result> Query(Query query)
        {
            var input = (query?.Search ?? "").Trim();
            if (string.IsNullOrEmpty(input))
                return new List<Result>();

            return new List<Result>
            {
                new Result
                {
                    Title = "Translate to Arabic (waiting) (Tip: \".\" at the beginning for better translation)",
                    SubTitle = input,
                    IcoPath = IconPath,
                    Action = _ => true
                }
            };
        }

        // Delayed: called after a short idle, safe place to call the API.
        public List<Result> Query(Query query, bool delayedExecution)
        {
            var input = (query?.Search ?? "").Trim();
            if (string.IsNullOrEmpty(input))
                return new List<Result>();

            var effectiveReasoningEffort = _reasoningEffort;

            var dotCount = 0;
            while (dotCount < input.Length && input[dotCount] == '.')
                dotCount++;

            if (dotCount > 0)
            {
                input = input.Substring(dotCount).TrimStart();

                for (var i = 0; i < dotCount; i++)
                    effectiveReasoningEffort = BumpReasoningEffort(effectiveReasoningEffort);

                if (string.IsNullOrEmpty(input))
                    return new List<Result>();
            }

            if (string.IsNullOrWhiteSpace(_openAiKey))
            {
                return new List<Result>
                {
                    new Result
                    {
                        Title = "OpenAI API key missing",
                        SubTitle = "Set OpenAIKey in plugin settings.",
                        IcoPath = IconPath,
                        Action = _ => true
                    }
                };
            }

            try
            {
                if (string.Equals(input, _lastInput, StringComparison.Ordinal) &&
                    string.Equals(effectiveReasoningEffort, _lastReasoningEffort, StringComparison.OrdinalIgnoreCase) &&
                    (DateTime.UtcNow - _lastAtUtc) < TimeSpan.FromSeconds(10) &&
                    !string.IsNullOrEmpty(_lastOutput))
                {
                    return BuildResult(_lastOutput, copyToClipboard: !LooksLikeError(_lastOutput));
                }

                var arabic = TranslateAsync(input, effectiveReasoningEffort).GetAwaiter().GetResult();

                _lastInput = input;
                _lastOutput = arabic;
                _lastAtUtc = DateTime.UtcNow;
                _lastReasoningEffort = effectiveReasoningEffort;

                var copyToClipboard = !LooksLikeError(arabic);
                return BuildResult(arabic, copyToClipboard: copyToClipboard);
            }
            catch (OperationCanceledException)
            {
                return new List<Result>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                var msg = ex.InnerException?.Message ?? ex.Message;
                _context?.API?.ShowMsg("M3arab Translator", msg);
                return BuildResult($"(Translation failed: {msg})", "Check your key/model/internet.", copyToClipboard: false);
            }
        }

        private static bool LooksLikeError(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return true;

            return text.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) ||
                   text.StartsWith("(", StringComparison.Ordinal); // our internal error strings
        }

        private List<Result> BuildResult(string title, string subTitle = "Enter: copy to clipboard (Tip: \".\" at the beginning for better translation)", bool copyToClipboard = true)
        {
            return new List<Result>
            {
                new Result
                {
                    Title = title,
                    SubTitle = subTitle,
                    IcoPath = IconPath,
                    Action = _ =>
                    {
                        if (!copyToClipboard)
                        {
                            return true;
                        }

                        try
                        {
                            Clipboard.SetText(title ?? string.Empty);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex);
                            _context?.API?.ShowMsg("M3arab Translator", "Failed to copy to clipboard");
                        }

                        return true;
                    }
                }
            };
        }

        private async Task<string> TranslateAsync(string input, string reasoningEffort)
        {
            var instructions = _instructions;

            var body = new
            {

                model = _model,
                instructions,
                input = new object[]
                {
                    new
                    {
                        role = "user",
                        content = new object[] { new { type = "input_text", text = input } }
                    }
                },
                reasoning = new
                {
                    effort = reasoningEffort
                },
                text = new
                {
                    verbosity = "low"
                }
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _openAiKey);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            var jsonText = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
                return $"Error: {(int)resp.StatusCode} {resp.ReasonPhrase} | {jsonText}";

            try
            {
                using var doc = JsonDocument.Parse(jsonText);
                if (TryExtractOutputText(doc, out var text))
                    return text;

                return "(Could not parse OpenAI response text)";
            }
            catch (JsonException)
            {
                return "(Invalid JSON from OpenAI)";
            }
        }

        // PowerToys Settings may call this even if you use AdditionalOptions.
        public Control CreateSettingPanel()
        {
            return new UserControl
            {
                Content = new TextBlock
                {
                    Text = "Configure OpenAI API Key in PowerToys Settings → PowerToys Run → Plugins → M3arab Translator.",
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(10),
                }
            };
        }

        private static bool TryExtractOutputText(JsonDocument doc, out string text)
        {
            text = string.Empty;

            if (doc.RootElement.TryGetProperty("output_text", out var outputTextEl) &&
                outputTextEl.ValueKind == JsonValueKind.String)
            {
                text = (outputTextEl.GetString() ?? string.Empty).Trim();
                return !string.IsNullOrWhiteSpace(text);
            }

            if (!doc.RootElement.TryGetProperty("output", out var outputArr) || outputArr.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var message in outputArr.EnumerateArray())
            {
                if (!message.TryGetProperty("content", out var contentArr) || contentArr.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var item in contentArr.EnumerateArray())
                {
                    if (item.TryGetProperty("type", out var typeEl) &&
                        typeEl.ValueKind == JsonValueKind.String &&
                        string.Equals(typeEl.GetString(), "output_text", StringComparison.Ordinal) &&
                        item.TryGetProperty("text", out var textEl) &&
                        textEl.ValueKind == JsonValueKind.String)
                    {
                        text = (textEl.GetString() ?? string.Empty).Trim();
                        return !string.IsNullOrWhiteSpace(text);
                    }

                    // Some payloads may omit "type".
                    if (item.TryGetProperty("text", out var textEl2) && textEl2.ValueKind == JsonValueKind.String)
                    {
                        text = (textEl2.GetString() ?? string.Empty).Trim();
                        if (!string.IsNullOrWhiteSpace(text))
                            return true;
                    }
                }
            }

            return false;
        }
    }
}