using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SemanticKernelHealthcare.Api.Models;

namespace SemanticKernelHealthcare.Api.Services;

public class TaskClassificationService(IChatCompletionService chatCompletion, ILogger<TaskClassificationService> logger)
    : ITaskClassificationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private const string SystemPrompt = """
        You are a medical office assistant. Extract all healthcare tasks from the transcription below.
        Return ONLY a valid JSON array. No explanation, no markdown, no code fences — just the raw array.

        Each element must match this schema exactly:
        {
          "type": one of ["MedicationRefill", "MedicationOrder", "ReferralOrder", "LabOrder"],
          "patientFirstName": string,
          "patientLastName": string,
          "description": string (a complete sentence describing what needs to happen)
        }

        Rules:
        - If no tasks are present, return an empty array: []
        - Only include tasks that clearly fit one of the four task types.
        - Do not invent patient names; use empty string if a name is not mentioned.
        """;

    public async Task<List<HealthcareTask>> ClassifyAsync(string transcription, CancellationToken ct = default)
    {
        var chatHistory = new ChatHistory(SystemPrompt);
        chatHistory.AddUserMessage(transcription);

        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0.0,
            MaxTokens = 1000,
        };

        var response = await chatCompletion.GetChatMessageContentAsync(chatHistory, settings, cancellationToken: ct);
        var json = response.Content ?? "[]";

        try
        {
            return JsonSerializer.Deserialize<List<HealthcareTask>>(json, JsonOptions) ?? [];
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse task classification response: {Raw}", json);
            return [];
        }
    }
}
