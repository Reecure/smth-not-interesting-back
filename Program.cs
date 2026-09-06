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

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = false;
    options.TimestampFormat = "HH:mm:ss ";
    options.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Disabled;
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

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Quest");
startupLogger.LogWarning("Quest API запущен. Квесты: {Quests}", string.Join(", ", QuestAnswerStore.QuestIds));

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

app.MapPost("/api/quest-answers", (SubmitAnswerRequest req, ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("Quest");

    if (string.IsNullOrWhiteSpace(req.SessionId))
    {
        logger.LogWarning("отклонено: пустой sessionId");
        return Results.BadRequest(new { error = "sessionId is required" });
    }

    if (string.IsNullOrWhiteSpace(req.QuestId) || !QuestAnswerStore.QuestIds.Contains(req.QuestId))
    {
        logger.LogWarning("отклонено: неизвестный questId {QuestId}", req.QuestId);
        return Results.BadRequest(new { error = $"unknown questId: {req.QuestId}" });
    }

    if (req.Payload.ValueKind == JsonValueKind.Undefined)
    {
        logger.LogWarning("отклонено: пустой payload для {QuestId}", req.QuestId);
        return Results.BadRequest(new { error = "payload is required" });
    }

    QuestAnswerStore.Add(req.SessionId, req.QuestId, req.Payload);

    var progress = QuestAnswerStore.GetProgress(req.SessionId);
    logger.LogWarning(
        "сохранён {QuestId} · сессия {SessionId} · пройдено {Done}/{Total}",
        req.QuestId,
        req.SessionId,
        progress.Completed.Length,
        QuestAnswerStore.QuestIds.Length
    );

    if (req.QuestId == "loading" || QuestAnswerStore.IsComplete(req.SessionId))
        SessionReport.Print(req.SessionId, logger);

    return Results.Ok(progress);
});

app.MapGet("/api/progress/{sessionId}", (string sessionId) =>
{
    if (string.IsNullOrWhiteSpace(sessionId))
        return Results.BadRequest(new { error = "sessionId is required" });

    return Results.Ok(QuestAnswerStore.GetProgress(sessionId));
});

app.MapDelete("/api/progress/{sessionId}", (string sessionId, string? questId, ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("Quest");
    QuestAnswerStore.Reset(sessionId, questId);
    logger.LogWarning("сброс {QuestId} для сессии {SessionId}", questId ?? "всё", sessionId);
    return Results.Ok(QuestAnswerStore.GetProgress(sessionId));
});

app.MapGet("/api/quest-answers", () => Results.Ok(QuestAnswerStore.All()));

app.MapGet("/api/report/{sessionId}", (string sessionId, ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("Quest");
    SessionReport.Print(sessionId, logger);
    return Results.Ok(QuestAnswerStore.GetProgress(sessionId));
});

app.MapGet("/api/report", (ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("Quest");
    var sessions = QuestAnswerStore.All()
        .Select(a => a.SessionId)
        .Distinct()
        .ToArray();

    foreach (var id in sessions)
        SessionReport.Print(id, logger);

    return Results.Ok(new { sessions, count = sessions.Length });
});

app.Run();