using System.Collections.Concurrent;
using System.Text.Json;

namespace WebApplication1;

public static class QuestAnswerStore
{
    public static readonly string[] QuestIds =
        { "letter", "telegram", "story", "shake", "chat", "loading" };

    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, QuestAnswer>> Sessions = new();
    private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "quest-answers.jsonl");
    private static readonly object FileLock = new();

    static QuestAnswerStore()
    {
        if (!File.Exists(FilePath)) return;

        foreach (var line in File.ReadAllLines(FilePath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var answer = JsonSerializer.Deserialize<QuestAnswer>(line);
                if (answer is null || string.IsNullOrWhiteSpace(answer.SessionId)) continue;
                Apply(answer);
            }
            catch (JsonException)
            {
            }
        }
    }

    private static void Apply(QuestAnswer answer)
    {
        var bucket = Sessions.GetOrAdd(answer.SessionId, _ => new ConcurrentDictionary<string, QuestAnswer>());
        bucket[answer.QuestId] = answer;
    }

    public static QuestAnswer Add(string sessionId, string questId, JsonElement payload)
    {
        var answer = new QuestAnswer(
            sessionId,
            questId,
            payload.Clone(),
            DateTime.UtcNow.ToString("O")
        );

        Apply(answer);

        lock (FileLock)
        {
            File.AppendAllText(FilePath, JsonSerializer.Serialize(answer) + Environment.NewLine);
        }

        return answer;
    }

    public static SessionProgress GetProgress(string sessionId)
    {
        var answers = new Dictionary<string, JsonElement>();

        if (Sessions.TryGetValue(sessionId, out var bucket))
        {
            foreach (var kv in bucket)
                answers[kv.Key] = kv.Value.Payload;
        }

        return new SessionProgress(
            sessionId,
            answers,
            answers.Keys.ToArray(),
            QuestIds.Length
        );
    }

    public static bool Reset(string sessionId, string? questId)
    {
        if (!Sessions.TryGetValue(sessionId, out var bucket)) return false;

        if (questId is null)
        {
            Sessions.TryRemove(sessionId, out _);
            return true;
        }

        return bucket.TryRemove(questId, out _);
    }

    public static IEnumerable<QuestAnswer> All() =>
        Sessions.Values.SelectMany(b => b.Values);
}