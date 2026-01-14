# Enterprise Notes MCP Server

An enterprise-grade MCP (Model Context Protocol) server for note management, designed for deployment in production LLM orchestration workflows.

## Key Features

| Feature | Description |
|---------|-------------|
| **HTTP Transport** | REST-based MCP over HTTP (not stdio) for server-to-server communication |
| **Enterprise Ready** | Health checks, OpenAPI docs, structured logging, rate limiting |
| **Containerized** | Docker and Kubernetes deployment manifests included |
| **Authentication** | API key authentication with extensible middleware |
| **Observability** | Serilog structured logging, Prometheus-ready metrics endpoint |

## Architecture

```
                                    Enterprise Network
    +------------------------------------------------------------------+
    |                                                                  |
    |  +------------------+     HTTP/JSON-RPC      +-----------------+ |
    |  | LLM Orchestrator | ------------------->  | EnterpriseNotes | |
    |  |                  | <-------------------  |    MCP Server   | |
    |  +--------+---------+                       +--------+--------+ |
    |           |                                          |          |
    |           v                                          v          |
    |  +------------------+                       +-----------------+ |
    |  |  Model Endpoint  |                       |  Persistent     | |
    |  | (Azure OpenAI,   |                       |  Storage        | |
    |  |  Claude, etc.)   |                       | (JSON/SQL/Cosmos)|
    |  +------------------+                       +-----------------+ |
    +------------------------------------------------------------------+
```

## Quick Start

### Local Development

```bash
cd EnterpriseNotesMcp
dotnet restore
dotnet run
```

Server starts at `http://localhost:5000`. Visit `/swagger` for API documentation.

### Docker

```bash
# Build and run
docker-compose up -d

# Check health
curl http://localhost:8080/health
```

### Kubernetes

```bash
# Deploy to cluster
kubectl create namespace ai-platform
kubectl apply -f k8s/

# Verify deployment
kubectl get pods -n ai-platform -l app=enterprise-notes-mcp
```

## API Reference

### MCP Endpoint

All MCP requests go to `POST /mcp` using JSON-RPC 2.0 format.

#### Initialize Session

```bash
curl -X POST http://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "initialize",
    "params": {
      "protocolVersion": "2024-11-05",
      "capabilities": {},
      "clientInfo": {"name": "my-orchestrator", "version": "1.0"}
    }
  }'
```

#### List Tools

```bash
curl -X POST http://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 2,
    "method": "tools/list",
    "params": {}
  }'
```

#### Call Tool - Create Note

```bash
curl -X POST http://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 3,
    "method": "tools/call",
    "params": {
      "name": "notes_create",
      "arguments": {
        "title": "Q4 Planning",
        "body": "Key initiatives: AI platform rollout, cost optimization",
        "tags": ["planning", "q4"]
      }
    }
  }'
```

#### Call Tool - Search Notes

```bash
curl -X POST http://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 4,
    "method": "tools/call",
    "params": {
      "name": "notes_search",
      "arguments": {
        "query": "planning",
        "limit": 5
      }
    }
  }'
```

#### Call Tool - Summarize Note

```bash
curl -X POST http://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 5,
    "method": "tools/call",
    "params": {
      "name": "notes_summarize",
      "arguments": {
        "id": "<note-id>",
        "style": "bullets"
      }
    }
  }'
```

### Available Tools

| Tool | Description | Required Params |
|------|-------------|-----------------|
| `notes_create` | Create a new note | `title`, `body` |
| `notes_search` | Search notes by keyword | `query` |
| `notes_summarize` | Generate note summary | `id` |

### Health Endpoints

| Endpoint | Purpose |
|----------|---------|
| `/health` | Full health check with details |
| `/health/live` | Liveness probe (is the app running?) |
| `/health/ready` | Readiness probe (can it serve traffic?) |

## Configuration

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `ASPNETCORE_ENVIRONMENT` | Development | Runtime environment |
| `ASPNETCORE_URLS` | http://localhost:5000 | Binding URLs |
| `Storage__FilePath` | notes.json | Data file location |
| `Authentication__Enabled` | false | Enable API key auth |
| `RateLimiting__PermitLimit` | 100 | Requests per window |
| `RateLimiting__WindowSeconds` | 60 | Rate limit window |

