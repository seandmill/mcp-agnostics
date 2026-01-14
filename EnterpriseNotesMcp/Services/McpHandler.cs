using System.Text.Json;
using EnterpriseNotesMcp.Models;

namespace EnterpriseNotesMcp.Services;

/// <summary>
/// Handles MCP protocol requests and dispatches to appropriate services.
/// </summary>
public class McpHandler : IMcpHandler
{
    private readonly INoteService _noteService;
    private readonly ILogger<McpHandler> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() 
    { 
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true 
    };

    // Tool definitions
    private static readonly List<McpTool> Tools =
    [
        new McpTool
        {
            Name = "notes_create",
            Description = "Create a new note with title, body, and optional tags. Returns the created note's ID and timestamp.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    title = new { type = "string", description = "The title of the note" },
                    body = new { type = "string", description = "The main content of the note" },
                    tags = new { type = "array", items = new { type = "string" }, description = "Optional tags for categorization" }
                },
                required = new[] { "title", "body" }
            }
        },
        new McpTool
        {
            Name = "notes_search",
            Description = "Search notes by keyword query and optional tag filters. Returns matching notes ranked by relevance.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    query = new { type = "string", description = "Search query (matches title and body)" },
                    tags = new { type = "array", items = new { type = "string" }, description = "Filter by tags (OR logic)" },
                    limit = new { type = "integer", description = "Maximum results to return (default: 10)" }
                },
                required = new[] { "query" }
            }
        },
        new McpTool
        {
            Name = "notes_summarize",
            Description = "Generate a summary of a note in various styles: 'bullets', 'short', or 'detailed'.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    id = new { type = "string", description = "The note ID to summarize" },
                    style = new { type = "string", description = "Summary style", @enum = new[] { "bullets", "short", "detailed" } }
                },
                required = new[] { "id" }
            }
        }
    ];

    public McpHandler(INoteService noteService, ILogger<McpHandler> logger)
    {
        _noteService = noteService;
        _logger = logger;
    }

    public async Task<object> HandleRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Handling MCP request: {Method}", request.Method);

        try
        {
            object result = request.Method switch
            {
                "initialize" => HandleInitialize(),
                "notifications/initialized" => new { }, // No response needed, but return empty object
                "tools/list" => HandleListTools(),
                "tools/call" => await HandleCallToolAsync(request.Params, cancellationToken),
                "resources/list" => await HandleListResourcesAsync(cancellationToken),
                "resources/read" => await HandleReadResourceAsync(request.Params, cancellationToken),
                _ => throw new McpMethodNotFoundException($"Method not found: {request.Method}")
            };

            return new JsonRpcResponse
            {
                Id = request.Id,
                Result = result
            };
        }
        catch (McpMethodNotFoundException ex)
        {
            _logger.LogWarning("Method not found: {Method}", request.Method);
            return CreateErrorResponse(request.Id, JsonRpcErrorCodes.MethodNotFound, ex.Message);
        }
        catch (McpInvalidParamsException ex)
        {
            _logger.LogWarning("Invalid params: {Message}", ex.Message);
            return CreateErrorResponse(request.Id, JsonRpcErrorCodes.InvalidParams, ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("Resource not found: {Message}", ex.Message);
            return CreateErrorResponse(request.Id, JsonRpcErrorCodes.ResourceNotFound, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Internal error handling request {Method}", request.Method);
            return CreateErrorResponse(request.Id, JsonRpcErrorCodes.InternalError, "Internal server error");
        }
    }

    private static McpInitializeResult HandleInitialize()
    {
        return new McpInitializeResult
        {
            ProtocolVersion = "2024-11-05",
            Capabilities = new McpServerCapabilities
            {
                Tools = new { },
                Resources = new { },
                Logging = new { }
            },
            ServerInfo = new McpServerInfo
            {
                Name = "EnterpriseNotesMcp",
                Version = "1.0.0"
            }
        };
    }

    private static McpListToolsResult HandleListTools()
    {
        return new McpListToolsResult { Tools = Tools };
    }

    private async Task<McpCallToolResult> HandleCallToolAsync(JsonElement? paramsElement, CancellationToken cancellationToken)
    {
        if (paramsElement == null)
            throw new McpInvalidParamsException("Missing params for tools/call");

        var callParams = JsonSerializer.Deserialize<McpCallToolParams>(paramsElement.Value.GetRawText(), _jsonOptions)
            ?? throw new McpInvalidParamsException("Invalid params for tools/call");

        _logger.LogInformation("Calling tool: {ToolName}", callParams.Name);

        return callParams.Name switch
        {
            "notes_create" => await HandleNotesCreateAsync(callParams.Arguments, cancellationToken),
            "notes_search" => await HandleNotesSearchAsync(callParams.Arguments, cancellationToken),
            "notes_summarize" => await HandleNotesSummarizeAsync(callParams.Arguments, cancellationToken),
            _ => throw new McpInvalidParamsException($"Unknown tool: {callParams.Name}")
        };
    }

    private async Task<McpCallToolResult> HandleNotesCreateAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var title = args.GetProperty("title").GetString() 
            ?? throw new McpInvalidParamsException("Title is required");
        var body = args.GetProperty("body").GetString() 
            ?? throw new McpInvalidParamsException("Body is required");
        var tags = args.TryGetProperty("tags", out var tagsElement) 
            ? JsonSerializer.Deserialize<List<string>>(tagsElement.GetRawText()) 
            : null;

        var request = new CreateNoteRequest { Title = title, Body = body, Tags = tags };
        var note = await _noteService.CreateNoteAsync(request, cancellationToken: cancellationToken);

        return new McpCallToolResult
        {
            Content =
            [
                new McpContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(new
                    {
                        success = true,
                        id = note.Id,
                        createdAt = note.CreatedAt
                    }, _jsonOptions)
                }
            ]
        };
    }

    private async Task<McpCallToolResult> HandleNotesSearchAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var query = args.GetProperty("query").GetString() 
            ?? throw new McpInvalidParamsException("Query is required");
        var tags = args.TryGetProperty("tags", out var tagsElement) 
            ? JsonSerializer.Deserialize<HashSet<string>>(tagsElement.GetRawText()) 
            : null;
        var limit = args.TryGetProperty("limit", out var limitElement) && limitElement.TryGetInt32(out var lim) 
            ? lim 
            : 10;

        var request = new SearchNotesRequest { Query = query, Tags = tags, Limit = limit };
        var results = await _noteService.SearchNotesAsync(request, cancellationToken);

        return new McpCallToolResult
        {
            Content =
            [
                new McpContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(new
                    {
                        query,
                        count = results.Count,
                        results = results.Select(n => new
                        {
                            n.Id,
                            n.Title,
                            snippet = n.Body.Length > 100 ? n.Body[..100] + "..." : n.Body,
                            n.Tags,
                            n.CreatedAt
                        })
                    }, _jsonOptions)
                }
            ]
        };
    }

    private async Task<McpCallToolResult> HandleNotesSummarizeAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var id = args.GetProperty("id").GetString() 
            ?? throw new McpInvalidParamsException("ID is required");
        var style = args.TryGetProperty("style", out var styleElement) 
            ? styleElement.GetString() ?? "short" 
            : "short";

        var request = new SummarizeNoteRequest { Id = id, Style = style };
        var summary = await _noteService.SummarizeNoteAsync(request, cancellationToken);

        return new McpCallToolResult
        {
            Content = [new McpContent { Type = "text", Text = summary }]
        };
    }

    private async Task<McpListResourcesResult> HandleListResourcesAsync(CancellationToken cancellationToken)
    {
        var notes = await _noteService.GetAllNotesAsync(cancellationToken);
        
        return new McpListResourcesResult
        {
            Resources = notes.Select(n => new McpResource
            {
                Uri = $"notes://{n.Id}",
                Name = n.Title,
                MimeType = "application/json",
                Description = $"Note created {n.CreatedAt:yyyy-MM-dd}"
            }).ToList()
        };
    }

    private async Task<McpReadResourceResult> HandleReadResourceAsync(JsonElement? paramsElement, CancellationToken cancellationToken)
    {
        if (paramsElement == null)
            throw new McpInvalidParamsException("Missing params for resources/read");

        var readParams = JsonSerializer.Deserialize<McpReadResourceParams>(paramsElement.Value.GetRawText(), _jsonOptions)
            ?? throw new McpInvalidParamsException("Invalid params for resources/read");

        if (!readParams.Uri.StartsWith("notes://"))
            throw new McpInvalidParamsException($"Invalid resource URI scheme: {readParams.Uri}");

        var id = readParams.Uri.Replace("notes://", "");
        var note = await _noteService.GetNoteAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Note not found: {id}");

        return new McpReadResourceResult
        {
            Contents =
            [
                new McpResourceContent
                {
                    Uri = readParams.Uri,
                    MimeType = "application/json",
                    Text = JsonSerializer.Serialize(note, _jsonOptions)
                }
            ]
        };
    }

    private static JsonRpcErrorResponse CreateErrorResponse(object? id, int code, string message)
    {
        return new JsonRpcErrorResponse
        {
            Id = id,
            Error = new JsonRpcError { Code = code, Message = message }
        };
    }
}

// Custom exceptions for MCP error handling
public class McpMethodNotFoundException : Exception
{
    public McpMethodNotFoundException(string message) : base(message) { }
}

public class McpInvalidParamsException : Exception
{
    public McpInvalidParamsException(string message) : base(message) { }
}
