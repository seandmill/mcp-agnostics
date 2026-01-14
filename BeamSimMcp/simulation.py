"""
Deterministic beam search simulation engine.

Scenarios define a state space with possible actions that transform states.
The beam search explores the space keeping the top-k candidates at each step.
"""

import hashlib
import random
from dataclasses import dataclass, field
from typing import Any


@dataclass
class SimulationState:
    """A state in the simulation with its score and history."""
    values: dict[str, Any]
    score: float = 0.0
    history: list[str] = field(default_factory=list)


@dataclass
class SimulationResult:
    """Result of a beam search simulation."""
    run_id: str
    best_result: dict[str, Any]
    top_k: list[dict[str, Any]]
    score_breakdown: dict[str, float]
    intermediate_states: list[list[dict[str, Any]]]
    scenario: dict[str, Any]
    constraints: dict[str, Any]


def _compute_hash(data: dict[str, Any]) -> int:
    """Compute a deterministic hash from scenario data."""
    serialized = str(sorted(data.items()))
    return int(hashlib.sha256(serialized.encode()).hexdigest()[:16], 16)


def _default_score_function(state: dict[str, Any], constraints: dict[str, Any]) -> dict[str, float]:
    """
    Default scoring: sum numeric values, apply constraint penalties.
    Returns breakdown of score components.
    """
    breakdown = {}
    
    # Score based on numeric values in state
    value_score = sum(v for v in state.values() if isinstance(v, (int, float)))
    breakdown["value_sum"] = value_score
    
    # Apply constraint penalties
    penalty = 0.0
    for key, limit in constraints.items():
        if key.startswith("max_") and key[4:] in state:
            actual = state.get(key[4:], 0)
            if isinstance(actual, (int, float)) and actual > limit:
                penalty -= (actual - limit) * 10
        elif key.startswith("min_") and key[4:] in state:
            actual = state.get(key[4:], 0)
            if isinstance(actual, (int, float)) and actual < limit:
                penalty -= (limit - actual) * 10
    breakdown["constraint_penalty"] = penalty
    
    return breakdown


def _generate_actions(state: dict[str, Any], scenario: dict[str, Any], rng: random.Random) -> list[dict[str, Any]]:
    """
    Generate possible actions from current state.
    Uses scenario's 'actions' field if provided, else generates increments.
    """
    actions = []
    
    if "actions" in scenario:
        # Use predefined actions from scenario
        for action in scenario["actions"]:
            actions.append(action.copy())
    else:
        # Default: generate increment/decrement actions for numeric fields
        for key, value in state.items():
            if isinstance(value, (int, float)):
                delta = rng.uniform(0.5, 2.0)
                actions.append({"type": "increment", "field": key, "delta": delta})
                actions.append({"type": "decrement", "field": key, "delta": delta})
    
    return actions


def _apply_action(state: dict[str, Any], action: dict[str, Any]) -> dict[str, Any]:
    """Apply an action to a state, returning new state."""
    new_state = state.copy()
    
    action_type = action.get("type", "")
    field = action.get("field", "")
    delta = action.get("delta", 1)
    
    if action_type == "increment" and field in new_state:
        if isinstance(new_state[field], (int, float)):
            new_state[field] = new_state[field] + delta
    elif action_type == "decrement" and field in new_state:
        if isinstance(new_state[field], (int, float)):
            new_state[field] = new_state[field] - delta
    elif action_type == "set" and field:
        new_state[field] = action.get("value", 0)
    
    return new_state


