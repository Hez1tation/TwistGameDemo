using System.Collections.Generic;
using UnityEngine;

public enum MathOp { Plus, Minus, Multiply }

public static class MathOpExt
{
    public static char ToChar(this MathOp op)
    {
        switch (op) { case MathOp.Plus: return '+'; case MathOp.Minus: return '-'; default: return '*'; }
    }

    public static string Symbol(this MathOp op)
    {
        switch (op) { case MathOp.Plus: return "+"; case MathOp.Minus: return "-"; default: return "x"; }
    }
}

// Pure game rules for the Step / Math Allowance maze (no rendering).
// Walls are always solid - unlike the previous prototype, there is no rule-modification here;
// the twist lives entirely in how the step budget is spent.
public class StepMazeModel
{
    public readonly HashSet<Vector2Int> walls;
    public readonly Dictionary<Vector2Int, int> numbers;
    public readonly Dictionary<Vector2Int, char> operators;
    public readonly Vector2Int start;
    public readonly Vector2Int exit;

    public Vector2Int pos;
    public int steps;
    // No default operator: a number tile only applies (and is only consumed) once an operator
    // has been collected. Landing on a number with no pending operator does nothing and leaves
    // the tile in place. Each application consumes the pending operator, so a fresh operator
    // must be collected before every number.
    public char? pendingOp = null;

    public bool Won => pos == exit;
    public bool Dead => !Won && steps <= 0;

    public StepMazeModel(HashSet<Vector2Int> walls, Dictionary<Vector2Int, int> numbers,
        Dictionary<Vector2Int, char> operators, Vector2Int start, Vector2Int exit, int startSteps)
    {
        this.walls = walls;
        this.numbers = new Dictionary<Vector2Int, int>(numbers);
        this.operators = new Dictionary<Vector2Int, char>(operators);
        this.start = start;
        this.exit = exit;
        pos = start;
        steps = startSteps;
    }

    public bool Passable(Vector2Int p) => !walls.Contains(p);

    // Moves one tile if possible. Reports what tile effect triggered so the view can react.
    public bool Step(Vector2Int dir, out char triggeredOperator, out bool triggeredNumber, out int numberValue)
    {
        triggeredOperator = '\0';
        triggeredNumber = false;
        numberValue = 0;

        var next = pos + dir;
        if (!Passable(next)) return false;

        pos = next;
        steps -= 1;

        if (operators.TryGetValue(pos, out char op))
        {
            pendingOp = op;
            operators.Remove(pos);
            triggeredOperator = op;
        }
        else if (numbers.TryGetValue(pos, out int val) && pendingOp.HasValue)
        {
            steps = Apply(pendingOp.Value, steps, val);
            pendingOp = null;
            numbers.Remove(pos);
            triggeredNumber = true;
            numberValue = val;
        }
        return true;
    }

    static int Apply(char op, int s, int v)
    {
        switch (op) { case '+': return s + v; case '-': return s - v; case '*': return s * v; default: return s; }
    }
}
