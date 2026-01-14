using System.Text.Json;
using EnterpriseNotesMcp.Models;

namespace EnterpriseNotesMcp.Storage;

/// <summary>
/// JSON file-based implementation of INoteRepository.
/// Suitable for demos and development. Replace with SQL/CosmosDB for production.
/// </summary>
public class JsonFileNoteRepository : INoteRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<JsonFileNoteRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public JsonFileNoteRepository(IConfiguration configuration, ILogger<JsonFileNoteRepository> logger)
    {
        _filePath = configuration.GetValue<string>("Storage:FilePath") ?? "notes.json";
        _logger = logger;
    }

    public async Task<IReadOnlyList<Note>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return await ReadNotesAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Note?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var notes = await GetAllAsync(cancellationToken);
        return notes.FirstOrDefault(n => n.Id == id);
    }

    public async Task<Note> CreateAsync(Note note, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var notes = await ReadNotesAsync(cancellationToken);
            var mutableNotes = notes.ToList();
            mutableNotes.Add(note);
            await WriteNotesAsync(mutableNotes, cancellationToken);
            
            _logger.LogInformation("Created note {NoteId}", note.Id);
            return note;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Note> UpdateAsync(Note note, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var notes = await ReadNotesAsync(cancellationToken);
            var mutableNotes = notes.ToList();
            var index = mutableNotes.FindIndex(n => n.Id == note.Id);
            
            if (index < 0)
                throw new KeyNotFoundException($"Note {note.Id} not found");
            
            mutableNotes[index] = note;
            await WriteNotesAsync(mutableNotes, cancellationToken);
            
            _logger.LogInformation("Updated note {NoteId}", note.Id);
            return note;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var notes = await ReadNotesAsync(cancellationToken);
            var mutableNotes = notes.ToList();
            var removed = mutableNotes.RemoveAll(n => n.Id == id);
            
            if (removed > 0)
            {
                await WriteNotesAsync(mutableNotes, cancellationToken);
                _logger.LogInformation("Deleted note {NoteId}", id);
            }
            
            return removed > 0;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<Note>> SearchAsync(
        string query, 
        HashSet<string>? tags, 
        int limit, 
        CancellationToken cancellationToken = default)
    {
        var notes = await GetAllAsync(cancellationToken);
        
        return notes
            .Where(n => 
                (n.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 n.Body.Contains(query, StringComparison.OrdinalIgnoreCase)) &&
                (tags == null || tags.Count == 0 || tags.Overlaps(n.Tags)))
            .Take(limit)
            .ToList();
    }

    private async Task<List<Note>> ReadNotesAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
            return [];

        try
        {
            var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
            return JsonSerializer.Deserialize<List<Note>>(json) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read notes from {FilePath}, returning empty list", _filePath);
            return [];
        }
    }

    private async Task WriteNotesAsync(List<Note> notes, CancellationToken cancellationToken)
    {
        // Atomic write: temp file + rename
        var tempPath = _filePath + ".tmp";
        var json = JsonSerializer.Serialize(notes, _jsonOptions);
        await File.WriteAllTextAsync(tempPath, json, cancellationToken);
        File.Move(tempPath, _filePath, overwrite: true);
    }
}
