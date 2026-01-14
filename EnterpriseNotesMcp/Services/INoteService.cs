using EnterpriseNotesMcp.Models;

namespace EnterpriseNotesMcp.Services;

/// <summary>
/// Business logic layer for note operations.
/// </summary>
public interface INoteService
{
    Task<Note> CreateNoteAsync(CreateNoteRequest request, string? userId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Note>> SearchNotesAsync(SearchNotesRequest request, CancellationToken cancellationToken = default);
    Task<string> SummarizeNoteAsync(SummarizeNoteRequest request, CancellationToken cancellationToken = default);
    Task<Note?> GetNoteAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Note>> GetAllNotesAsync(CancellationToken cancellationToken = default);
}
