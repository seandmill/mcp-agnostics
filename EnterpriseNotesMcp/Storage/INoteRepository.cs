using EnterpriseNotesMcp.Models;

namespace EnterpriseNotesMcp.Storage;

/// <summary>
/// Repository interface for note persistence.
/// In production, swap this implementation for SQL Server, Cosmos DB, etc.
/// </summary>
public interface INoteRepository
{
    Task<IReadOnlyList<Note>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Note?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<Note> CreateAsync(Note note, CancellationToken cancellationToken = default);
    Task<Note> UpdateAsync(Note note, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Note>> SearchAsync(string query, HashSet<string>? tags, int limit, CancellationToken cancellationToken = default);
}
