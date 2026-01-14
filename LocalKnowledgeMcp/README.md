# LocalKnowledgeMcp

A minimal TypeScript MCP server providing local document indexing and TF/keyword search.

## Tools

| Tool          | Description                                    |
| ------------- | ---------------------------------------------- |
| `docs_ingest` | Ingest a document for indexing and persistence |
| `docs_query`  | Search documents using keyword/TF scoring      |

## Resources

| URI           | Description                      |
| ------------- | -------------------------------- |
| `docs://<id>` | Returns full document + metadata |

## Setup

```bash
cd LocalKnowledgeMcp
npm install
npm run build
```

## Run

```bash
npm start
# or
node dist/index.js
```

The server runs on stdin/stdout, waiting for JSON-RPC input.

## Example MCP Client Calls

### List Tools

```bash
echo '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}' | node dist/index.js
```

### Ingest a Document

```bash
echo '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"docs_ingest","arguments":{"id":"intro-ts","text":"TypeScript is a typed superset of JavaScript that compiles to plain JavaScript.","metadata":{"author":"docs","tags":["typescript","javascript"]}}}}' | node dist/index.js
```

### Query Documents

```bash
echo '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"docs_query","arguments":{"query":"typescript javascript","limit":5}}}' | node dist/index.js
```

### List Resources

```bash
echo '{"jsonrpc":"2.0","id":1,"method":"resources/list","params":{}}' | node dist/index.js
```

### Read a Document Resource

```bash
echo '{"jsonrpc":"2.0","id":1,"method":"resources/read","params":{"uri":"docs://intro-ts"}}' | node dist/index.js
```

## IDE Registration

Add to `~/.gemini/settings.json`:

```json
{
  "mcpServers": {
    "local-knowledge": {
      "command": "node",
      "args": ["/path/to/mcp-agnostics/LocalKnowledgeMcp/dist/index.js"]
    }
  }
}
```

## Design Notes

- **TF Scoring**: Simple term frequency matching—no external dependencies, works well for small corpora
- **Atomic Writes**: Uses temp file + rename to prevent data corruption
- **Pre-tokenization**: Documents are tokenized on ingest for fast query-time matching
- **Extensibility**: Add IDF weighting by tracking document frequency across corpus
