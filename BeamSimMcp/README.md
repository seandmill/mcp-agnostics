# Beam Simulation MCP Server

A minimal Python MCP server for running deterministic beam search simulations.

## How to Run

1. **Prerequisites**: Python 3.10+, pip
2. **Install**:
   ```bash
   cd BeamSimMcp
   pip install -e .
   ```
3. **Run**:
   ```bash
   python server.py
   ```
   The server speaks JSON-RPC over stdin/stdout and will wait for input.

## Example MCP Client Calls

### simulate_run

Run a beam search simulation:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "simulate_run",
    "arguments": {
      "scenario": {
        "initial_state": {
          "x": 0,
          "y": 0,
          "score": 10
        }
      },
      "constraints": {
        "max_x": 100,
        "min_y": -50
      },
      "beamWidth": 5,
      "maxSteps": 10,
      "seed": 42
    }
  }
}
```

**Response:**

```json
{
  "runId": "a1b2c3d4-...",
  "bestResult": {
    "values": {"x": 45.2, "y": 12.3, "score": 67.5},
    "score": 125.0,
    "path": ["initial", "increment(x)", "increment(score)", ...]
  },
  "topK": [...],
  "scoreBreakdown": {
    "value_sum": 125.0,
    "constraint_penalty": 0.0
  }
}
```

### simulate_explain

Get a human-readable explanation of a run:

```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "tools/call",
  "params": {
    "name": "simulate_explain",
    "arguments": {
      "runId": "a1b2c3d4-..."
    }
  }
}
```

### Read Resource

Retrieve full simulation data:

```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "resources/read",
  "params": {
    "uri": "simulations://a1b2c3d4-..."
  }
}
```

## Design Notes

- **Determinism**: Uses `random.seed()` for reproducible simulations. Same seed + scenario = same results.
- **Beam Search**: Keeps top-k candidates at each step, explores action space incrementally.
- **Persistence**: Stores runs in `simulations.json` with atomic writes (temp file + rename).
- **Extensibility**:
  - Add custom scoring in `simulation.py:_default_score_function`
  - Define scenario-specific actions in the `actions` field
  - Extend constraints vocabulary (currently supports `max_*` and `min_*` prefixes)
