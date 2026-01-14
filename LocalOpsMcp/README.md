# Local Ops MCP Server

A minimal C# MCP server for managing local notes.

## How to Run

1. **Prerequisites**: .NET 8 SDK
2. **Build**:
   ```bash
   dotnet build
   ```
3. **Run**:
   ```bash
   dotnet run
   ```
   (Note: The server speaks JSON-RPC over Stdin/Stdout, so it will wait for input).

## Example Client Calls

### Create Note

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "notes_create",
    "arguments": {
      "title": "Meeting Notes",
      "body": "Discussed Q4 roadmap. Action items: release v2.",
      "tags": ["work", "planning"]
    }
  }
}
```

### Search Notes

```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "tools/call",
  "params": {
    "name": "notes_search",
    "arguments": {
      "query": "roadmap",
      "limit": 5
    }
  }
}
```

### Read Resource

```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "resources/read",
  "params": {
    "uri": "notes://<UUID>"
  }
}
```

## Design Notes

- **Persistence**: Uses a local `notes.json` file. Thread-safe simple lock for writes.
- **Protocol**: Implements minimal JSON-RPC 2.0. No 3rd party NuGets used, only `System.Text.Json`.
- **Extensibility**: Add new tools in `Handlers.cs` and register them in `Program.cs`.