### Authentication

Enable authentication in production:

```json
{
  "Authentication": {
    "Enabled": true,
    "ApiKeys": [
      {
        "Key": "your-secure-api-key",
        "UserId": "orchestrator-service",
        "Role": "service"
      }
    ]
  }
}
```

Then include the API key in requests:

```bash
curl -X POST http://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -H "X-API-Key: your-secure-api-key" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'
```

## Integration with LLM Orchestrators

### Example: Azure OpenAI Function Calling

```csharp
// 1. Get tool definitions from MCP server
var tools = await mcpClient.ListToolsAsync();

// 2. Convert to Azure OpenAI function definitions
var functions = tools.Select(t => new ChatCompletionsFunctionToolDefinition
{
    Name = t.Name,
    Description = t.Description,
    Parameters = BinaryData.FromString(t.InputSchema.GetRawText())
});

// 3. Call Azure OpenAI with functions
var chatOptions = new ChatCompletionsOptions
{
    Tools = functions.ToList(),
    // ... other options
};
var response = await openAiClient.GetChatCompletionsAsync(chatOptions);

// 4. Handle tool calls
foreach (var toolCall in response.Value.Choices[0].Message.ToolCalls)
{
    var result = await mcpClient.CallToolAsync(
        toolCall.Name,
        JsonSerializer.Deserialize<object>(toolCall.Arguments)
    );
    
    // 5. Inject result back into conversation
    // ...
}
```

See `Examples/McpClient.cs` for a complete client implementation.

## Project Structure

```
EnterpriseNotesMcp/
+-- Controllers/
|   +-- McpController.cs       # HTTP endpoints
+-- Examples/
|   +-- McpClient.cs           # Client example for orchestrators
+-- k8s/
|   +-- deployment.yaml        # Kubernetes manifests
+-- Middleware/
|   +-- McpAuthMiddleware.cs   # Authentication
+-- Models/
|   +-- McpProtocol.cs         # MCP/JSON-RPC types
|   +-- Note.cs                # Domain model
+-- Services/
|   +-- IMcpHandler.cs         # MCP handler interface
|   +-- McpHandler.cs          # MCP protocol implementation
|   +-- INoteService.cs        # Business logic interface
|   +-- NoteService.cs         # Business logic implementation
+-- Storage/
|   +-- INoteRepository.cs     # Repository interface
|   +-- JsonFileNoteRepository.cs  # JSON file storage
+-- Program.cs                 # Application entry point
+-- appsettings.json           # Configuration
+-- Dockerfile                 # Container build
+-- docker-compose.yml         # Local container orchestration
```

## Extending for Production

### Replace Storage Backend

Implement `INoteRepository` for your production storage:

```csharp
// SQL Server
services.AddScoped<INoteRepository, SqlServerNoteRepository>();

// Cosmos DB
services.AddScoped<INoteRepository, CosmosDbNoteRepository>();

// Redis (for caching layer)
services.AddScoped<INoteRepository, RedisNoteRepository>();
```

### Add Azure AD Authentication

Replace `McpAuthMiddleware` with Azure AD/Entra ID:

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
```

### Add Distributed Tracing

```csharp
builder.Services.AddOpenTelemetryTracing(builder =>
{
    builder.AddAspNetCoreInstrumentation()
           .AddHttpClientInstrumentation()
           .AddJaegerExporter();
});
```

## Differences from stdio MCP Servers

| Aspect | stdio (IDE) | HTTP (Enterprise) |
|--------|-------------|-------------------|
| Transport | stdin/stdout | HTTP POST |
| Client | IDE (subprocess) | Orchestrator service |
| Scaling | Single process | Horizontal (replicas) |
| Discovery | File path | DNS / Service registry |
| Auth | OS-level | API keys, OAuth, mTLS |
| State | Local files | Shared database |

## License

MIT
