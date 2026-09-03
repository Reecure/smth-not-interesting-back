using Microsoft.AspNetCore.HttpOverrides;
using WebApplication1;

var builder = WebApplication.CreateBuilder(args);

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

app.MapPost("/api/quest-answers", (QuestAnswer answer) =>
{
    QuestAnswerStore.Add(answer);
    return Results.Ok();
});

app.MapGet("/api/quest-answers", () => Results.Ok(QuestAnswerStore.All()));

app.MapGet("/api/quest-answers/{sessionId}", (string sessionId) =>
    Results.Ok(QuestAnswerStore.BySession(sessionId)));

app.Run();