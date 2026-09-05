using System.Text.Json;

namespace WebApplication1;

public record SubmitAnswerRequest(
    string SessionId,
    string QuestId,
    JsonElement Payload
);

public record QuestAnswer(
    string SessionId,
    string QuestId,
    JsonElement Payload,
    string SubmittedAt
);

public record SessionProgress(
    string SessionId,
    Dictionary<string, JsonElement> Answers,
    string[] Completed,
    int Total
);