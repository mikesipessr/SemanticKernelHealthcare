// ============================================================
// TaskClassificationService.cs — Structured task extraction via GPT-4o
//
// This service takes the raw transcription text from Whisper and
// uses GPT-4o to identify and extract structured healthcare tasks.
//
// The approach is "prompt engineering for structured output":
//   1. We craft a system prompt that defines the exact JSON schema
//      we want and gives the model strict rules to follow.
//   2. We set Temperature = 0 so the model's output is as
//      deterministic and consistent as possible.
//   3. We deserialize the JSON response into typed C# objects.
//   4. If parsing fails for any reason, we log a warning and return
//      an empty list rather than letting an exception propagate.
//
// A future version could use OpenAI's structured output feature
// (which enforces the schema at the API level), but the prompt-based
// approach used here works across any chat model — including
// Azure OpenAI and non-OpenAI providers compatible with SK.
// ============================================================

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SemanticKernelHealthcare.Api.Models;

namespace SemanticKernelHealthcare.Api.Services;

public class TaskClassificationService(IChatCompletionService chatCompletion, ILogger<TaskClassificationService> logger)
    : ITaskClassificationService
{
    // JsonSerializerOptions shared across all requests (thread-safe when frozen).
    //
    // PropertyNameCaseInsensitive = true: deserializes both "patientFirstName"
    // (camelCase from GPT) and "PatientFirstName" (PascalCase if the model
    // deviates) without errors. Defensive parsing is better than being strict
    // when dealing with LLM output.
    //
    // JsonStringEnumConverter: deserializes enum values from their string
    // names ("MedicationRefill") rather than integer indices (0). This matches
    // the strings defined in the system prompt below.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // The system prompt is the most important part of this service.
    // It instructs GPT-4o to act as a medical office assistant and
    // return a strictly formatted JSON array.
    //
    // Key prompt engineering choices:
    //
    //   "Return ONLY a valid JSON array" — without this, the model often
    //   wraps the JSON in a markdown code block (```json ... ```) or adds
    //   a conversational prefix like "Here are the tasks:". Either would
    //   break JSON parsing.
    //
    //   Explicit schema with field names and allowed values — giving the
    //   model an exact target reduces hallucination and format drift.
    //
    //   "return an empty array: []" for the no-tasks case — without this,
    //   the model might return null, "none", or an explanation string.
    //
    //   "Do not invent patient names" — LLMs have a tendency to fill in
    //   missing information; this constraint prevents fabricated names.
    private const string SystemPrompt = """
        You are a medical office assistant. Extract all healthcare tasks from the transcription below.
        Return ONLY a valid JSON array. No explanation, no markdown, no code fences — just the raw array.

        Each element must match this schema exactly:
        {
          "type": one of ["MedicationRefill", "MedicationOrder", "ReferralOrder", "LabOrder"],
          "patientFirstName": string,
          "patientLastName": string,
          "description": string (concise clinical detail only — the test name, medication name, or specialty; no action verbs, no patient name)
        }

        Rules:
        - If no tasks are present, return an empty array: []
        - Only include tasks that clearly fit one of the four task types.
        - Do not invent patient names; use empty string if a name is not mentioned.
        """;

    public async Task<List<HealthcareTask>> ClassifyAsync(string transcription, CancellationToken ct = default)
    {
        // ChatHistory represents the conversation. The system prompt
        // goes in first (sets the model's persona and rules), then the
        // user message carries the actual transcription to classify.
        var chatHistory = new ChatHistory(SystemPrompt);
        chatHistory.AddUserMessage(transcription);

        // OpenAIPromptExecutionSettings tunes the GPT-4o request.
        //
        // Temperature = 0.0 makes the output as deterministic as possible.
        // For structured data extraction we want the model to follow the
        // schema precisely rather than exploring creative variations.
        //
        // MaxTokens = 1000 caps the response length. A clinical note is
        // unlikely to contain more than a handful of tasks, so 1000 tokens
        // is generous while preventing runaway usage if the model goes off-script.
        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0.0,
            MaxTokens   = 1000,
        };

        // GetChatMessageContentAsync sends the conversation to GPT-4o
        // and returns the model's reply as a ChatMessageContent object.
        // The .Content property holds the raw string (our JSON array).
        var response = await chatCompletion.GetChatMessageContentAsync(chatHistory, settings, cancellationToken: ct);
        var json = response.Content ?? "[]";

        // Attempt to deserialize the model's response into a typed list.
        // We guard against malformed JSON because LLMs can occasionally
        // return unexpected output (truncated response, markdown leak, etc.).
        // A warning is logged with the raw response so we can investigate
        // and improve the prompt if it happens frequently.
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
