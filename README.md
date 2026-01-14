# MCP Agnostics

A collection of MCP (Model Context Protocol) servers demonstrating various capabilities across languages and deployment models. Includes both **stdio-based servers** for IDE integration and an **HTTP-based enterprise server** for production LLM orchestration workflows.

## Available Servers

| Server | Language | Transport | Purpose |
|--------|----------|-----------|---------|
| [LocalOpsMcp](./LocalOpsMcp) | C# (.NET 8) | stdio | Note management (IDE integration) |
| [BeamSimMcp](./BeamSimMcp) | Python 3.10+ | stdio | Deterministic beam search simulation |
| [LocalKnowledgeMcp](./LocalKnowledgeMcp) | TypeScript | stdio | Document indexing and TF/keyword search |
| [EnterpriseNotesMcp](./EnterpriseNotesMcp) | C# (.NET 8) | **HTTP** | Enterprise note management with auth, health checks, and container deployment |

---

## Transport Models

### stdio Transport (IDE Integration)

The stdio-based servers (`LocalOpsMcp`, `BeamSimMcp`, `LocalKnowledgeMcp`) run as subprocesses spawned by IDEs:

```
+-------------+     subprocess     +-------------+
|     IDE     | ----------------> |  MCP Server |
|             | <-- stdin/stdout->|   (stdio)   |
+-------------+                   +-------------+
```

### HTTP Transport (Enterprise Workflows)

The HTTP-based server (`EnterpriseNotesMcp`) runs as a persistent service accessible via REST:

```
+------------------+      HTTP/JSON-RPC      +------------------+
| LLM Orchestrator | --------------------->  | EnterpriseNotes  |
|                  | <---------------------  |   MCP Server     |
+--------+---------+                         +--------+---------+
         |                                            |
         v                                            v
+------------------+                         +------------------+
|  Model Endpoint  |                         |   Persistent     |
| (Azure OpenAI,   |                         |     Storage      |
|  Claude, etc.)   |                         +------------------+
+------------------+
```

---

## Serving MCP Servers Locally

### LocalOpsMcp (C# - stdio)

```bash
cd LocalOpsMcp
dotnet build
dotnet run
```

The server runs on stdin/stdout, waiting for JSON-RPC input.

### BeamSimMcp (Python - stdio)

```bash
cd BeamSimMcp
python3 -m venv .venv
source .venv/bin/activate
pip install -e .
python server.py
```

The server runs on stdin/stdout, waiting for JSON-RPC input.

### LocalKnowledgeMcp (TypeScript - stdio)

```bash
cd LocalKnowledgeMcp
npm install
npm run build
node dist/index.js
```

The server runs on stdin/stdout, waiting for JSON-RPC input.

### EnterpriseNotesMcp (C# - HTTP)

```bash
cd EnterpriseNotesMcp
dotnet restore
dotnet run
```

Server starts at `http://localhost:5000`. Visit `/swagger` for interactive API docs.

---

## Registering with an IDE

Many IDEs support MCP servers. Here's how to register them:

### macOS/Linux

Edit `~/.gemini/settings.json` or `~/.gemini/<IDE>/mcp_config.json`:

```json
{
  "mcpServers": {
    "local-ops": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/mcp-agnostics/LocalOpsMcp"]
    },
    "beam-sim": {
      "command": "/path/to/mcp-agnostics/BeamSimMcp/.venv/bin/python",
      "args": ["/path/to/mcp-agnostics/BeamSimMcp/server.py"]
    },
    "local-knowledge": {
      "command": "node",
      "args": ["/path/to/mcp-agnostics/LocalKnowledgeMcp/dist/index.js"]
    }
  }
}
```

### Windows

Edit `%USERPROFILE%\.gemini\settings.json`:

```json
{
  "mcpServers": {
    "local-ops": {
      "command": "dotnet",
      "args": ["run", "--project", "C:\\path\\to\\mcp-agnostics\\LocalOpsMcp"]
    },
    "beam-sim": {
      "command": "C:\\path\\to\\mcp-agnostics\\BeamSimMcp\\.venv\\Scripts\\python.exe",
      "args": ["C:\\path\\to\\mcp-agnostics\\BeamSimMcp\\server.py"]
    },
    "local-knowledge": {
      "command": "node",
      "args": ["C:\\path\\to\\mcp-agnostics\\LocalKnowledgeMcp\\dist\\index.js"]
    }
  }
}
```

> [!NOTE]
> After editing `settings.json`, restart your IDE for changes to take effect.

---

## Enterprise Deployment

The `EnterpriseNotesMcp` server demonstrates how to deploy MCP in production environments where LLM orchestrators need to call MCP tools as part of automated workflows.

### Docker Deployment

```bash
cd EnterpriseNotesMcp

# Build and run
docker-compose up -d

# Verify health
curl http://localhost:8080/health

# Test MCP endpoint
curl -X POST http://localhost:8080/mcp \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'
```

### Kubernetes Deployment

```bash
# Create namespace
kubectl create namespace ai-platform

# Deploy
kubectl apply -f EnterpriseNotesMcp/k8s/

# Verify
kubectl get pods -n ai-platform -l app=enterprise-notes-mcp
```

### Enterprise Architecture Pattern

