using System.Text.Json;
using System.Text.Json.Serialization;

namespace LocalOpsMcp;

// JSON-RPC Basics
public record JsonRpcMessage(
    [property: JsonPropertyName("jsonrpc")] string JsonRpc,
    [property: JsonPropertyName("id")] object? Id,
    [property: JsonPropertyName("method")] string? Method
);

public record JsonRpcRequest(
    string JsonRpc,
    object? Id,
    string Method,
    [property: JsonPropertyName("params")] JsonElement? Params
) : JsonRpcMessage(JsonRpc, Id, Method);

public record JsonRpcResponse(
    string JsonRpc,
    object? Id,
    [property: JsonPropertyName("result")] object? Result,
    [property: JsonPropertyName("error")] JsonRpcError? Error
) : JsonRpcMessage(JsonRpc, Id, null);

public record JsonRpcError(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("data")] object? Data = null
);

// MCP Initialization
public record InitializeParams(
    [property: JsonPropertyName("protocolVersion")] string ProtocolVersion,
    [property: JsonPropertyName("capabilities")] object Capabilities,
    [property: JsonPropertyName("clientInfo")] ClientInfo ClientInfo
);
public record ClientInfo(string Name, string Version);
public record InitializeResult(
    [property: JsonPropertyName("protocolVersion")] string ProtocolVersion,
    [property: JsonPropertyName("capabilities")] ServerCapabilities Capabilities,
    [property: JsonPropertyName("serverInfo")] ServerInfo ServerInfo
);
public record ServerCapabilities(
    [property: JsonPropertyName("tools")] object? Tools = null,
    [property: JsonPropertyName("resources")] object? Resources = null
);
public record ServerInfo(string Name, string Version);

// MCP Tools
public record Tool(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("inputSchema")] object InputSchema
);
public record ListToolsResult(
    [property: JsonPropertyName("tools")] List<Tool> Tools
);
public record CallToolParams(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("arguments")] JsonElement Arguments
);
public record CallToolResult(
    [property: JsonPropertyName("content")] List<Content> Content,
    [property: JsonPropertyName("isError")] bool IsError = false
);
public record Content(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] string Text
);

// MCP Resources
public record Resource(
    [property: JsonPropertyName("uri")] string Uri,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("mimeType")] string? MimeType = null,
    [property: JsonPropertyName("description")] string? Description = null
);
public record ListResourcesResult(
    [property: JsonPropertyName("resources")] List<Resource> Resources
);
public record ReadResourceParams(
    [property: JsonPropertyName("uri")] string Uri
);
public record ReadResourceResult(
    [property: JsonPropertyName("contents")] List<ResourceContent> Contents
);
public record ResourceContent(
    [property: JsonPropertyName("uri")] string Uri,
    [property: JsonPropertyName("mimeType")] string? MimeType,
    [property: JsonPropertyName("text")] string Text
);

// Note Models
public record Note(
    string Id,
    string Title,
    string Body,
    List<string> Tags,
    DateTimeOffset CreatedAt
);
