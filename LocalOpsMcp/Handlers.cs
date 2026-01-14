using System.Text.Json;

namespace LocalOpsMcp;

public class Handlers(Storage storage)
{
    public CallToolResult CreateNote(JsonElement args)
    {
        var title = args.GetProperty("title").GetString() ?? throw new ArgumentException("Title required");
        var body = args.GetProperty("body").GetString() ?? throw new ArgumentException("Body required");
        var tags = args.TryGetProperty("tags", out var t) ? t.Deserialize<List<string>>() ?? [] : [];

        var note = new Note(Guid.NewGuid().ToString(), title, body, tags, DateTimeOffset.UtcNow);
        storage.Save(note);

        return new CallToolResult(
        [
            new Content("text", $"Created note {note.Id} at {note.CreatedAt}")
        ]);
    }

    public CallToolResult SearchNotes(JsonElement args)
    {
        var query = args.GetProperty("query").GetString() ?? "";
        var tags = args.TryGetProperty("tags", out var t) ? t.Deserialize<HashSet<string>>() : null;
        var limit = args.TryGetProperty("limit", out var l) && l.TryGetInt32(out var lim) ? lim : 10;

        var notes = storage.GetAll();
        var matches = notes
            .Where(n => (n.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                         n.Body.Contains(query, StringComparison.OrdinalIgnoreCase)) &&
                        (tags == null || tags.Overlaps(n.Tags)))
            .Take(limit)
            .ToList();

        var text = JsonSerializer.Serialize(matches, new JsonSerializerOptions { WriteIndented = true });
        return new CallToolResult([new Content("text", text)]);
    }

    public CallToolResult SummarizeNote(JsonElement args)
    {
        var id = args.GetProperty("id").GetString() ?? throw new ArgumentException("ID required");
        var style = args.TryGetProperty("style", out var s) ? s.GetString() : "short";

        var note = storage.GetAll().FirstOrDefault(n => n.Id == id) ?? throw new Exception("Note not found");

        string summary = style switch
        {
            "bullets" => string.Join("\n", note.Body.Split('.').Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => $"- {s.Trim()}.")),
            "short" => note.Body.Length > 100 ? note.Body[..100] + "..." : note.Body,
            "detailed" => $"Detailed Summary of '{note.Title}':\n{note.Body}\nTags: {string.Join(", ", note.Tags)}",
            _ => note.Body
        };

        return new CallToolResult([new Content("text", summary)]);
    }

    public ListResourcesResult ListResources()
    {
        var notes = storage.GetAll();
        return new ListResourcesResult(notes.Select(n => new Resource($"notes://{n.Id}", n.Title, "application/json", "A stored note")).ToList());
    }

    public ReadResourceResult ReadResource(string uri)
    {
        var id = uri.Replace("notes://", "");
        var note = storage.GetAll().FirstOrDefault(n => n.Id == id) ?? throw new Exception("Note not found");
        return new ReadResourceResult([new ResourceContent(uri, "application/json", JsonSerializer.Serialize(note))]);
    }
}
