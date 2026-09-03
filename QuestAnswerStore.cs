using System.Collections.Concurrent;
using System.Text.Json;

namespace WebApplication1;

public record QuestAnswer(string QuestId, JsonElement Payload, string SubmittedAt, string? SessionId);

public static class QuestAnswerStore
{
    private static readonly ConcurrentBag<QuestAnswer> Answers = new();
    private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "quest-answers.jsonl");
    private static readonly object FileLock = new();

    static QuestAnswerStore()
    {
        if (!File.Exists(FilePath)) return;

        foreach (var line in File.ReadAllLines(FilePath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var answer = JsonSerializer.Deserialize<QuestAnswer>(line);
            if (answer != null) Answers.Add(answer);
        }
    }

    public static void Add(QuestAnswer answer)
    {
        Answers.Add(answer);
        lock (FileLock)
        {
            File.AppendAllText(FilePath, JsonSerializer.Serialize(answer) + Environment.NewLine);
        }
    }

    public static IEnumerable<QuestAnswer> All() => Answers;

    public static IEnumerable<QuestAnswer> BySession(string sessionId) =>
        Answers.Where(a => a.SessionId == sessionId);
}