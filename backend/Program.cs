using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;
using SemanticKernelHealthcare.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var apiKey = builder.Configuration["OpenAI:ApiKey"]
    ?? throw new InvalidOperationException(
        "OpenAI:ApiKey is not configured. Add it to appsettings.Development.json or use dotnet user-secrets.");

var chatModel = builder.Configuration["OpenAI:ChatModel"] ?? "gpt-4o";
var whisperModel = builder.Configuration["OpenAI:WhisperModel"] ?? "whisper-1";

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();

builder.Services.AddKernel()
    .AddOpenAIAudioToText(whisperModel, apiKey)
    .AddOpenAIChatCompletion(chatModel, apiKey);

builder.Services.AddScoped<ITranscriptionService, TranscriptionService>();
builder.Services.AddScoped<ITaskClassificationService, TaskClassificationService>();

builder.Services.AddCors(options =>
    options.AddPolicy("ViteDev", policy =>
        policy.WithOrigins("http://localhost:5173", "https://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseCors("ViteDev");
    app.MapOpenApi();
}

app.UseStaticFiles();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
