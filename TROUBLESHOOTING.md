# MCP Server Quick Reference

## Tool Disambiguation

When prompting the LLM, be specific about which server you want to use:

### LocalOpsMcp (C# - Notes)

**Tools:**

- `notes_create` - Create a new note
- `notes_search` - Search existing notes
- `notes_summarize` - Summarize a note by ID

**Example prompts:**

- ✅ "Create a note about the meeting"
- ✅ "Search my notes for 'roadmap'"
- ✅ "Use the notes server to save this"

**Storage:** `LocalOpsMcp/notes.json`

### BeamSimMcp (Python - Simulation)

**Tools:**

- `simulate_run` - Run beam search simulation
- `simulate_explain` - Explain simulation results

**Example prompts:**

- ✅ "Run a simulation with x=10, y=5"
- ✅ "Simulate optimizing these values"
- ✅ "Use beam search to find the best result"

**Storage:** `BeamSimMcp/simulations.json`

## Troubleshooting

### "Wrong server is being called"

**Solution:** Be more explicit in your prompt

- Instead of: "save this"
- Use: "create a note about this" or "use the notes server"

### "Server not responding"

**Solution:** Restart Antigravity after editing `mcp_config.json`

### "Command not found"

**Solution:** Check that paths in `mcp_config.json` are absolute:

```bash
# Verify C# server path
ls -la /Users/seanmillah/Documents/sdmdev/mcp-agnostics/LocalOpsMcp

# Verify Python server path
ls -la /Users/seanmillah/Documents/sdmdev/mcp-agnostics/BeamSimMcp/.venv/bin/python
```

### Testing manually

```bash
# Test C# server
cd LocalOpsMcp
echo '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}' | dotnet run

# Test Python server
cd BeamSimMcp
source .venv/bin/activate
echo '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}' | python server.py
```
