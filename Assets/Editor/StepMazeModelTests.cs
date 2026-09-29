using System.Collections.Generic;
using UnityEngine;

// Replays known-good move sequences (and known-bad ones) against the real StepMazeModel,
// run headlessly via -executeMethod StepMazeModelTests.Run
public static class StepMazeModelTests
{
    // Level 1: straight corridor, moving right the whole way.
    static readonly Vector2Int[] L1_Moves = Right(12);

    // Level 2: 4 right, down, down (operator), up, up, 8 right (matches the branch layout).
    static readonly Vector2Int[] L2_Moves = Concat(Right(4), Down(2), Up(2), Right(8));

    static Vector2Int[] Right(int n) => Repeat(new Vector2Int(1, 0), n);
    static Vector2Int[] Left(int n) => Repeat(new Vector2Int(-1, 0), n);
    static Vector2Int[] Down(int n) => Repeat(new Vector2Int(0, 1), n);
    static Vector2Int[] Up(int n) => Repeat(new Vector2Int(0, -1), n);
    static Vector2Int[] Repeat(Vector2Int v, int n) { var a = new Vector2Int[n]; for (int i = 0; i < n; i++) a[i] = v; return a; }
    static Vector2Int[] Concat(params Vector2Int[][] parts)
    {
        var list = new List<Vector2Int>();
        foreach (var p in parts) list.AddRange(p);
        return list.ToArray();
    }

    public static void Run()
    {
        int failures = 0;

        UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
        var levelsRoot = TwistDemoSetup.BuildLevels();

        // --- Scene round-trip: what's built must match StepLevelData exactly ---
        for (int i = 0; i < StepLevelData.Layouts.Length; i++)
        {
            var d = SceneLevelReader.Read(levelsRoot.GetChild(i));
            var rows = StepLevelData.Layouts[i];
            bool wallsMatch = true;
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[y].Length; x++)
                {
                    bool expectWall = rows[y][x] == '#';
                    bool isWall = d.walls.Contains(new Vector2Int(x, y));
                    if (expectWall != isWall) wallsMatch = false;
                }
            bool numsMatch = d.numbers.Count == StepLevelData.Numbers[i].Length;
            foreach (var (x, y, v) in StepLevelData.Numbers[i])
                if (!d.numbers.TryGetValue(new Vector2Int(x, y), out int got) || got != v) numsMatch = false;
            bool opsMatch = d.operators.Count == StepLevelData.Operators[i].Length;
            foreach (var (x, y, op) in StepLevelData.Operators[i])
                if (!d.operators.TryGetValue(new Vector2Int(x, y), out char got) || got != op.ToChar()) opsMatch = false;

            bool ok = wallsMatch && numsMatch && opsMatch;
            Debug.Log($"TEST scene round-trip L{i + 1}: {(ok ? "PASS" : "FAIL")}");
            if (!ok) failures++;
        }

        // --- Level 1: fails by exactly 1 step without any pickups (hypothetical baseline - in
        // real play the operator/number pairs sit on the only path, so this simulates "what if
        // they had no effect at all") ---
        failures += Check("L1 fails by 1 step with zero pickups",
            RunSim(0, StepLevelData.StartSteps[0], L1_Moves, stripTiles: true) == null);

        // --- Level 1: solvable, generous surplus when collecting every operator/number pair on the direct path ---
        {
            int? final = RunSim(0, StepLevelData.StartSteps[0], L1_Moves, stripTiles: false);
            bool ok = final.HasValue && final.Value == 8;
            Debug.Log($"TEST L1 solved with surplus 8 (generous): {(ok ? "PASS" : "FAIL")} (got {final})");
            if (!ok) failures++;
        }

        // --- Level 2: direct route alone (no detour) fails ---
        failures += Check("L2 direct route (no detour) fails",
            RunSim(1, StepLevelData.StartSteps[1], Right(12), stripTiles: true) == null);

        // --- Level 2: detour WITHOUT the operator (number alone does nothing now) still fails ---
        {
            var moves = Concat(Right(4), Down(2), Up(2), Right(8));
            int? final = RunSimNoOperators(1, StepLevelData.StartSteps[1], moves);
            bool ok = final == null;
            Debug.Log($"TEST L2 detour without the x operator fails: {(ok ? "PASS" : "FAIL")}");
            if (!ok) failures++;
        }

