
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiAssistLibrary.ConversationMemory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;


namespace AiAssistLibrary.LLM;

public sealed class GoogleGeminiAnswerProvider : IAnswerProvider
{
	private readonly HttpClient _http;
	private readonly GoogleGeminiOptions _opts;
	private readonly ILogger<GoogleGeminiAnswerProvider> _log;

	public GoogleGeminiAnswerProvider(
		HttpClient http,
		IOptions<GoogleGeminiOptions> opts,
		ILogger<GoogleGeminiAnswerProvider> log)
	{
		_http = http;
		_opts = opts.Value;
		_log = log;
	}

	public async Task<string> GetAnswerAsync(
		PromptPack pack,
		CancellationToken ct = default,
		string? overrideModel = null)
	{
		if (string.IsNullOrWhiteSpace(_opts.ApiKey))
			throw new InvalidOperationException("GoogleGeminiOptions.ApiKey is not configured.");

		var model = string.IsNullOrWhiteSpace(overrideModel)
			? _opts.Model
			: overrideModel;

		// Use your existing "raw block sent to model"
		var userPrompt = pack.AssembledPrompt ?? string.Empty;

		// Optional: system instructions – this is exactly what AI Studio's "System Instructions" does
		GeminiContent? systemInstruction = null;
		if (!string.IsNullOrWhiteSpace(pack.SystemPrompt))
		{
			systemInstruction = new GeminiContent
			{
				// role is ignored for systemInstruction in Vertex/Gemini; safe to set or omit
				Role = "system",
				Parts =
				[
					new GeminiPart { Text = pack.SystemPrompt }
				]
			};
		}

		var request = new GeminiRequest
		{
			Contents =
			[
				new GeminiContent
				{
					Role = "user",
					Parts =
					[
						new GeminiPart { Text = userPrompt }
					]
				}
			],
			SystemInstruction = systemInstruction,
			GenerationConfig = new GeminiGenerationConfig
			{
				Temperature = 0.1,
				MaxOutputTokens = 256
			}
		};

		var url =
			$"{_opts.BaseUrl}/models/{model}:generateContent?key={Uri.EscapeDataString(_opts.ApiKey)}";

		var json = JsonSerializer.Serialize(
			request,
			new JsonSerializerOptions
			{
				DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase
			});

		using var httpReq = new HttpRequestMessage(HttpMethod.Post, url)
		{
			Content = new StringContent(json, Encoding.UTF8, "application/json")
		};

		HttpResponseMessage httpResp;
		try
		{
			httpResp = await _http.SendAsync(httpReq, ct);
		}
		catch (Exception ex)
		{
			_log.LogError(ex, "Error calling Gemini generateContent.");
			throw;
		}

		var respBody = await httpResp.Content.ReadAsStringAsync(ct);

		if (!httpResp.IsSuccessStatusCode)
		{
			_log.LogError(
				"Gemini returned non-success status code {Status}: {Body}",
				(int)httpResp.StatusCode,
				respBody);

			throw new InvalidOperationException(
				$"Gemini call failed with status {(int)httpResp.StatusCode}: {respBody}");
		}

		GeminiResponse? resp;
		try
		{
			resp = JsonSerializer.Deserialize<GeminiResponse>(
				respBody,
				new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true
				});
		}
		catch (Exception ex)
		{
			_log.LogError(ex, "Failed to deserialize Gemini response: {Json}", respBody);
			throw;
		}

		var text = resp?.GetCombinedText()?.Trim();
		return string.IsNullOrWhiteSpace(text) ? string.Empty : text;
	}

	// ---- DTOs for Gemini API ----

	private sealed class GeminiRequest
	{
		[JsonPropertyName("contents")]
		public List<GeminiContent> Contents { get; set; } = [];

		[JsonPropertyName("systemInstruction")]
		public GeminiContent? SystemInstruction { get; set; }

		[JsonPropertyName("generationConfig")]
		public GeminiGenerationConfig? GenerationConfig { get; set; }
	}

	private sealed class GeminiContent
	{
		[JsonPropertyName("role")]
		public string? Role { get; set; }

		[JsonPropertyName("parts")]
		public List<GeminiPart> Parts { get; set; } = [];
	}

	private sealed class GeminiPart
	{
		[JsonPropertyName("text")]
		public string? Text { get; set; }
	}

	private sealed class GeminiGenerationConfig
	{
		[JsonPropertyName("temperature")]
		public double Temperature { get; set; }

		[JsonPropertyName("maxOutputTokens")]
		public int MaxOutputTokens { get; set; }
	}

	private sealed class GeminiResponse
	{
		[JsonPropertyName("candidates")]
		public List<GeminiCandidate>? Candidates { get; set; }

		public string? GetCombinedText()
		{
			if (Candidates is null || Candidates.Count == 0)
				return null;

			var first = Candidates[0];
			if (first.Content?.Parts == null)
				return null;

			var sb = new StringBuilder();
			foreach (var p in first.Content.Parts)
			{
				if (!string.IsNullOrEmpty(p.Text))
					sb.Append(p.Text);
			}

			return sb.ToString();
		}
	}

	private sealed class GeminiCandidate
	{
		[JsonPropertyName("content")]
		public GeminiContent? Content { get; set; }
	}
}
