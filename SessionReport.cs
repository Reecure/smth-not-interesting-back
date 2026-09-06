using System.Text;
using System.Text.Json;

namespace WebApplication1;

public static class SessionReport
{
    private const int Width = 62;

    public static void Print(string sessionId, ILogger logger)
    {
        var answers = QuestAnswerStore.GetAnswers(sessionId);
        var sb = new StringBuilder();

        sb.AppendLine();
        sb.AppendLine("+" + new string('=', Width) + "+");
        Bar(sb, "РЕЗУЛЬТАТЫ СЕССИИ");
        Bar(sb, sessionId);
        Bar(sb, $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        Bar(sb, $"пройдено квестов: {answers.Count} из {QuestAnswerStore.QuestIds.Length}");
        sb.AppendLine("+" + new string('-', Width) + "+");

        AppendPlanes(sb, answers);
        AppendPhrase(sb, answers);
        AppendDucks(sb, answers);
        AppendLetter(sb, answers);
        AppendChat(sb, answers);
        AppendCats(sb, answers);

        sb.AppendLine("+" + new string('=', Width) + "+");

        logger.LogWarning("{Report}", sb.ToString());
    }

    private static JsonElement? Get(Dictionary<string, JsonElement> a, string quest) =>
        a.TryGetValue(quest, out var el) ? el : null;

    private static string? Str(JsonElement? el, string prop)
    {
        if (el is null) return null;
        if (el.Value.ValueKind != JsonValueKind.Object) return null;
        if (!el.Value.TryGetProperty(prop, out var v)) return null;
        return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    private static int? Num(JsonElement? el, string prop)
    {
        if (el is null) return null;
        if (el.Value.ValueKind != JsonValueKind.Object) return null;
        if (!el.Value.TryGetProperty(prop, out var v)) return null;
        if (v.ValueKind != JsonValueKind.Number) return null;
        return v.TryGetInt32(out var n) ? n : (int)v.GetDouble();
    }

    private static bool? Flag(JsonElement? el, string prop)
    {
        if (el is null) return null;
        if (el.Value.ValueKind != JsonValueKind.Object) return null;
        if (!el.Value.TryGetProperty(prop, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static string[] Arr(JsonElement? el, string prop)
    {
        if (el is null) return Array.Empty<string>();
        if (el.Value.ValueKind != JsonValueKind.Object) return Array.Empty<string>();
        if (!el.Value.TryGetProperty(prop, out var v)) return Array.Empty<string>();
        if (v.ValueKind != JsonValueKind.Array) return Array.Empty<string>();

        return v.EnumerateArray()
            .Select(x => x.ValueKind switch
            {
                JsonValueKind.String => x.GetString() ?? "",
                JsonValueKind.Object => x.TryGetProperty("label", out var l)
                    ? l.GetString() ?? x.ToString()
                    : x.ToString(),
                _ => x.ToString()
            })
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();
    }

    private static void Bar(StringBuilder sb, string text)
    {
        foreach (var chunk in Wrap(text, Width - 4))
            sb.AppendLine($"|  {chunk.PadRight(Width - 3)}|");
    }

    private static void Section(StringBuilder sb, string title)
    {
        sb.AppendLine("|" + new string(' ', Width) + "|");
        var line = $"[ {title} ]";
        sb.AppendLine($"|  {line.PadRight(Width - 3)}|");
    }

    private static void Line(StringBuilder sb, string text)
    {
        foreach (var chunk in Wrap(text, Width - 7))
            sb.AppendLine($"|     {chunk.PadRight(Width - 6)}|");
    }

    private static void Blank(StringBuilder sb)
    {
        sb.AppendLine("|" + new string(' ', Width) + "|");
    }

    private static IEnumerable<string> Wrap(string text, int width)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield return "";
            yield break;
        }

        var words = text.Replace("\r", "").Replace("\n", " ").Split(' ');
        var line = new StringBuilder();

        foreach (var w in words)
        {
            var word = w;

            while (word.Length > width)
            {
                if (line.Length > 0)
                {
                    yield return line.ToString();
                    line.Clear();
                }
                yield return word[..width];
                word = word[width..];
            }

            if (line.Length + word.Length + 1 > width && line.Length > 0)
            {
                yield return line.ToString();
                line.Clear();
            }

            if (line.Length > 0) line.Append(' ');
            line.Append(word);
        }

        if (line.Length > 0) yield return line.ToString();
    }

    private static void AppendPlanes(StringBuilder sb, Dictionary<string, JsonElement> a)
    {
        Section(sb, "САМОЛЁТИКИ");
        var tg = Get(a, "telegram");
        var hits = Num(tg, "planesHit");

        if (hits is null)
        {
            Line(sb, "квест не пройден");
            return;
        }

        Line(sb, $"сбито в башню: {hits}");
        Line(sb, hits switch
        {
            0 => "ни одного. дисциплина.",
            < 5 => "немного, но со вкусом",
            < 15 => "методично",
            _ => "это уже вандализм"
        });
    }

    private static void AppendPhrase(StringBuilder sb, Dictionary<string, JsonElement> a)
    {
        Section(sb, "ФРАЗА");
        var tg = Get(a, "telegram");
        var phrase = Str(tg, "phrase");
        var tail = Str(tg, "tail");

        if (string.IsNullOrWhiteSpace(phrase))
        {
            Line(sb, "не собрана");
            return;
        }

        Line(sb, phrase);
        if (!string.IsNullOrWhiteSpace(tail))
        {
            Blank(sb);
            Line(sb, $"дописал от себя: {tail}");
        }
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
        Line(sb, $"заменено слов: {replaced} из {total}");

        var full = Str(letter, "fullText");
        if (!string.IsNullOrWhiteSpace(full))
        {
            Blank(sb);
            Line(sb, full);
        }

        if (letter.Value.ValueKind == JsonValueKind.Object &&
            letter.Value.TryGetProperty("segments", out var segs) &&
            segs.ValueKind == JsonValueKind.Array)
        {
            var changed = segs.EnumerateArray()
                .Where(s =>
                    s.ValueKind == JsonValueKind.Object &&
                    s.TryGetProperty("type", out var t) &&
                    t.GetString() == "slot" &&
                    s.TryGetProperty("replaced", out var r) &&
                    r.ValueKind == JsonValueKind.True)
                .ToList();

            if (changed.Count > 0)
            {
                Blank(sb);
                Line(sb, "подстановки:");
                foreach (var s in changed)
                {
                    var orig = s.TryGetProperty("original", out var o) ? o.GetString() : "?";
                    var user = s.TryGetProperty("user", out var u) ? u.GetString() : "?";
                    Line(sb, $"  {orig}  ->  {user}");
                }
            }
        }

        var kept = Arr(letter, "kept");
        if (kept.Length > 0)
        {
            Blank(sb);
            Line(sb, $"оставил как было: {string.Join(", ", kept)}");
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
        if (troll.Length > 0) Line(sb, $"кнопки в ожидании: {string.Join(", ", troll)}");

        var dead = Num(chat, "deadClicks");
        if (dead is > 0) Line(sb, $"тыкал в мёртвые кнопки: {dead}");

        var watched = Flag(chat, "watched");
        if (watched == true) Line(sb, "досмотрел до конца");
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

        var cats = Arr(story, "cats");
        var score = Num(story, "score");
        var coins = Num(story, "coinsLeft");
        var won = Num(story, "gamesWon");
        var played = Num(story, "gamesPlayed");
        var spins = Num(story, "spins");
        var perfect = Flag(story, "perfect");

        Blank(sb);
        if (cats.Length > 0) Line(sb, $"коты: {string.Join(", ", cats)}");
        Line(sb, $"счёт: {score} · монет осталось: {coins}");
        Line(sb, $"игр: {won} из {played} · спинов: {spins}");
        if (perfect == true) Line(sb, "идеальный расклад");
    }
}