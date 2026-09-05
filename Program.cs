using System.Text.Json;
using Microsoft.AspNetCore.HttpOverrides;
using WebApplication1;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args });

builder.Host.ConfigureAppConfiguration((_, configBuilder) =>
{
    foreach (var source in configBuilder.Sources
                 .OfType<Microsoft.Extensions.Configuration.FileConfigurationSource>())
    {
        source.ReloadOnChange = false;
    }
});

builder.Services.AddSignalR();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .SetIsOriginAllowed(_ => true)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseCors();

app.MapGet("/health", () => Results.Ok("OK"));

app.MapHub<ShakeHub>("/hub/shake", options =>
{
    options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets |
                         Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
});

app.MapGet("/api/room", () => Results.Ok(new
{
    code = Guid.NewGuid().ToString("N")[..6].ToUpper()
}));

app.MapPost("/api/quest-answers", (SubmitAnswerRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.SessionId))
        return Results.BadRequest(new { error = "sessionId is required" });

    if (string.IsNullOrWhiteSpace(req.QuestId) || !QuestAnswerStore.QuestIds.Contains(req.QuestId))
        return Results.BadRequest(new { error = $"unknown questId: {req.QuestId}" });

    if (req.Payload.ValueKind == JsonValueKind.Undefined)
        return Results.BadRequest(new { error = "payload is required" });

    QuestAnswerStore.Add(req.SessionId, req.QuestId, req.Payload);
    return Results.Ok(QuestAnswerStore.GetProgress(req.SessionId));
});

app.MapGet("/api/progress/{sessionId}", (string sessionId) =>
{
    if (string.IsNullOrWhiteSpace(sessionId))
        return Results.BadRequest(new { error = "sessionId is required" });

    return Results.Ok(QuestAnswerStore.GetProgress(sessionId));
});

app.MapDelete("/api/progress/{sessionId}", (string sessionId, string? questId) =>
{
    QuestAnswerStore.Reset(sessionId, questId);
    return Results.Ok(QuestAnswerStore.GetProgress(sessionId));
});

app.MapGet("/api/quest-answers", () => Results.Ok(QuestAnswerStore.All()));

app.Run();