class BeamSimulator:
    """Deterministic beam search simulator."""
    
    def __init__(
        self,
        beam_width: int = 5,
        max_steps: int = 10,
        seed: int | None = None
    ):
        self.beam_width = beam_width
        self.max_steps = max_steps
        self.seed = seed
        self.rng = random.Random(seed if seed is not None else _compute_hash({"default": True}))
    
    def run(
        self,
        run_id: str,
        scenario: dict[str, Any],
        constraints: dict[str, Any] | None = None
    ) -> SimulationResult:
        """
        Execute beam search simulation.
        
        Args:
            run_id: Unique identifier for this run
            scenario: Must contain 'initial_state' dict
            constraints: Optional constraints like max_x, min_y
        
        Returns:
            SimulationResult with best result, top-k, and trace
        """
        constraints = constraints or {}
        
        # Initialize with scenario's initial state
        initial_state = scenario.get("initial_state", {})
        if not initial_state:
            raise ValueError("Scenario must contain 'initial_state'")
        
        # Create initial beam
        initial_breakdown = _default_score_function(initial_state, constraints)
        initial_score = sum(initial_breakdown.values())
        
        beam: list[SimulationState] = [
            SimulationState(
                values=initial_state.copy(),
                score=initial_score,
                history=["initial"]
            )
        ]
        
        intermediate_states: list[list[dict[str, Any]]] = []
        
        # Run beam search
        for step in range(self.max_steps):
            # Record intermediate state
            intermediate_states.append([
                {"values": s.values.copy(), "score": s.score, "history": s.history.copy()}
                for s in beam
            ])
            
            candidates: list[SimulationState] = []
            
            for state in beam:
                actions = _generate_actions(state.values, scenario, self.rng)
                
                for action in actions:
                    new_values = _apply_action(state.values, action)
                    breakdown = _default_score_function(new_values, constraints)
                    new_score = sum(breakdown.values())
                    
                    action_desc = f"{action.get('type', 'unknown')}({action.get('field', '')})"
                    candidates.append(SimulationState(
                        values=new_values,
                        score=new_score,
                        history=state.history + [action_desc]
                    ))
            
            # Keep top beam_width candidates
            candidates.sort(key=lambda s: s.score, reverse=True)
            beam = candidates[:self.beam_width]
            
            if not beam:
                break
        
        # Record final state
        intermediate_states.append([
            {"values": s.values.copy(), "score": s.score, "history": s.history.copy()}
            for s in beam
        ])
        
        # Get best result
        best = beam[0] if beam else SimulationState(values=initial_state, score=0.0)
        best_breakdown = _default_score_function(best.values, constraints)
        
        return SimulationResult(
            run_id=run_id,
            best_result={
                "values": best.values,
                "score": best.score,
                "path": best.history
            },
            top_k=[
                {"values": s.values, "score": s.score, "path": s.history}
                for s in beam[:self.beam_width]
            ],
            score_breakdown=best_breakdown,
            intermediate_states=intermediate_states,
            scenario=scenario,
            constraints=constraints
        )
    
    def explain(self, result: SimulationResult) -> str:
        """Generate human-readable explanation of how the best result was chosen."""
        lines = [
            f"# Simulation Explanation (Run ID: {result.run_id})",
            "",
            "## Initial Scenario",
            f"Starting state: {result.scenario.get('initial_state', {})}",
            f"Constraints applied: {result.constraints or 'None'}",
            "",
            "## Search Process",
            f"The beam search explored {len(result.intermediate_states)} steps,",
            f"keeping the top {len(result.top_k)} candidates at each step.",
            "",
            "## Winning Path",
        ]
        
        for i, step in enumerate(result.best_result.get("path", [])):
            lines.append(f"  {i}. {step}")
        
        lines.extend([
            "",
            "## Final Score Breakdown",
        ])
        
        for component, value in result.score_breakdown.items():
            lines.append(f"  - {component}: {value:.2f}")
        
        lines.extend([
            "",
            f"**Total Score: {result.best_result.get('score', 0):.2f}**",
            "",
            "## Why This Result Won",
            "This result achieved the highest combined score by maximizing",
            "value contributions while minimizing constraint penalties.",
        ])
        
        if result.constraints:
            lines.append(f"The constraints {list(result.constraints.keys())} were successfully respected.")
        
        return "\n".join(lines)
