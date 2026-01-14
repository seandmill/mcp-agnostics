# MCP Tool Registry

GitOps-managed tool registry for MCP servers in the Lattice AI Platform.

## Overview

This registry defines which MCP server tools are available to the Lattice Tool Gateway. Tools are defined in YAML files, validated via CI/CD, and compiled into a single `tool_catalog.json` that the Tool Gateway loads at startup.

## Directory Structure

```
tool_registry/
+-- registry.config.yaml          # Global config + MCP server endpoints
+-- compile_registry.ts           # Compiles YAML -> JSON catalog
+-- mcp-client-adapter.ts         # MCP client for Tool Gateway
+-- types.ts                      # Shared TypeScript types
+-- dist/
|   +-- tool_catalog.json         # Compiled catalog (generated)
+-- servers/
    +-- enterprise-notes/
    |   +-- tools/
    |       +-- notes_create/v1/
    |       |   +-- tool.yaml
    |       +-- notes_search/v1/
    |       |   +-- tool.yaml
    |       +-- notes_summarize/v1/
    |           +-- tool.yaml
    +-- beam-sim/
    |   +-- tools/
    |       +-- simulate_run/v1/
    |       |   +-- tool.yaml
    |       +-- simulate_explain/v1/
    |           +-- tool.yaml
    +-- local-knowledge/
        +-- tools/
            +-- docs_ingest/v1/
            |   +-- tool.yaml
            +-- docs_query/v1/
                +-- tool.yaml
```

## How It Works

### 1. Define Tools (GitOps)

Each tool is defined in a `tool.yaml` file:

```yaml
tool:
  id: notes_search
  name: Search Notes
  description: Search notes by keyword query
  version: "1.0.0"
  
  # MCP binding - which server to call
  mcp:
    server: enterprise-notes
    method: tools/call
    tool_name: notes_search
  
  # Governance
  risk_tier: low
  idempotent: true
  scopes:
    data: [notes:read]
    actions: [search]
  required_roles: []
  
  # Schemas
  input_schema: { ... }
  output_schema: { ... }
```

### 2. Compile Registry

```bash
npx ts-node compile_registry.ts
```

This validates all YAML files and generates `dist/tool_catalog.json`.

### 3. Tool Gateway Loads Catalog

At startup, the Tool Gateway:
1. Loads `tool_catalog.json`
2. Registers each tool with its MCP binding
3. When a tool is called, delegates to the MCP server via HTTP

### 4. Runtime Flow

```
Orchestration Engine
        |
        | "call tool: notes_search"
        v
   Tool Gateway
        |
        +-- Load tool from registry
        +-- Check RBAC + Policy
        +-- Check Risk Tier
        |
        v
   MCP Client Adapter
        |
        | HTTP POST /mcp
        | {"method": "tools/call", "params": {"name": "notes_search", ...}}
        v
   EnterpriseNotesMcp Server
        |
        v
   Return result through chain
```

## MCP Server Configuration

MCP servers are configured in `registry.config.yaml`:

```yaml
mcp_servers:
  enterprise-notes:
    name: "Enterprise Notes MCP"
    transport: http
    endpoint: http://enterprise-notes-mcp:8080/mcp
    health_endpoint: http://enterprise-notes-mcp:8080/health
    auth:
      type: api_key
      header: X-API-Key
      secret_ref: mcp-enterprise-notes-api-key
    owner: platform-team
    contact: platform@company.com
```

## Adding a New Tool

1. Create directory: `servers/<server-name>/tools/<tool-id>/v1/`
2. Add `tool.yaml` with full metadata
3. Run `npx ts-node compile_registry.ts` to validate
4. Commit and push (CI validates)
5. Tool Gateway picks up new tools on restart

## Adding a New MCP Server

1. Add server config to `registry.config.yaml` under `mcp_servers`
2. Deploy the MCP server (Docker/Kubernetes)
3. Create tool definitions under `servers/<server-name>/tools/`
4. Compile and deploy

## Integration with Lattice Tool Gateway

The `mcp-client-adapter.ts` shows how to integrate with the existing Tool Gateway:

```typescript
// In execute.ts
if (tool.handler) {
  // Local handler (existing tools)
  result = await tool.handler(input, userContext);
} else if (tool.mcp) {
  // MCP server delegation
  result = await mcpClient.callTool(tool.mcp, input, correlationId);
}
```

## Governance

Each tool definition includes:

| Field | Purpose |
|-------|---------|
| `risk_tier` | low/medium/high - triggers HITL for high |
| `scopes` | Data and action scopes for RBAC |
| `required_roles` | Which roles can use this tool |
| `idempotent` | Whether retries are safe |
| `redaction` | PII scan/redact rules for input/output |

## CI/CD Pipeline

```yaml
# .github/workflows/tool-registry.yml
on:
  push:
    paths:
      - 'tool_registry/**'

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
      - run: npm install yaml
      - run: npx ts-node tool_registry/compile_registry.ts
      - uses: actions/upload-artifact@v4
        with:
          name: tool-catalog
          path: tool_registry/dist/tool_catalog.json
```
