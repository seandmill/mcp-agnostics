# MCP Agnostics

A collection of minimal MCP (Model Context Protocol) servers demonstrating various capabilities. Each server is self-contained and language-agnostic in design.

## Available Servers

| Server                       | Language     | Purpose                                                  |
| ---------------------------- | ------------ | -------------------------------------------------------- |
| [LocalOpsMcp](./LocalOpsMcp) | C# (.NET 8)  | Note management with create, search, and summarize tools |
| [BeamSimMcp](./BeamSimMcp)   | Python 3.10+ | Deterministic beam search simulation                     |

---

## Serving MCP Servers Locally

### LocalOpsMcp (C#)

```bash
cd LocalOpsMcp
dotnet build
dotnet run
```

The server runs on stdin/stdout, waiting for JSON-RPC input.

### BeamSimMcp (Python)

```bash
cd BeamSimMcp
python3 -m venv .venv
source .venv/bin/activate
pip install -e .
python server.py
```

The server runs on stdin/stdout, waiting for JSON-RPC input.

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
    }
  }
}
```

> [!NOTE]
> After editing `settings.json`, restart your IDE for changes to take effect.

---

## Probing the LLM to Invoke MCP Tools

Once registered, the LLM has access to these tools. Use natural language that matches the tool's purpose:

### LocalOpsMcp Keywords

| Tool              | Trigger Phrases                                               |
| ----------------- | ------------------------------------------------------------- |
| `notes.create`    | "create a note", "save a note about...", "jot down..."        |
| `notes.search`    | "search my notes for...", "find notes about...", "look up..." |
| `notes.summarize` | "summarize my notes", "give me a summary of notes on..."      |

**Example prompts:**

- _"Create a note titled 'Meeting Notes' with the content 'Discussed Q4 roadmap'"_
- _"Search my notes for anything about roadmap"_
- _"Summarize all my notes tagged with 'work'"_

### BeamSimMcp Keywords

| Tool               | Trigger Phrases                                                                     |
| ------------------ | ----------------------------------------------------------------------------------- |
| `simulate.run`     | "run a simulation", "simulate...", "beam search over...", "optimize..."             |
| `simulate.explain` | "explain that simulation", "why did the simulation choose...", "walk me through..." |

**Example prompts:**

- _"Run a beam search simulation starting with x=10, y=5, with a max constraint of x=50"_
- _"Simulate optimizing these values with a beam width of 3 and seed 42 for reproducibility"_
- _"Explain the last simulation - why did it pick that result?"_

---

## Testing MCP Servers Manually

You can test servers directly by piping JSON-RPC to stdin:

```bash
# List available tools
echo '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}' | dotnet run --project LocalOpsMcp

# Call a tool
echo '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"notes.create","arguments":{"title":"Test","body":"Hello"}}}' | dotnet run --project LocalOpsMcp
```

---

## Architecture

All servers follow the MCP specification:

- **Transport**: stdin/stdout with JSON-RPC 2.0
- **Tools**: Actions the LLM can invoke with structured input/output
- **Resources**: Data the LLM can read via `resources://` URIs
- **Persistence**: Local JSON files (no external services)

```
┌─────────────────┐     stdin/stdout      ┌─────────────────┐
│       IDE       │ ◄──── JSON-RPC ────► │   MCP Server    │
│                 │                       │ (LocalOps/Beam) │
└─────────────────┘                       └─────────────────┘
                                                   │
                                                   ▼
                                          ┌───────────────┐
                                          │  Local JSON   │
                                          │    Storage    │
                                          └───────────────┘
```

---

## Adding New MCP Servers

1. Create a new directory at root (e.g., `MyNewMcp/`)
2. Implement the MCP protocol (tools/list, tools/call, resources/list, resources/read)
3. Add a `README.md` with tool documentation
4. Register in the IDE's `settings.json`

See existing servers for reference implementations.
