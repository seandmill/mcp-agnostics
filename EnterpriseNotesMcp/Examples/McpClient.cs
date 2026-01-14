// =============================================================================
// Example: MCP Client for LLM Orchestrator Integration
// =============================================================================
// 
// This example demonstrates how an LLM orchestration service would call
// MCP tools over HTTP. Use this as a reference for integrating MCP servers
// into your enterprise AI workflows.
//
// Usage in an orchestrator:
// 1. LLM returns a tool call request (e.g., "call notes_search with query='quarterly report'")
// 2. Orchestrator extracts tool name and arguments
// 3. Orchestrator calls MCP server via HTTP
// 4. Orchestrator injects result back into LLM context
// 5. LLM generates final response using the tool result

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EnterpriseNotesMcp.Examples;

/// <summary>
/// Example MCP client for calling the HTTP-based MCP server.
/// This would be used by an LLM orchestration service.
/// </summary>
public class McpClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly JsonSerializerOptions _jsonOptions;
    private int _requestId;

    public McpClient(string baseUrl, string? apiKey = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        
        if (!string.IsNullOrEmpty(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        }

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    /// <summary>
    /// Initialize the MCP session.
    /// </summary>
    public async Task<InitializeResult> InitializeAsync(CancellationToken cancellationToken = default)
    {
        var request = CreateRequest("initialize", new
        {
            protocolVersion = "2024-11-05",
            capabilities = new { },
            clientInfo = new { name = "LLM-Orchestrator", version = "1.0.0" }
        });

        var response = await SendRequestAsync<InitializeResult>(request, cancellationToken);
        return response;
    }

    /// <summary>
    /// List available tools from the MCP server.
    /// </summary>
    public async Task<List<McpToolDefinition>> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        var request = CreateRequest("tools/list", new { });
        var response = await SendRequestAsync<ListToolsResponse>(request, cancellationToken);
        return response.Tools;
    }

    /// <summary>
    /// Call a tool on the MCP server.
    /// This is the main method used by orchestrators when handling LLM tool calls.
    /// </summary>
    public async Task<ToolCallResult> CallToolAsync(
        string toolName, 
        object arguments, 
        CancellationToken cancellationToken = default)
    {
        var request = CreateRequest("tools/call", new
        {
            name = toolName,
            arguments
        });

        var response = await SendRequestAsync<ToolCallResult>(request, cancellationToken);
        return response;
    }

    /// <summary>
    /// List available resources.
    /// </summary>
    public async Task<List<McpResourceDefinition>> ListResourcesAsync(CancellationToken cancellationToken = default)
    {
        var request = CreateRequest("resources/list", new { });
        var response = await SendRequestAsync<ListResourcesResponse>(request, cancellationToken);
        return response.Resources;
    }

    /// <summary>
    /// Read a specific resource.
    /// </summary>
    public async Task<ResourceReadResult> ReadResourceAsync(
        string uri, 
        CancellationToken cancellationToken = default)
    {
        var request = CreateRequest("resources/read", new { uri });
        var response = await SendRequestAsync<ResourceReadResult>(request, cancellationToken);
        return response;
    }

    private JsonRpcRequest CreateRequest(string method, object @params)
    {
        return new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = Interlocked.Increment(ref _requestId),
            Method = method,
            Params = @params
        };
    }

    private async Task<T> SendRequestAsync<T>(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"{_baseUrl}/mcp", 
            request, 
            _jsonOptions,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonRpcResponse<T>>(_jsonOptions, cancellationToken);

        if (jsonResponse?.Error != null)
        {
            throw new McpException(jsonResponse.Error.Code, jsonResponse.Error.Message);
        }

        return jsonResponse!.Result!;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

// =============================================================================
// Request/Response Models
// =============================================================================

public class JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = "";

    [JsonPropertyName("params")]
    public object? Params { get; set; }
}

