using System.Text.Json;
using System.Text.Json.Serialization;

namespace EnterpriseNotesMcp.Models;

// =============================================================================
// JSON-RPC 2.0 Base Types
// =============================================================================

/// <summary>
/// Base JSON-RPC request structure.
/// </summary>
public record JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    [JsonPropertyName("id")]
    public object? Id { get; init; }

    [JsonPropertyName("method")]
    public string Method { get; init; } = string.Empty;

    [JsonPropertyName("params")]
    public JsonElement? Params { get; init; }
}

/// <summary>
/// JSON-RPC success response.
/// </summary>
public record JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    [JsonPropertyName("id")]
    public object? Id { get; init; }

    [JsonPropertyName("result")]
    public object? Result { get; init; }
}

/// <summary>
/// JSON-RPC error response.
/// </summary>
public record JsonRpcErrorResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    [JsonPropertyName("id")]
    public object? Id { get; init; }

    [JsonPropertyName("error")]
    public required JsonRpcError Error { get; init; }
}

/// <summary>
/// JSON-RPC error object.
/// </summary>
public record JsonRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; init; }
}

/// <summary>
/// Standard JSON-RPC error codes.
/// </summary>
public static class JsonRpcErrorCodes
{
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;
    
    // MCP-specific error codes (-32000 to -32099)
    public const int ResourceNotFound = -32002;
    public const int ToolExecutionError = -32003;
}

// =============================================================================
// MCP Protocol Types
// =============================================================================

/// <summary>
/// MCP initialization result.
/// </summary>
public record McpInitializeResult
{
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; init; } = "2024-11-05";

    [JsonPropertyName("capabilities")]
    public McpServerCapabilities Capabilities { get; init; } = new();

    [JsonPropertyName("serverInfo")]
    public McpServerInfo ServerInfo { get; init; } = new();
}

public record McpServerCapabilities
{
    [JsonPropertyName("tools")]
    public object? Tools { get; init; } = new { };

    [JsonPropertyName("resources")]
    public object? Resources { get; init; } = new { };

    [JsonPropertyName("logging")]
    public object? Logging { get; init; } = new { };
}

public record McpServerInfo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "EnterpriseNotesMcp";

    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0.0";
}

// =============================================================================
// MCP Tools
// =============================================================================

public record McpTool
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("inputSchema")]
    public required object InputSchema { get; init; }
}

public record McpListToolsResult
{
    [JsonPropertyName("tools")]
    public List<McpTool> Tools { get; init; } = [];
}

public record McpCallToolParams
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("arguments")]
    public JsonElement Arguments { get; init; }
}

public record McpCallToolResult
{
    [JsonPropertyName("content")]
    public List<McpContent> Content { get; init; } = [];

    [JsonPropertyName("isError")]
    public bool IsError { get; init; }
}

public record McpContent
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "text";

    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;
}

// =============================================================================
// MCP Resources
// =============================================================================

public record McpResource
{
    [JsonPropertyName("uri")]
    public required string Uri { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("mimeType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MimeType { get; init; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }
}

public record McpListResourcesResult
{
    [JsonPropertyName("resources")]
    public List<McpResource> Resources { get; init; } = [];
}

public record McpReadResourceParams
{
    [JsonPropertyName("uri")]
    public string Uri { get; init; } = string.Empty;
}

public record McpReadResourceResult
{
    [JsonPropertyName("contents")]
    public List<McpResourceContent> Contents { get; init; } = [];
}

public record McpResourceContent
{
    [JsonPropertyName("uri")]
    public required string Uri { get; init; }

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; init; }

    [JsonPropertyName("text")]
    public required string Text { get; init; }
}
