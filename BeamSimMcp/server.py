"""
Beam Simulation MCP Server

A minimal MCP server for running deterministic beam search simulations.
Exposes tools for running simulations and explaining results.
"""

import asyncio
import json
from dataclasses import asdict
from typing import Any

from mcp.server import Server
from mcp.server.stdio import stdio_server
from mcp.types import (
    Resource,
    TextContent,
    Tool,
)

from simulation import BeamSimulator, SimulationResult
from storage import generate_run_id, get_simulation, save_simulation

# Create the MCP server instance
server = Server("beam-sim-mcp")


# --- Tool Definitions ---

SIMULATE_RUN_SCHEMA = {
    "type": "object",
    "properties": {
        "scenario": {
            "type": "object",
            "description": "Simulation scenario with 'initial_state' dict and optional 'actions' array",
            "properties": {
                "initial_state": {
                    "type": "object",
                    "description": "Initial state values (numeric fields for optimization)"
                },
                "actions": {
                    "type": "array",
                    "description": "Optional predefined actions",
                    "items": {"type": "object"}
                }
            },
            "required": ["initial_state"]
        },
        "constraints": {
            "type": "object",
            "description": "Optional constraints like max_x, min_y for scoring penalties"
        },
        "beamWidth": {
            "type": "integer",
            "description": "Number of candidates to keep per step (default: 5)",
            "default": 5
        },
        "maxSteps": {
            "type": "integer", 
            "description": "Maximum simulation steps (default: 10)",
            "default": 10
        },
        "seed": {
            "type": "integer",
            "description": "Random seed for deterministic results"
        }
    },
    "required": ["scenario"]
}

SIMULATE_EXPLAIN_SCHEMA = {
    "type": "object",
    "properties": {
        "runId": {
            "type": "string",
            "description": "ID of a previous simulation run to explain"
        }
    },
    "required": ["runId"]
}


@server.list_tools()
async def list_tools() -> list[Tool]:
    """Return available simulation tools."""
    return [
        Tool(
            name="simulate_run",
            description="Run a deterministic beam search simulation over a scenario",
            inputSchema=SIMULATE_RUN_SCHEMA
        ),
        Tool(
            name="simulate_explain",
            description="Get a human-readable explanation of a simulation run",
            inputSchema=SIMULATE_EXPLAIN_SCHEMA
        )
    ]


@server.call_tool()
async def call_tool(name: str, arguments: dict[str, Any]) -> list[TextContent]:
    """Handle tool invocations."""
    
    if name == "simulate_run":
        return await _handle_simulate_run(arguments)
    elif name == "simulate_explain":
        return await _handle_simulate_explain(arguments)
    else:
        raise ValueError(f"Unknown tool: {name}")


async def _handle_simulate_run(args: dict[str, Any]) -> list[TextContent]:
    """Execute a beam search simulation."""
    
    # Validate required input
    scenario = args.get("scenario")
    if not scenario:
        raise ValueError("Missing required field: scenario")
    
    if not isinstance(scenario, dict):
        raise ValueError("scenario must be an object")
    
    if "initial_state" not in scenario:
        raise ValueError("scenario must contain 'initial_state'")
    
    # Extract optional parameters
    constraints = args.get("constraints", {})
    beam_width = args.get("beamWidth", 5)
    max_steps = args.get("maxSteps", 10)
    seed = args.get("seed")
    
    # Validate parameter types
    if not isinstance(beam_width, int) or beam_width < 1:
        raise ValueError("beamWidth must be a positive integer")
    if not isinstance(max_steps, int) or max_steps < 1:
        raise ValueError("maxSteps must be a positive integer")
    if seed is not None and not isinstance(seed, int):
        raise ValueError("seed must be an integer")
    
    # Generate run ID (deterministic if seed provided)
    run_id = generate_run_id(seed)
    
    # Run simulation
    simulator = BeamSimulator(
        beam_width=beam_width,
        max_steps=max_steps,
        seed=seed
    )
    
    result = simulator.run(run_id, scenario, constraints)
    
    # Persist the result
    await save_simulation(run_id, {
        "run_id": result.run_id,
        "best_result": result.best_result,
        "top_k": result.top_k,
        "score_breakdown": result.score_breakdown,
        "intermediate_states": result.intermediate_states,
        "scenario": result.scenario,
        "constraints": result.constraints
    })
    
    # Return structured response
    response = {
        "runId": result.run_id,
        "bestResult": result.best_result,
        "topK": result.top_k,
        "scoreBreakdown": result.score_breakdown
    }
    
    return [TextContent(type="text", text=json.dumps(response, indent=2))]


async def _handle_simulate_explain(args: dict[str, Any]) -> list[TextContent]:
    """Generate explanation for a simulation run."""
    
    run_id = args.get("runId")
    if not run_id:
        raise ValueError("Missing required field: runId")
    
    if not isinstance(run_id, str):
        raise ValueError("runId must be a string")
    
    # Retrieve the simulation
    data = await get_simulation(run_id)
    if not data:
        raise ValueError(f"Simulation not found: {run_id}")
    
    # Reconstruct result object
    result = SimulationResult(
        run_id=data["run_id"],
        best_result=data["best_result"],
        top_k=data["top_k"],
        score_breakdown=data["score_breakdown"],
        intermediate_states=data["intermediate_states"],
        scenario=data["scenario"],
        constraints=data["constraints"]
    )
    
    # Generate explanation
    simulator = BeamSimulator()
    explanation = simulator.explain(result)
    
    return [TextContent(type="text", text=explanation)]


# --- Resource Definitions ---

@server.list_resources()
async def list_resources() -> list[Resource]:
    """List available simulation resources."""
    from storage import list_simulations
    
    run_ids = await list_simulations()
    return [
        Resource(
            uri=f"simulations://{run_id}",
            name=f"Simulation {run_id[:8]}...",
            description="Full simulation data including input, intermediate states, and results",
            mimeType="application/json"
        )
        for run_id in run_ids
    ]


@server.read_resource()
async def read_resource(uri: str) -> str:
    """Read a simulation resource by URI."""
    
    # Parse the URI
    if not uri.startswith("simulations://"):
        raise ValueError(f"Invalid resource URI scheme: {uri}")
    
    run_id = uri.replace("simulations://", "")
    if not run_id:
        raise ValueError("Missing run ID in URI")
    
    # Retrieve simulation data
    data = await get_simulation(run_id)
    if not data:
        raise ValueError(f"Simulation not found: {run_id}")
    
    return json.dumps(data, indent=2)


# --- Server Entry Point ---

async def run_server():
    """Run the MCP server."""
    async with stdio_server() as (read_stream, write_stream):
        await server.run(
            read_stream,
            write_stream,
            server.create_initialization_options()
        )


def main():
    """Entry point for the beam-sim-mcp command."""
    asyncio.run(run_server())


if __name__ == "__main__":
    main()
