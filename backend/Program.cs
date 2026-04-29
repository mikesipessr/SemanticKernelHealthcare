// ============================================================
// Program.cs — Application entry point and service registration
//
// ASP.NET Core's "minimal hosting" model lets us configure the
// entire application in a single file. The pattern is:
//   1. Create a WebApplicationBuilder (configures DI, logging, config)
//   2. Register services into the DI container
//   3. Build the WebApplication
//   4. Register middleware and route handlers
//   5. Run the app
// ============================================================

using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;
using SemanticKernelHealthcare.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ------------------------------------------------------------
// Configuration
// ASP.NET Core's configuration system merges values from
// appsettings.json → appsettings.{Environment}.json → environment
// variables → user secrets (in development). We read the OpenAI
// API key here so we can fail fast with a clear message if it
// hasn't been set, rather than crashing inside a service later.
// ------------------------------------------------------------
var apiKey = builder.Configuration["OpenAI:ApiKey"]
    ?? throw new InvalidOperationException(
        "OpenAI:ApiKey is not configured. Add it to appsettings.Development.json or use dotnet user-secrets.");

// Model names are configurable so you can swap models without
// recompiling. Defaults are sensible values for most use cases.
var chatModel    = builder.Configuration["OpenAI:ChatModel"]    ?? "gpt-4o";
var whisperModel = builder.Configuration["OpenAI:WhisperModel"] ?? "whisper-1";

// ------------------------------------------------------------
// MVC / Controller registration
// AddControllers() scans for classes decorated with [ApiController]
// and registers them as route handlers.
//
// AddJsonOptions() configures System.Text.Json, the default JSON
// serializer in ASP.NET Core. The JsonStringEnumConverter makes
// enum values serialize as strings ("ReferralOrder") instead of
// integers (2), which is much more readable in API responses and
// matches what our GPT-4o classification prompt expects.
// ------------------------------------------------------------
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// AddOpenApi() enables the built-in OpenAPI document generation
// introduced in .NET 9+. In development you can browse the spec at
// /openapi/v1.json and use it with tools like Scalar or Swagger UI.
builder.Services.AddOpenApi();

// ------------------------------------------------------------
// Semantic Kernel registration
//
// Semantic Kernel (SK) is Microsoft's open-source SDK for building
// AI-powered applications. It acts as an orchestration layer that
// abstracts different AI providers behind common interfaces.
//
// AddKernel() registers the Kernel and its builder into ASP.NET
// Core's DI container. The fluent calls that follow register two
// OpenAI connectors on the same Kernel:
//
//   AddOpenAIAudioToText  → makes IAudioToTextService injectable
//                           backed by OpenAI's Whisper model
//
//   AddOpenAIChatCompletion → makes IChatCompletionService injectable
//                             backed by GPT-4o
//
// By registering both on the same builder the Kernel knows about
// both services, and our application services can inject whichever
// interface they need directly — no factory or locator pattern required.
// ------------------------------------------------------------
builder.Services.AddKernel()
    .AddOpenAIAudioToText(whisperModel, apiKey)
    .AddOpenAIChatCompletion(chatModel, apiKey);

// ------------------------------------------------------------
// Application service registration
//
// AddScoped creates a new service instance per HTTP request.
// That's the right lifetime here: each request gets its own
// TranscriptionService and TaskClassificationService, which in turn
// receive their SK interfaces from the DI container.
// ------------------------------------------------------------
builder.Services.AddScoped<ITranscriptionService, TranscriptionService>();
builder.Services.AddScoped<ITaskClassificationService, TaskClassificationService>();

// ------------------------------------------------------------
// CORS policy for Vite's development server
//
// In production, the React app is served as static files from
// the same origin as the API, so there's no cross-origin issue.
// In development, however, Vite runs on port 5173 while the API
// runs on port 5050 — different origins. This policy allows the
// browser to make cross-origin requests from the Vite dev server.
//
// Note: Vite's proxy (vite.config.ts) already rewrites /api/*
// requests so the browser never actually sees a cross-origin URL
// during development. This CORS policy is a safety net for any
// direct browser-to-API calls (e.g. from Postman or curl tests).
// ------------------------------------------------------------
builder.Services.AddCors(options =>
    options.AddPolicy("ViteDev", policy =>
        policy.WithOrigins("http://localhost:5173", "https://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()));

var app = builder.Build();

// ------------------------------------------------------------
// Middleware pipeline
//
// Order matters: each middleware wraps the next in a chain.
// Requests flow top-to-bottom; responses bubble back up.
// ------------------------------------------------------------

if (app.Environment.IsDevelopment())
{
    // Enable CORS only in development. In production the frontend
    // is served from the same origin, so CORS isn't needed.
    app.UseCors("ViteDev");

    // Expose the OpenAPI JSON endpoint for API exploration.
    app.MapOpenApi();
}

// Serve static files from wwwroot/. In production you would copy
// the Vite build output (frontend/dist) to backend/wwwroot before
// publishing so the API and UI ship as a single deployable unit.
app.UseStaticFiles();

// Route incoming requests to the matching [ApiController] action.
app.MapControllers();

// For any request that doesn't match a controller route, serve
// index.html. This is the standard SPA fallback pattern: it lets
// the React router handle client-side navigation (e.g. /dashboard)
// without the server returning a 404 for routes it doesn't know about.
app.MapFallbackToFile("index.html");

app.Run();
