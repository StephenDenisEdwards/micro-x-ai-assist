using AiAssistLibrary.Services;
using AiAssistLibrary.Services.QuestionDetection;
using Microsoft.Extensions.DependencyInjection;

namespace AiAssistLibrary.Extensions;

public static class ServiceCollectionQuestionExtensions
{
 public static IServiceCollection AddQuestionDetection(this IServiceCollection services)
 {
 // No global services required yet; detection is created inside SpeechPushClient
 return services;
 }
}
