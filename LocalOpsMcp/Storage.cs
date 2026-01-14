using System.Text.Json;

namespace LocalOpsMcp;

public class Storage
{
    private const string FileName = "notes.json";
    private readonly object _lock = new();

    public List<Note> GetAll()
    {
        lock (_lock)
        {
            if (!File.Exists(FileName)) return [];
            try
            {
                var json = File.ReadAllText(FileName);
                return JsonSerializer.Deserialize<List<Note>>(json) ?? [];
            }
            catch
            {
                return [];
            }
        }
    }

    public void Save(Note note)
    {
        lock (_lock)
        {
            var notes = GetAll();
            notes.Add(note);
            File.WriteAllText(FileName, JsonSerializer.Serialize(notes, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
