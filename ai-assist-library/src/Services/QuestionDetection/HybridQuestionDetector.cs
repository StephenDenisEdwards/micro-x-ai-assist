using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AiAssistLibrary.Services.QuestionDetection;

// Hybrid detector placeholder: applies rule detector and can be extended to call external classifiers.
public sealed class HybridQuestionDetector : IQuestionDetector
{
    private const string ApiVersion = "2024-12-01-preview";
    private readonly RuleQuestionDetector _rule = new();
    private readonly double _minConfidence;
    private readonly ILogger<HybridQuestionDetector>? _log;
    private readonly HttpClient? _http;
    private readonly string? _endpoint;
    private readonly string? _deployment;
    private readonly string? _apiKey;
    private readonly bool _enabled;

    public HybridQuestionDetector(
        ILogger<HybridQuestionDetector>? log,
        double minConfidence,
        HttpClient? http,
        string? endpoint,
        string? deployment,
        string? apiKey,
        bool enableFallback)
    {
        _log = log;
        _minConfidence = minConfidence;
        _http = http;
        _endpoint = endpoint?.TrimEnd('/');
        _deployment = deployment;
        _apiKey = apiKey;
        _enabled = enableFallback &&
                   http is not null &&
                   !string.IsNullOrWhiteSpace(_endpoint) &&
                   !string.IsNullOrWhiteSpace(_deployment) &&
                   !string.IsNullOrWhiteSpace(_apiKey);
        if (!_enabled)
        {
            _log?.LogDebug("HybridQuestionDetector disabled: endpoint={Endpoint}, deployment={Deployment}, keyPresent={Key}",
                _endpoint, _deployment, !string.IsNullOrWhiteSpace(_apiKey));
        }
    }

    public IReadOnlyList<DetectedQuestion> Detect(string transcriptSegment, TimeSpan start, TimeSpan end, string? speakerId = null)
    {
        var prelim = _rule.Detect(transcriptSegment, start, end, speakerId).ToList();
        if (!_enabled) return prelim;

        var review = prelim.Where(q => q.Confidence >= _minConfidence && q.Confidence < 0.7).ToList();
        if (review.Count == 0) return prelim;

        try
        {
            var promptSb = new StringBuilder("Classify each line strictly as QUESTION or NOT. Output JSON lines: {\"text\":\"...\",\"isQuestion\":true|false}. Lines:\n");
            foreach (var r in review) promptSb.AppendLine(r.Text);

            var payload = new
            {
                messages = new[]
                {
                    new { role = "system", content = "You classify whether a line is a question." },
                    new { role = "user", content = promptSb.ToString() }
                },
                //temperature = 0,
                // Explicit model (deployment) for forward compatibility even though deployment is in URL
                model = _deployment
            };

            var url = $"{_endpoint}/openai/deployments/{_deployment}/chat/completions?api-version={ApiVersion}";
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            req.Headers.Add("api-key", _apiKey!);

            var resp = _http!.Send(req);
            if (!resp.IsSuccessStatusCode)
            {
                var body = resp.Content is null ? "<no body>" : resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                _log?.LogWarning("Azure OpenAI fallback non-success. Status={StatusCode}. Body={Body}", (int)resp.StatusCode, body);
                return prelim; // graceful fallback
            }

            using var doc = JsonDocument.Parse(resp.Content.ReadAsStream());
            var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;

            foreach (var r in review)
            {
                if (content.Contains(r.Text) && content.Contains("\"isQuestion\":true"))
                {
                    r.Confidence = Math.Min(1.0, r.Confidence + 0.2);
                }
                else if (content.Contains(r.Text) && content.Contains("\"isQuestion\":false"))
                {
                    r.Confidence = Math.Min(r.Confidence, 0.4);
                }
            }
        }
        catch (Exception ex)
        {
            _log?.LogWarning(ex, "Azure OpenAI fallback failed");
        }
        return prelim;
    }
}
