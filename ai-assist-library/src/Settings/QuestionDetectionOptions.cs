namespace AiAssistLibrary.Settings;

public sealed class QuestionDetectionOptions
{
 // Enable question detection globally
 public bool Enabled { get; set; } = true;

 // Confidence threshold for emitting a detected question
 public double MinConfidence { get; set; } =0.5;

 // Use Azure OpenAI fallback when confidence is in [MinConfidence, FallbackPromoteMax)
 public bool EnableOpenAIFallback { get; set; } = false;

 // Azure OpenAI endpoint and deployment (when fallback enabled)
 public string? OpenAIEndpoint { get; set; }
 public string? OpenAIDeployment { get; set; }
 public string? OpenAIKey { get; set; }
}