        // --- Level 2: using the x combo solves it with a 4-step surplus ---
        {
            int? final = RunSim(1, StepLevelData.StartSteps[1], L2_Moves, stripTiles: false);
            bool ok = final.HasValue && final.Value == 4;
            Debug.Log($"TEST L2 solved via x combo with surplus 4: {(ok ? "PASS" : "FAIL")} (got {final})");
            if (!ok) failures++;
        }

        // --- Core rule: a number with no pending operator does nothing and stays uncollected ---
        {
            var walls = new HashSet<Vector2Int>();
            var numbers = new Dictionary<Vector2Int, int> { { new Vector2Int(1, 0), 100 } };
            var operators = new Dictionary<Vector2Int, char>();
            var model = new StepMazeModel(walls, numbers, operators, Vector2Int.zero, new Vector2Int(5, 0), 10);
            model.Step(new Vector2Int(1, 0), out _, out bool triggered, out _);
            bool ok = !triggered && model.steps == 9 && model.numbers.ContainsKey(new Vector2Int(1, 0));
            Debug.Log($"TEST number with no operator does nothing and stays: {(ok ? "PASS" : "FAIL")} (steps={model.steps})");
            if (!ok) failures++;
        }

        // --- Core rule: pendingOp is consumed - a second number needs a fresh operator ---
        {
            var walls = new HashSet<Vector2Int>();
            var operators = new Dictionary<Vector2Int, char> { { new Vector2Int(1, 0), '+' } };
            var numbers = new Dictionary<Vector2Int, int> { { new Vector2Int(2, 0), 5 }, { new Vector2Int(3, 0), 5 } };
            var model = new StepMazeModel(walls, numbers, operators, Vector2Int.zero, new Vector2Int(9, 0), 20);
            model.Step(new Vector2Int(1, 0), out _, out _, out _); // grab +
            model.Step(new Vector2Int(1, 0), out _, out bool firstApplied, out _); // 18 + 5 = 23
            model.Step(new Vector2Int(1, 0), out _, out bool secondApplied, out _); // no operator left: no-op
            bool ok = firstApplied && !secondApplied && model.numbers.ContainsKey(new Vector2Int(3, 0));
            Debug.Log($"TEST pending operator consumed after one use: {(ok ? "PASS" : "FAIL")} (steps={model.steps})");
            if (!ok) failures++;
        }

        Debug.Log(failures == 0 ? "ALL TESTS PASSED" : $"{failures} TEST(S) FAILED");
        UnityEditor.EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    static int Check(string label, bool condition)
    {
        Debug.Log($"TEST {label}: {(condition ? "PASS" : "FAIL")}");
        return condition ? 0 : 1;
    }

    // Runs the given moves against a fresh model for the given level; returns final steps if it
    // reached the exit, or null if it died or never reached the exit.
    static int? RunSim(int levelIndex, int startSteps, Vector2Int[] moves, bool stripTiles)
    {
        var levelsRoot = TwistDemoSetup.BuildLevels();
        var d = SceneLevelReader.Read(levelsRoot.GetChild(levelIndex));
        var numbers = stripTiles ? new Dictionary<Vector2Int, int>() : d.numbers;
        var operators = stripTiles ? new Dictionary<Vector2Int, char>() : d.operators;
        var model = new StepMazeModel(d.walls, numbers, operators, d.start, d.exit, startSteps);
        foreach (var dir in moves)
        {
            if (!model.Step(dir, out _, out _, out _)) return null;
            if (model.Won) return model.steps;
            if (model.Dead) return null;
        }
        return model.Won ? model.steps : (int?)null;
    }

    static int? RunSimNoOperators(int levelIndex, int startSteps, Vector2Int[] moves)
    {
        var levelsRoot = TwistDemoSetup.BuildLevels();
        var d = SceneLevelReader.Read(levelsRoot.GetChild(levelIndex));
        var model = new StepMazeModel(d.walls, d.numbers, new Dictionary<Vector2Int, char>(), d.start, d.exit, startSteps);
        foreach (var dir in moves)
        {
            if (!model.Step(dir, out _, out _, out _)) return null;
            if (model.Won) return model.steps;
            if (model.Dead) return null;
        }
        return model.Won ? model.steps : (int?)null;
    }
}
