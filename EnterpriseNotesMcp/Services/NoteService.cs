using EnterpriseNotesMcp.Models;
using EnterpriseNotesMcp.Storage;

namespace EnterpriseNotesMcp.Services;

/// <summary>
/// Implementation of note business logic.
/// </summary>
public class NoteService : INoteService
{
    private readonly INoteRepository _repository;
    private readonly ILogger<NoteService> _logger;

    public NoteService(INoteRepository repository, ILogger<NoteService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Note> CreateNoteAsync(
        CreateNoteRequest request, 
        string? userId = null, 
        CancellationToken cancellationToken = default)
    {
        var note = new Note
        {
            Id = Guid.NewGuid().ToString(),
            Title = request.Title,
            Body = request.Body,
            Tags = request.Tags ?? [],
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = userId
        };

        await _repository.CreateAsync(note, cancellationToken);
        
        _logger.LogInformation(
            "Note created: {NoteId} by user {UserId}", 
            note.Id, 
            userId ?? "anonymous");
        
        return note;
    }

    public async Task<IReadOnlyList<Note>> SearchNotesAsync(
        SearchNotesRequest request, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Searching notes: query={Query}, tags={Tags}, limit={Limit}",
            request.Query,
            request.Tags != null ? string.Join(",", request.Tags) : "none",
            request.Limit);

        return await _repository.SearchAsync(
            request.Query, 
            request.Tags, 
            request.Limit, 
            cancellationToken);
    }

    public async Task<string> SummarizeNoteAsync(
        SummarizeNoteRequest request, 
        CancellationToken cancellationToken = default)
    {
        var note = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Note {request.Id} not found");

        return request.Style.ToLowerInvariant() switch
        {
            "bullets" => GenerateBulletSummary(note),
            "short" => GenerateShortSummary(note),
            "detailed" => GenerateDetailedSummary(note),
            _ => GenerateShortSummary(note)
        };
    }

    public async Task<Note?> GetNoteAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<Note>> GetAllNotesAsync(CancellationToken cancellationToken = default)
    {
        return await _repository.GetAllAsync(cancellationToken);
    }

    private static string GenerateBulletSummary(Note note)
    {
        var sentences = note.Body
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => $"- {s.Trim()}.");
        
        return string.Join("\n", sentences);
    }

    private static string GenerateShortSummary(Note note)
    {
        return note.Body.Length > 100 
            ? note.Body[..100] + "..." 
            : note.Body;
    }

    private static string GenerateDetailedSummary(Note note)
    {
        var tagList = note.Tags.Count > 0 
            ? string.Join(", ", note.Tags) 
            : "none";
        
        return $"""
            Title: {note.Title}
            Created: {note.CreatedAt:yyyy-MM-dd HH:mm:ss UTC}
            Tags: {tagList}
            
            Content:
            {note.Body}
            """;
    }
}
