using WebApplication1;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .SetIsOriginAllowed(_ => true)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

app.UseCors();
app.MapHub<ShakeHub>("/hub/shake");

app.MapGet("/api/room", () => Results.Ok(new
{
    code = Guid.NewGuid().ToString("N")[..6].ToUpper()
}));

app.MapPost("/api/quest-answers", (QuestAnswer answer) =>
{
    QuestAnswerStore.Add(answer);
    return Results.Ok();
});

app.MapGet("/api/quest-answers", () => Results.Ok(QuestAnswerStore.All()));

app.MapGet("/api/quest-answers/{sessionId}", (string sessionId) =>
    Results.Ok(QuestAnswerStore.BySession(sessionId)));

app.Run();