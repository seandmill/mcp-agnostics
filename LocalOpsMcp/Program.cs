using System.Text.Json;
using LocalOpsMcp;

// Essential: Ensure stdout is unbuffered for MCP protocol
Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });

var storage = new Storage();
var handlers = new Handlers(storage);
var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

while (true)
{
    var line = await Console.In.ReadLineAsync();
    if (line == null) break;

    try
    {
        var msg = JsonSerializer.Deserialize<JsonRpcMessage>(line, options);
        if (msg == null) continue;

        if (msg.Method == "initialize")
        {
            var req = JsonSerializer.Deserialize<JsonRpcRequest>(line, options);
            var res = new InitializeResult("2024-11-05",
                new ServerCapabilities(
                    new { },
                    new { }
                ),
                new ServerInfo("LocalOpsMcp", "1.0"));
            Reply(msg.Id, res);
        }
        else if (msg.Method == "notifications/initialized")
        {
            // No response needed
        }
        else if (msg.Method == "tools/list")
        {
            var tools = new List<Tool>
            {
                new("notes_create", "Create a note", new { type = "object", properties = new { title = new { type = "string" }, body = new { type = "string" }, tags = new { type = "array", items = new { type = "string" } } }, required = new[] { "title", "body" } }),
                new("notes_search", "Search notes", new { type = "object", properties = new { query = new { type = "string" }, tags = new { type = "array", items = new { type = "string" } }, limit = new { type = "integer" } }, required = new[] { "query" } }),
                new("notes_summarize", "Summarize a note", new { type = "object", properties = new { id = new { type = "string" }, style = new { type = "string", @enum = new[] { "bullets", "short", "detailed" } } }, required = new[] { "id" } })
            };
            Reply(msg.Id, new ListToolsResult(tools));
        }
        else if (msg.Method == "tools/call")
        {
            var req = JsonSerializer.Deserialize<JsonRpcRequest>(line, options);
            if (req?.Params == null) throw new Exception("Invalid params");

            var callParams = JsonSerializer.Deserialize<CallToolParams>(req.Params.Value.GetRawText(), options);

            CallToolResult result = callParams?.Name switch
            {
                "notes_create" => handlers.CreateNote(callParams.Arguments),
                "notes_search" => handlers.SearchNotes(callParams.Arguments),
                "notes_summarize" => handlers.SummarizeNote(callParams.Arguments),
                _ => throw new Exception($"Unknown tool {callParams?.Name}")
            };
            Reply(msg.Id, result);
        }
        else if (msg.Method == "resources/list")
        {
            Reply(msg.Id, handlers.ListResources());
        }
        else if (msg.Method == "resources/read")
        {
            var req = JsonSerializer.Deserialize<JsonRpcRequest>(line, options);
            if (req?.Params == null) throw new Exception("Invalid params");
            var readParams = JsonSerializer.Deserialize<ReadResourceParams>(req.Params.Value.GetRawText(), options);
            Reply(msg.Id, handlers.ReadResource(readParams!.Uri));
        }
        else
        {
            // Ignore unknown methods or return error if strictly required
        }
    }
    catch (Exception ex)
    {
        // In a real server we would return a proper JSON-RPC error
        // For this minimal example we just log to stderr so we don't break the JSON stream
        Console.Error.WriteLine($"Error: {ex.Message}");
    }
}

void Reply(object? id, object result)
{
    if (id == null) return;
    var response = new JsonRpcResponse("2.0", id, result);
    Console.WriteLine(JsonSerializer.Serialize(response, options));
    Console.Out.Flush(); // Critical: flush immediately so the client doesn't hang
}