```
                     Enterprise MCP Registry
+-------------------------------------------------------------+
|                                                             |
|  User Request                                               |
|       |                                                     |
|       v                                                     |
|  +------------+     +-------------------+                   |
|  | API Gateway| --> | LLM Orchestrator  |                   |
|  | (auth)     |     | (pre-processing,  |                   |
|  +------------+     |  tool dispatch)   |                   |
|                     +---------+---------+                   |
|                               |                             |
|          +--------------------+--------------------+        |
|          |                    |                    |        |
|          v                    v                    v        |
|  +---------------+   +---------------+   +---------------+  |
|  | EnterpriseMcp |   | BeamSimMcp    |   | KnowledgeMcp  |  |
|  | (HTTP :8080)  |   | (HTTP :8081)  |   | (HTTP :8082)  |  |
|  +---------------+   +---------------+   +---------------+  |
|          |                    |                    |        |
|          v                    v                    v        |
|  +---------------+   +---------------+   +---------------+  |
|  |   SQL/Cosmos  |   |  Simulations  |   | Vector Store  |  |
|  +---------------+   +---------------+   +---------------+  |
|                                                             |
+-------------------------------------------------------------+
```

### Key Differences: stdio vs HTTP

| Aspect | stdio (IDE) | HTTP (Enterprise) |
|--------|-------------|-------------------|
| Client | IDE (subprocess) | Orchestrator service |
| Connection | Process spawn | HTTP requests |
| Scaling | Single instance | Horizontal (replicas) |
| Auth | OS-level | API keys, OAuth, mTLS |
| Discovery | File path config | DNS / Service registry |
| State | Local files | Shared database |
| Monitoring | stderr logs | Prometheus, tracing |

### Integration Flow

1. **User submits request** -> API Gateway (auth, rate limiting)
2. **Pre-inference tasks** run (logging, context enrichment)
3. **Orchestrator calls LLM** with tool definitions from MCP servers
4. **LLM returns tool call** -> Orchestrator intercepts
5. **Orchestrator calls MCP server** via HTTP
6. **MCP server executes tool** -> Returns JSON-RPC response
7. **Orchestrator injects result** into LLM context
8. **LLM generates final response**

See [EnterpriseNotesMcp/Examples/McpClient.cs](./EnterpriseNotesMcp/Examples/McpClient.cs) for a complete orchestrator integration example.

---

## Probing the LLM to Invoke MCP Tools

Once registered, the LLM has access to these tools. Use natural language that matches the tool's purpose:

### LocalOpsMcp / EnterpriseNotesMcp Keywords

| Tool | Trigger Phrases |
|------|-----------------|
| `notes_create` | "create a note", "save a note about...", "jot down..." |
| `notes_search` | "search my notes for...", "find notes about...", "look up..." |
| `notes_summarize` | "summarize my notes", "give me a summary of notes on..." |

### BeamSimMcp Keywords

| Tool | Trigger Phrases |
|------|-----------------|
| `simulate_run` | "run a simulation", "simulate...", "beam search over...", "optimize..." |
| `simulate_explain` | "explain that simulation", "why did the simulation choose...", "walk me through..." |

### LocalKnowledgeMcp Keywords

| Tool | Trigger Phrases |
|------|-----------------|
| `docs_ingest` | "index this document", "save this text", "add to knowledge base" |
| `docs_query` | "search my documents for...", "find docs about...", "query knowledge" |

---

## Testing MCP Servers Manually

### stdio Servers

```bash
# List available tools
echo '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}' | dotnet run --project LocalOpsMcp

# Call a tool
echo '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"notes_create","arguments":{"title":"Test","body":"Hello"}}}' | dotnet run --project LocalOpsMcp
```

### HTTP Server (EnterpriseNotesMcp)

```bash
# Start server
cd EnterpriseNotesMcp && dotnet run &

# List tools
curl -X POST http://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'

# Create a note
curl -X POST http://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc":"2.0",
    "id":2,
    "method":"tools/call",
    "params":{
      "name":"notes_create",
      "arguments":{"title":"Test Note","body":"Created via HTTP"}
    }
  }'
```

---

## Architecture

### stdio Servers

```
+------------------+     stdin/stdout      +------------------+
|       IDE        | <----- JSON-RPC ----> |   MCP Server     |
|                  |                       | (LocalOps/Beam)  |
+------------------+                       +------------------+
                                                    |
                                                    v
                                           +----------------+
                                           |  Local JSON    |
                                           |    Storage     |
                                           +----------------+
```

### HTTP Server (Enterprise)

```
+------------------+                       +------------------+
| LLM Orchestrator |                       | EnterpriseNotes  |
|                  | ------- HTTP -------> |   MCP Server     |
|                  | <----- JSON-RPC ----- |                  |
+------------------+                       +------------------+
                                                    |
                                           +-------+-------+
                                           |               |
                                           v               v
                                   +------------+  +-------------+
                                   | SQL/Cosmos |  | Redis Cache |
                                   +------------+  +-------------+
```

---

## Adding New MCP Servers

1. Create a new directory at root (e.g., `MyNewMcp/`)
2. Choose transport:
   - **stdio**: For IDE integration (simpler, local only)
   - **HTTP**: For enterprise/orchestrator integration (scalable, networked)
3. Implement the MCP protocol (`tools/list`, `tools/call`, `resources/list`, `resources/read`)
4. Add a `README.md` with tool documentation
5. For stdio: Register in IDE's `settings.json`
6. For HTTP: Deploy via Docker/Kubernetes

See existing servers for reference implementations:
- **stdio**: `LocalOpsMcp`, `BeamSimMcp`, `LocalKnowledgeMcp`
- **HTTP**: `EnterpriseNotesMcp`
