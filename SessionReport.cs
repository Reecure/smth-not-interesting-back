using System.Text;
using System.Text.Json;

namespace WebApplication1;

public static class SessionReport
{
    public static void Print(string sessionId, ILogger logger)
    {
        var answers = QuestAnswerStore.GetAnswers(sessionId);
        var sb = new StringBuilder();

        sb.AppendLine();
        sb.AppendLine("╔══════════════════════════════════════════════════════════");
        sb.AppendLine($"║  СЕССИЯ ЗАВЕРШЕНА  {sessionId}");
        sb.AppendLine($"║  {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine("╠══════════════════════════════════════════════════════════");

        AppendPlanes(sb, answers);
        AppendPhrase(sb, answers);
        AppendDucks(sb, answers);
        AppendLetter(sb, answers);
        AppendChat(sb, answers);
        AppendCats(sb, answers);

        sb.AppendLine("╚══════════════════════════════════════════════════════════");

        logger.LogInformation("{Report}", sb.ToString());
    }

    private static JsonElement? Get(Dictionary<string, JsonElement> a, string quest) =>
        a.TryGetValue(quest, out var el) ? el : null;

    private static string? Str(JsonElement? el, string prop)
    {
        if (el is null) return null;
        if (!el.Value.TryGetProperty(prop, out var v)) return null;
        return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    private static int? Num(JsonElement? el, string prop)
    {
        if (el is null) return null;
        if (!el.Value.TryGetProperty(prop, out var v)) return null;
        return v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;
    }

    private static string[] Arr(JsonElement? el, string prop)
    {
        if (el is null) return Array.Empty<string>();
        if (!el.Value.TryGetProperty(prop, out var v)) return Array.Empty<string>();
        if (v.ValueKind != JsonValueKind.Array) return Array.Empty<string>();

        return v.EnumerateArray()
            .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() ?? "" : x.ToString())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();
    }

    private static void Section(StringBuilder sb, string title)
    {
        sb.AppendLine($"║");
        sb.AppendLine($"║  ── {title} ──");
    }

    private static void Line(StringBuilder sb, string text)
    {
        foreach (var chunk in Wrap(text, 54))
            sb.AppendLine($"║     {chunk}");
    }

    private static IEnumerable<string> Wrap(string text, int width)
    {
        var words = text.Split(' ');
        var line = new StringBuilder();

        foreach (var w in words)
        {
            if (line.Length + w.Length + 1 > width && line.Length > 0)
            {
                yield return line.ToString();
                line.Clear();
            }
            if (line.Length > 0) line.Append(' ');
            line.Append(w);
        }

        if (line.Length > 0) yield return line.ToString();
    }

    private static void AppendPlanes(StringBuilder sb, Dictionary<string, JsonElement> a)
    {
        Section(sb, "САМОЛЁТИКИ");
        var tg = Get(a, "telegram");
        var hits = Num(tg, "planesHit");
        Line(sb, hits is null ? "квест не пройден" : $"сбито: {hits}");
    }

    private static void AppendPhrase(StringBuilder sb, Dictionary<string, JsonElement> a)
    {
        Section(sb, "ФРАЗА");
        var tg = Get(a, "telegram");
        var phrase = Str(tg, "phrase");
        var tail = Str(tg, "tail");

        if (phrase is null)
        {
            Line(sb, "не собрана");
            return;
        }

        Line(sb, $"целиком: {phrase}");
        if (!string.IsNullOrWhiteSpace(tail)) Line(sb, $"хвост:   {tail}");
    }

    private static void AppendDucks(StringBuilder sb, Dictionary<string, JsonElement> a)
    {
        Section(sb, "УТОЧКИ");
        var shake = Get(a, "shake");
        var placed = Num(shake, "ducksPlaced");
        var peak = Num(shake, "peak");

        if (placed is null)
        {
            Line(sb, "квест не пройден");
            return;
        }

        Line(sb, $"спасено: {placed}");
        if (peak is not null) Line(sb, $"пик тряски: {peak}");
    }

    private static void AppendLetter(StringBuilder sb, Dictionary<string, JsonElement> a)
    {
        Section(sb, "ПИСЬМО");
        var letter = Get(a, "letter");

        if (letter is null)
        {
            Line(sb, "квест не пройден");
            return;
        }

        var replaced = Num(letter, "replaced");
        var total = Num(letter, "total");
        Line(sb, $"заменено слов: {replaced}/{total}");

        var full = Str(letter, "fullText");
        if (!string.IsNullOrWhiteSpace(full))
        {
            sb.AppendLine("║");
            Line(sb, full);
        }

        if (letter.Value.TryGetProperty("segments", out var segs) &&
            segs.ValueKind == JsonValueKind.Array)
        {
            var changed = segs.EnumerateArray()
                .Where(s =>
                    s.TryGetProperty("type", out var t) &&
                    t.GetString() == "slot" &&
                    s.TryGetProperty("replaced", out var r) &&
                    r.ValueKind == JsonValueKind.True)
                .ToList();

            if (changed.Count > 0)
            {
                sb.AppendLine("║");
                Line(sb, "подстановки:");
                foreach (var s in changed)
                {
                    var orig = s.TryGetProperty("original", out var o) ? o.GetString() : "?";
                    var user = s.TryGetProperty("user", out var u) ? u.GetString() : "?";
                    Line(sb, $"  «{orig}» → «{user}»");
                }
            }
        }
    }

    private static void AppendChat(StringBuilder sb, Dictionary<string, JsonElement> a)
    {
        Section(sb, "СПЛЕТНЯ");
        var chat = Get(a, "chat");

        if (chat is null)
        {
            Line(sb, "квест не пройден");
            return;
        }

        var replies = Arr(chat, "replies");
        if (replies.Length > 0) Line(sb, $"ответы: {string.Join(" | ", replies)}");

        var reactions = Arr(chat, "reactions");
        if (reactions.Length > 0) Line(sb, $"реакции: {string.Join(" ", reactions)}");

        var troll = Arr(chat, "trollActions");
        if (troll.Length > 0) Line(sb, $"кнопки: {string.Join(", ", troll)}");

        var dead = Num(chat, "deadClicks");
        if (dead is > 0) Line(sb, $"тыкал в мёртвые кнопки: {dead}");
    }

    private static void AppendCats(StringBuilder sb, Dictionary<string, JsonElement> a)
    {
        Section(sb, "КОТЫ");
        var story = Get(a, "story");

        if (story is null)
        {
            Line(sb, "квест не пройден");
            return;
        }

        var text = Str(story, "story");
        if (!string.IsNullOrWhiteSpace(text)) Line(sb, text);

        var score = Num(story, "score");
        var coins = Num(story, "coinsLeft");
        var won = Num(story, "gamesWon");
        var played = Num(story, "gamesPlayed");

        sb.AppendLine("║");
        Line(sb, $"счёт: {score} · монет: {coins} · игр: {won}/{played}");
    }
}