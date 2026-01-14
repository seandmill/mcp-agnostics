using System.Text.Json.Serialization;

namespace EnterpriseNotesMcp.Models;

/// <summary>
/// Domain model for a note.
/// </summary>
public record Note
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("body")]
    public required string Body { get; init; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; init; } = [];

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset? UpdatedAt { get; init; }

    [JsonPropertyName("createdBy")]
    public string? CreatedBy { get; init; }
}

/// <summary>
/// DTO for creating a note.
/// </summary>
public record CreateNoteRequest
{
    public required string Title { get; init; }
    public required string Body { get; init; }
    public List<string>? Tags { get; init; }
}

/// <summary>
/// DTO for searching notes.
/// </summary>
public record SearchNotesRequest
{
    public required string Query { get; init; }
    public HashSet<string>? Tags { get; init; }
    public int Limit { get; init; } = 10;
}

/// <summary>
/// DTO for summarizing a note.
/// </summary>
public record SummarizeNoteRequest
{
    public required string Id { get; init; }
    public string Style { get; init; } = "short";
}
