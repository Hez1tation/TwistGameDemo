using System.Collections.Generic;
using UnityEngine;

// Reads a built level (Unity primitives placed under a level root) back into the plain data
// StepMazeModel needs. Shared by the runtime game and by editor tests so both agree on what
// the scene actually contains.
public class SceneLevelData
{
    public HashSet<Vector2Int> walls = new HashSet<Vector2Int>();
    public Dictionary<Vector2Int, int> numbers = new Dictionary<Vector2Int, int>();
    public Dictionary<Vector2Int, char> operators = new Dictionary<Vector2Int, char>();
    public Dictionary<Vector2Int, GameObject> numberObjs = new Dictionary<Vector2Int, GameObject>();
    public Dictionary<Vector2Int, GameObject> operatorObjs = new Dictionary<Vector2Int, GameObject>();
    public Vector2Int start;
    public Vector2Int exit;
}

public static class SceneLevelReader
{
    public static SceneLevelData Read(Transform root)
    {
        var d = new SceneLevelData();
        foreach (var w in root.GetComponentsInChildren<WallTile>(true)) d.walls.Add(w.Cell);
        foreach (var n in root.GetComponentsInChildren<NumberTile>(true))
        {
            d.numbers[n.Cell] = n.value;
            d.numberObjs[n.Cell] = n.gameObject;
        }
        foreach (var o in root.GetComponentsInChildren<OperatorTile>(true))
        {
            d.operators[o.Cell] = o.op.ToChar();
            d.operatorObjs[o.Cell] = o.gameObject;
        }
        var start = root.GetComponentInChildren<StartMarker>(true);
        var exit = root.GetComponentInChildren<ExitMarker>(true);
        d.start = start.Cell;
        d.exit = exit.Cell;
        return d;
    }
}
