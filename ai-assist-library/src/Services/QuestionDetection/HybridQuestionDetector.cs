using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AiAssistLibrary.Services.QuestionDetection;

// Applies rule detector then (optionally) asks Azure to re-classify medium confidence items.
public sealed class HybridQuestionDetector : IQuestionDetector
{
	private const string ApiVersion = "2024-12-01-preview";
	private readonly RuleQuestionDetector _rule = new();
	private readonly RulesImperativeDetect _imperative = new();
	private readonly double _minConfidence;
	private readonly ILogger<HybridQuestionDetector>? _log;
	private readonly HttpClient? _http;
	private readonly string? _endpoint;
	private readonly string? _deployment;
	private readonly string? _apiKey;
	private readonly bool _enabled;

	// Imperative info-seeking starters (kept local to avoid changing RuleQuestionDetector).
	private static readonly string[] ImperativeInfoStarters =
	{
		"explain", "describe", "tell me", "show me", "give me", "help me",
		"please explain", "please describe", "please tell me", "please show me", "please give me", "please help me",
		"walk me through", "please walk me through"
	};

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
		// Run both detectors; imperative detection promotes commands to questions.
		var ruleQuestions = _rule.Detect(transcriptSegment, start, end, speakerId);
		var imperativeQuestions = _imperative.Detect(transcriptSegment, start, end, speakerId);

		// Merge, preferring higher confidence if duplicate text.
		var merged = new Dictionary<string, DetectedQuestion>(StringComparer.OrdinalIgnoreCase);
		void AddRange(IEnumerable<DetectedQuestion> src)
		{
			foreach (var q in src)
			{
				if (merged.TryGetValue(q.Text, out var existing))
				{
					existing.Confidence = Math.Max(existing.Confidence, q.Confidence);
					// If one category is Imperative keep it (so we can distinguish later)
					if (existing.Category != "Imperative" && q.Category == "Imperative")
						existing.Category = q.Category;
				}
				else
				{
					merged[q.Text] = q;
				}
			}
		}

		AddRange(ruleQuestions);
		AddRange(imperativeQuestions);

		var prelim = merged.Values.ToList();
		if (!_enabled) return prelim;

		static bool IsImperativeInfoRequest(string text)
		{
			var lower = text.Trim().ToLowerInvariant();
			foreach (var s in ImperativeInfoStarters)
				if (lower.StartsWith(s + " ")) return true;
			return false;
		}

		// Review:
		// - Medium confidence items (<0.7) that are >= _minConfidence
		// - Imperative info requests (<0.7) even if below _minConfidence
		var review = prelim.Where(q =>
				q.Confidence <0.7 &&
				(q.Confidence >= _minConfidence || IsImperativeInfoRequest(q.Text)))
			.ToList();

		if (review.Count ==0) return prelim;

		try
		{
			// Stable ID mapping.
			var reviewItems = review.Select((q, i) => new { id = i, text = q.Text }).ToArray();
			var linesJson = JsonSerializer.Serialize(reviewItems);

			var messages = new[]
			{
				new
				{
					role = "system",
					content =
						"You classify whether each utterance is a QUESTION. " +
						"QUESTION = seeks information, clarification, definition, comparison, explanation, or conceptual instruction. " +
						"Imperative info requests like 'Explain X', 'Describe Y', 'Walk me through Z' are QUESTION. " +
						"NOT = purely action commands (e.g. 'Deploy the update now.'), greetings, statements, status reports."
				},
				new
				{
					role = "user",
					content =
						"Return strict JSON only. Do not add commentary. Provide an array of {id,isQuestion} under property 'classifications'."
				},
				new
				{
					role = "user",
					content =
						"Examples:\n" +
						"\"Explain dependency injection in .NET applications.\" => true\n" +
						"\"Explain the difference between class and struct in C sharp.\" => true\n" +
						"\"It's stable, right?\" => true\n" +
						"\"Deploy the update now.\" => false\n" +
						"\"Send me the report.\" => false"
				},
				new
				{
					role = "user",
					content = $"Lines: {linesJson}"
				}
			};

			var payload = new
			{
				messages,
				model = _deployment,
				response_format = new
				{
					type = "json_schema",
					json_schema = new
					{
						name = "question_classification",
						schema = new
						{
							type = "object",
							required = new[] { "classifications" },
							additionalProperties = false,
							properties = new
							{
								classifications = new
								{
									type = "array",
									items = new
									{
										type = "object",
										required = new[] { "id", "isQuestion" },
										additionalProperties = false,
										properties = new
										{
											id = new { type = "integer" },
											isQuestion = new { type = "boolean" }
										}
									}
								}
							}
						}
					}
				}
			};

			var url = $"{_endpoint}/openai/deployments/{_deployment}/chat/completions?api-version={ApiVersion}";
			using var req = new HttpRequestMessage(HttpMethod.Post, url)
			{
				Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
			};
			req.Headers.Add("api-key", _apiKey!);

			var resp = _http!.Send(req);
			if (!resp.IsSuccessStatusCode)
			{
				var body = resp.Content is null ? "<no body>" : resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
				_log?.LogWarning("Azure fallback non-success. Status={StatusCode}. Body={Body}", (int)resp.StatusCode, body);
				return prelim;
			}

			using var doc = JsonDocument.Parse(resp.Content.ReadAsStream());
			var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
			if (string.IsNullOrWhiteSpace(content))
			{
				_log?.LogWarning("Azure fallback returned empty content");
				return prelim;
			}

			using var classifiedDoc = JsonDocument.Parse(content);
			if (!classifiedDoc.RootElement.TryGetProperty("classifications", out var arr) || arr.ValueKind != JsonValueKind.Array)
			{
				_log?.LogWarning("Azure fallback unexpected JSON: {Content}", content);
				return prelim;
			}

			foreach (var item in arr.EnumerateArray())
			{
				if (!item.TryGetProperty("id", out var idEl) || !item.TryGetProperty("isQuestion", out var qEl))
					continue;

				var id = idEl.GetInt32();
				if (id <0 || id >= review.Count) continue;
				var isQuestion = qEl.GetBoolean();
				var original = review[id];
				if (isQuestion)
					original.Confidence = Math.Max(original.Confidence,0.75); // elevate to stable acceptance
				else
					original.Confidence = Math.Min(original.Confidence,0.4); // push down ambiguous non-question
			}
		}
		catch (Exception ex)
		{
			_log?.LogWarning(ex, "Azure fallback failed");
		}

		return prelim;
	}
}
