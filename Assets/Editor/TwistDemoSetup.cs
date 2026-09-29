using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TwistDemoSetup
{
    const string ScenePath = "Assets/Scenes/Main.unity";

    static readonly Color WallColor = new Color(0.36f, 0.38f, 0.48f);
    static readonly Color FloorColor = new Color(0.16f, 0.17f, 0.22f);
    static readonly Color StartColor = Color.white;
    static readonly Color ExitColor = new Color(1f, 0.85f, 0.3f);
    static readonly Color NumberColor = new Color(0.3f, 0.65f, 0.95f);
    static readonly Color OperatorColor = new Color(0.95f, 0.45f, 0.25f);

    [MenuItem("Twist/Regenerate Demo Scene (overwrites edits)")]
    public static void CreateScene()
    {
        if (!Application.isBatchMode &&
            !EditorUtility.DisplayDialog("Regenerate scene",
                "This rebuilds Assets/Scenes/Main.unity from the built-in level layouts and discards any manual edits to it. Continue?",
                "Regenerate", "Cancel"))
            return;

        if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");

        // Unity's batchmode scene serializer has proven unreliable when saving many
        // near-identical procedurally-created primitives in one pass: objects occasionally
        // vanish silently on save (a known class of Editor bug, not something this script can
        // prevent outright). So build+save, then verify the result against what we expect, and
        // rebuild from scratch if anything is missing - this guarantees a correct final scene.
        const int maxAttempts = 15;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var levels = BuildLevels();

            var go = new GameObject("StepMazeGame");
            var game = go.AddComponent<StepMazeGame>();
            game.levelsRoot = levels;

            // Everything renders Unlit, so the scene light has nothing to do - disable its
            // shadows rather than leave it doing pointless shadow-map work.
            var light = Object.FindFirstObjectByType<Light>();
            if (light != null) light.shadows = LightShadows.None;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            var reloaded = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            string problem = VerifyScene(reloaded);
            if (problem == null)
            {
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
                Debug.Log($"Created {ScenePath} (verified on attempt {attempt})");
                return;
            }
            Debug.LogWarning($"Scene verification failed on attempt {attempt}: {problem}. Rebuilding...");
        }

        Debug.LogError($"Failed to build a verified scene after {maxAttempts} attempts.");
        if (Application.isBatchMode) EditorApplication.Exit(1);
    }

    // Returns null if the saved+reloaded scene matches StepLevelData exactly, otherwise a
    // description of what's wrong.
    static string VerifyScene(UnityEngine.SceneManagement.Scene scene)
    {
        var levelsGo = GameObject.Find("Levels");
        if (levelsGo == null) return "no 'Levels' object";
        if (levelsGo.transform.childCount != StepLevelData.Layouts.Length)
            return $"expected {StepLevelData.Layouts.Length} levels, found {levelsGo.transform.childCount}";

        for (int i = 0; i < StepLevelData.Layouts.Length; i++)
        {
            var levelChild = levelsGo.transform.GetChild(i);
            if (levelChild.GetComponentInChildren<StartMarker>(true) == null) return $"L{i + 1}: missing Start marker";
            if (levelChild.GetComponentInChildren<ExitMarker>(true) == null) return $"L{i + 1}: missing Exit marker";

            var d = SceneLevelReader.Read(levelChild);
            var rows = StepLevelData.Layouts[i];
            int expectedWalls = 0;
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[y].Length; x++)
                    if (rows[y][x] == '#') expectedWalls++;
            if (d.walls.Count != expectedWalls) return $"L{i + 1}: expected {expectedWalls} walls, found {d.walls.Count}";

            if (d.numbers.Count != StepLevelData.Numbers[i].Length)
                return $"L{i + 1}: expected {StepLevelData.Numbers[i].Length} numbers, found {d.numbers.Count}";
            foreach (var (x, y, v) in StepLevelData.Numbers[i])
                if (!d.numbers.TryGetValue(new Vector2Int(x, y), out int got) || got != v)
                    return $"L{i + 1}: number at ({x},{y}) missing or wrong (expected {v})";

            if (d.operators.Count != StepLevelData.Operators[i].Length)
                return $"L{i + 1}: expected {StepLevelData.Operators[i].Length} operators, found {d.operators.Count}";
            foreach (var (x, y, op) in StepLevelData.Operators[i])
                if (!d.operators.TryGetValue(new Vector2Int(x, y), out char got) || got != op.ToChar())
                    return $"L{i + 1}: operator at ({x},{y}) missing or wrong";
        }
        return null;
    }

    // Builds a "Levels" object with one child per level, made entirely of Unity primitives
    // (Cube/Sphere/Cylinder/Plane) so designers can select and tweak pieces in the Scene view.
    public static Transform BuildLevels()
    {
        var root = new GameObject("Levels").transform;
        float xGap = 0f;
        for (int i = 0; i < StepLevelData.Layouts.Length; i++)
        {
            var rows = StepLevelData.Layouts[i];
            var level = new GameObject(StepLevelData.Names[i]).transform;
            level.SetParent(root, false);
            level.position = new Vector3(xGap, 0f, 0f);
            xGap += rows[0].Length + 4;

            float w = rows[0].Length, h = rows.Length;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(level, false);
            floor.transform.localPosition = new Vector3((w - 1) / 2f, -0.01f, (h - 1) / 2f);
            floor.transform.localScale = new Vector3(w / 10f, 1f, h / 10f);
            SetColor(floor, FloorColor);
            floor.GetComponent<Collider>().enabled = false;

            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[y].Length; x++)
                    if (rows[y][x] == '#') AddWall(level, x, y);

            foreach (var (x, y, value) in StepLevelData.Numbers[i]) AddNumber(level, x, y, value);
            foreach (var (x, y, op) in StepLevelData.Operators[i]) AddOperator(level, x, y, op);

            var startPos = FindChar(rows, 'P');
            var exitPos = FindChar(rows, 'E');
            AddStart(level, startPos.x, startPos.y);
            AddExit(level, exitPos.x, exitPos.y);
        }
        return root;
    }

    static Vector2Int FindChar(string[] rows, char c)
    {
        for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < rows[y].Length; x++)
                if (rows[y][x] == c) return new Vector2Int(x, y);
        return Vector2Int.zero;
    }

    static void AddWall(Transform parent, int x, int y)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = $"Wall ({x},{y})";
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0.5f, y);
        SetColor(go, WallColor);
        go.AddComponent<WallTile>();
        Undo.RegisterCreatedObjectUndo(go, "Create Wall");
        EditorUtility.SetDirty(go);
    }

    static void AddStart(Transform parent, int x, int y)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = $"Start (Player) @ ({x},{y})";
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0.4f, y);
        go.transform.localScale = Vector3.one * 0.7f;
        SetColor(go, StartColor);
        go.GetComponent<Collider>().enabled = false;
        go.AddComponent<StartMarker>();
        Undo.RegisterCreatedObjectUndo(go, "Create Start");
        EditorUtility.SetDirty(go);
    }

    static void AddExit(Transform parent, int x, int y)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = $"Exit @ ({x},{y})";
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0.05f, y);
        go.transform.localScale = new Vector3(0.8f, 0.05f, 0.8f);
        SetColor(go, ExitColor);
        go.GetComponent<Collider>().enabled = false;
        go.AddComponent<ExitMarker>();
        Undo.RegisterCreatedObjectUndo(go, "Create Exit");
        EditorUtility.SetDirty(go);
    }

    // Number/operator values are shown as OnGUI overlays by StepMazeGame (computed from these
    // components at runtime) rather than as child TextMesh objects - Unity's scene serializer
    // was unreliable when saving many near-identical parent+child primitive combos in one batch.
    static void AddNumber(Transform parent, int x, int y, int value)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = $"Number ({value}) @ ({x},{y})";
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0.35f, y);
        go.transform.localScale = Vector3.one * 0.55f;
        SetColor(go, NumberColor);
        go.GetComponent<Collider>().enabled = false;
        var tile = go.AddComponent<NumberTile>();
        tile.value = value;
        Undo.RegisterCreatedObjectUndo(go, "Create Number");
        EditorUtility.SetDirty(go);
    }

    static void AddOperator(Transform parent, int x, int y, MathOp op)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = $"Operator ({op.Symbol()}) @ ({x},{y})";
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0.3f, y);
        go.transform.localScale = Vector3.one * 0.55f;
        go.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        SetColor(go, OperatorColor);
        go.GetComponent<Collider>().enabled = false;
        var tile = go.AddComponent<OperatorTile>();
        tile.op = op;
        Undo.RegisterCreatedObjectUndo(go, "Create Operator");
        EditorUtility.SetDirty(go);
    }

    static Shader cachedShader;

    static void SetColor(GameObject go, Color c)
    {
        // Flat, unlit color: this is a mechanics-first prototype, not a lighting showcase -
        // no shading/specular/shadow gradients to distract from the grid state itself.
        if (cachedShader == null)
            cachedShader = Shader.Find("Unlit/Color") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
        var mat = new Material(cachedShader) { color = c };
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }
}