public class JsonRpcResponse<T>
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("result")]
    public T? Result { get; set; }

    [JsonPropertyName("error")]
    public JsonRpcError? Error { get; set; }
}

public class JsonRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

public class InitializeResult
{
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; set; } = "";

    [JsonPropertyName("serverInfo")]
    public ServerInfo? ServerInfo { get; set; }
}

public class ServerInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";
}

public class ListToolsResponse
{
    [JsonPropertyName("tools")]
    public List<McpToolDefinition> Tools { get; set; } = [];
}

public class McpToolDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("inputSchema")]
    public JsonElement InputSchema { get; set; }
}

public class ToolCallResult
{
    [JsonPropertyName("content")]
    public List<ContentItem> Content { get; set; } = [];

    [JsonPropertyName("isError")]
    public bool IsError { get; set; }
}

public class ContentItem
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";
}

public class ListResourcesResponse
{
    [JsonPropertyName("resources")]
    public List<McpResourceDefinition> Resources { get; set; } = [];
}

public class McpResourceDefinition
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public class ResourceReadResult
{
    [JsonPropertyName("contents")]
    public List<ResourceContent> Contents { get; set; } = [];
}

public class ResourceContent
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = "";

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";
}

public class McpException : Exception
{
    public int Code { get; }

    public McpException(int code, string message) : base(message)
    {
        Code = code;
    }
}

// =============================================================================
// Example Usage in an Orchestrator
// =============================================================================

/// <summary>
/// Example showing how to integrate MCP into an LLM orchestration workflow.
/// </summary>
public static class OrchestratorExample
{
    /// <summary>
    /// Example workflow: User asks to summarize notes about "quarterly report"
    /// </summary>
    public static async Task RunExampleWorkflowAsync()
    {
        // Configuration - in production, these come from environment/config
        var mcpServerUrl = "http://localhost:8080";
        var apiKey = "orchestrator-key-67890";

        using var mcpClient = new McpClient(mcpServerUrl, apiKey);

        // Step 1: Initialize MCP session
        Console.WriteLine("Initializing MCP session...");
        var initResult = await mcpClient.InitializeAsync();
        Console.WriteLine($"Connected to: {initResult.ServerInfo?.Name} v{initResult.ServerInfo?.Version}");

        // Step 2: Get available tools (useful for building LLM tool definitions)
        Console.WriteLine("\nFetching available tools...");
        var tools = await mcpClient.ListToolsAsync();
        foreach (var tool in tools)
        {
            Console.WriteLine($"  - {tool.Name}: {tool.Description}");
        }

        // Step 3: Simulate LLM deciding to call notes_search
        // In a real orchestrator, the LLM would return this decision
        Console.WriteLine("\nLLM decides to search notes...");
        var searchResult = await mcpClient.CallToolAsync("notes_search", new
        {
            query = "quarterly report",
            limit = 5
        });

        Console.WriteLine("Search result:");
        foreach (var content in searchResult.Content)
        {
            Console.WriteLine(content.Text);
        }

        // Step 4: Create a new note (simulating LLM wanting to save something)
        Console.WriteLine("\nLLM decides to create a note...");
        var createResult = await mcpClient.CallToolAsync("notes_create", new
        {
            title = "Q4 Meeting Summary",
            body = "Discussed quarterly targets. Key points: revenue up 15%, new product launch scheduled.",
            tags = new[] { "meetings", "q4", "summary" }
        });

        Console.WriteLine("Create result:");
        foreach (var content in createResult.Content)
        {
            Console.WriteLine(content.Text);
        }

        // Step 5: List resources (notes stored in the system)
        Console.WriteLine("\nListing available resources...");
        var resources = await mcpClient.ListResourcesAsync();
        foreach (var resource in resources)
        {
            Console.WriteLine($"  - {resource.Uri}: {resource.Name}");
        }

        Console.WriteLine("\nWorkflow complete!");
    }
}
