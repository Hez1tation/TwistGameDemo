using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Runs the game on top of level objects built from Unity's own primitives (Cube/Sphere/Cylinder)
// that live in the scene under "Levels". The Start marker of the active level doubles as the
// player token, so no extra runtime-only visuals are generated.
public class StepMazeGame : MonoBehaviour
{
    const float StepTime = 0.11f;

    public Transform levelsRoot;

    class LevelInstance
    {
        public Transform root;
        public Vector3 origin;
        public HashSet<Vector2Int> walls = new HashSet<Vector2Int>();
        public Dictionary<Vector2Int, int> numbers = new Dictionary<Vector2Int, int>();
        public Dictionary<Vector2Int, char> operators = new Dictionary<Vector2Int, char>();
        public Dictionary<Vector2Int, GameObject> numberObjs = new Dictionary<Vector2Int, GameObject>();
        public Dictionary<Vector2Int, GameObject> operatorObjs = new Dictionary<Vector2Int, GameObject>();
        public Vector2Int start, exit;
        public int startSteps;
        public Transform player;
    }

    readonly List<LevelInstance> levels = new List<LevelInstance>();
    StepMazeModel model;
    LevelInstance current;
    int levelIndex;

    bool moving, levelComplete, gameComplete, dying;
    string message = "";
    float messageUntil;

    void Start()
    {
        if (levelsRoot == null)
        {
            var found = GameObject.Find("Levels");
            if (found != null) levelsRoot = found.transform;
        }
        if (levelsRoot == null)
        {
            Debug.LogError("StepMazeGame: no 'Levels' object found. Use Twist > Regenerate Demo Scene.");
            enabled = false;
            return;
        }

        foreach (var n in levelsRoot.GetComponentsInChildren<NumberTile>(true))
            Debug.Log($"[DIAG] scene NumberTile at {n.Cell} localPos={n.transform.localPosition} value={n.value}");

        foreach (Transform lvl in levelsRoot) levels.Add(BuildLevelInstance(lvl));
        LoadLevel(0);
    }

    LevelInstance BuildLevelInstance(Transform root)
    {
        var d = SceneLevelReader.Read(root);
        var inst = new LevelInstance
        {
            root = root,
            origin = root.position,
            walls = d.walls,
            numbers = d.numbers,
            operators = d.operators,
            numberObjs = d.numberObjs,
            operatorObjs = d.operatorObjs,
            start = d.start,
            exit = d.exit,
            player = root.GetComponentInChildren<StartMarker>(true).transform,
        };
        return inst;
    }

    void LoadLevel(int index)
    {
        StopAllCoroutines();
        moving = false; dying = false; levelComplete = false;
        levelIndex = index;

        for (int i = 0; i < levels.Count; i++) levels[i].root.gameObject.SetActive(i == index);
        current = levels[index];

        // Restore any tiles collected on a previous attempt at this level.
        current.numbers.Clear();
        current.operators.Clear();
        var namesN = new List<Vector2Int>(current.numberObjs.Keys);
        foreach (var c in namesN) { current.numberObjs[c].SetActive(true); }
        var namesO = new List<Vector2Int>(current.operatorObjs.Keys);
        foreach (var c in namesO) { current.operatorObjs[c].SetActive(true); }
        foreach (var kv in current.numberObjs) current.numbers[kv.Key] = kv.Value.GetComponent<NumberTile>().value;
        foreach (var kv in current.operatorObjs) current.operators[kv.Key] = kv.Value.GetComponent<OperatorTile>().op.ToChar();

        int startSteps = index < StepLevelData.StartSteps.Length ? StepLevelData.StartSteps[index] : 10;
        model = new StepMazeModel(current.walls, current.numbers, current.operators, current.start, current.exit, startSteps);
        current.player.position = GridUtil.WorldOf(current.origin, model.pos, current.player.position.y);

        FitCamera();
        ShowMessage(index < StepLevelData.Hints.Length ? StepLevelData.Hints[index] : "", 8f);
    }

    void FitCamera()
    {
        var cam = Camera.main;
        if (cam == null) { var go = new GameObject("Main Camera") { tag = "MainCamera" }; cam = go.AddComponent<Camera>(); }
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.07f, 0.07f, 0.1f);

        var rows = StepLevelData.Layouts[levelIndex];
        float w = rows[0].Length, h = rows.Length;
        float aspect = Mathf.Max(0.5f, (float)Screen.width / Screen.height);
        cam.orthographicSize = Mathf.Max(h / 2f + 2.2f, (w / 2f + 1f) / aspect);
        var center = current.origin + new Vector3((w - 1) / 2f, 0f, (h - 1) / 2f);
        cam.transform.position = new Vector3(center.x, 12f, center.z - 0.001f);
        cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    void ShowMessage(string text, float seconds = 3f) { message = text; messageUntil = Time.time + seconds; }

    void DrawTileLabel(Camera cam, Vector2Int cell, string text, float s, float sw, float sh, GUIStyle style)
    {
        Vector3 world = GridUtil.WorldOf(current.origin, cell, 0.35f);
        Vector3 screen = cam.WorldToScreenPoint(world);
        if (screen.z < 0f) return;
        float gx = screen.x / s;
        float gy = (Screen.height - screen.y) / s;
        GUI.Label(new Rect(gx - 20, gy - 14, 40, 28), text, style);
    }

    void Update()
    {
        if (dying) return;

        if (gameComplete)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) { gameComplete = false; LoadLevel(0); }
            return;
        }

        if (Input.GetKeyDown(KeyCode.R)) { LoadLevel(levelIndex); return; }

        if (levelComplete)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                if (levelIndex + 1 < levels.Count) LoadLevel(levelIndex + 1); else gameComplete = true;
            }
            return;
        }

        if (!moving)
        {
            Vector2Int dir = ReadDirection();
            if (dir != Vector2Int.zero) StartCoroutine(MoveRoutine(dir));
        }
    }

    static Vector2Int ReadDirection()
    {
        // The top-down camera looks straight down, so world +Z reads as "up" on screen.
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) return new Vector2Int(0, 1);
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) return new Vector2Int(0, -1);
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) return new Vector2Int(-1, 0);
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) return new Vector2Int(1, 0);
        return Vector2Int.zero;
    }

    IEnumerator MoveRoutine(Vector2Int dir)
    {
        moving = true;
        Vector3 a = current.player.position;
        bool moved = model.Step(dir, out char triggeredOp, out bool triggeredNum, out int numVal);
        if (!moved) { moving = false; yield break; }

        Vector3 b = GridUtil.WorldOf(current.origin, model.pos, a.y);
        float t = 0f;
        while (t < StepTime) { t += Time.deltaTime; current.player.position = Vector3.Lerp(a, b, Mathf.Clamp01(t / StepTime)); yield return null; }
        current.player.position = b;

        if (triggeredOp != '\0' && current.operatorObjs.TryGetValue(model.pos, out var opGo)) opGo.SetActive(false);
        if (triggeredNum && current.numberObjs.TryGetValue(model.pos, out var numGo)) numGo.SetActive(false);

        if (model.Won) { levelComplete = true; ShowMessage("Level complete! Press Space to continue.", 999f); moving = false; yield break; }
        if (model.Dead) { yield return StartCoroutine(DieAndReload()); yield break; }

        moving = false;
    }

    IEnumerator DieAndReload()
    {
        dying = true; moving = false;
        yield return new WaitForSeconds(1f);
        LoadLevel(levelIndex);
        ShowMessage("Ran out of steps! Level restarted.", 2.5f);
    }

    void OnGUI()
    {
        if (model == null) return;
        float s = Mathf.Max(1f, Screen.height / 540f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
        float sw = Screen.width / s, sh = Screen.height / s;

        var title = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        var body = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = new Color(0.85f, 0.85f, 0.9f) } };
        var center = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
        var big = new GUIStyle(title) { fontSize = 30, alignment = TextAnchor.MiddleCenter };

        var tileLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        var cam = Camera.main;
        if (cam != null)
        {
            foreach (var kv in model.numbers) DrawTileLabel(cam, kv.Key, kv.Value.ToString(), s, sw, sh, tileLabelStyle);
            foreach (var kv in model.operators)
            {
                string sym = kv.Value == '+' ? "+" : kv.Value == '-' ? "-" : "x";
                DrawTileLabel(cam, kv.Key, sym, s, sw, sh, tileLabelStyle);
            }
        }

        string levelName = levelIndex < StepLevelData.Names.Length ? StepLevelData.Names[levelIndex] : "Level " + (levelIndex + 1);
        GUI.Label(new Rect(14, 8, 500, 30), levelName, title);
        GUI.Label(new Rect(14, 36, 700, 22), "Move: WASD / Arrows    Restart: R", body);

        var stepsStyle = new GUIStyle(title) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(sw / 2 - 150, 6, 300, 36), "Steps left: " + model.steps, stepsStyle);
        string opName = !model.pendingOp.HasValue ? "none - grab an operator first"
            : model.pendingOp.Value == '+' ? "+" : model.pendingOp.Value == '-' ? "-" : "x";
        GUI.Label(new Rect(sw / 2 - 150, 40, 300, 22), "Pending operator: " + opName, center);

        if (dying)
        {
            var old = GUI.color;
            GUI.color = new Color(0.85f, 0.1f, 0.1f, 0.35f);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Label(new Rect(0, sh / 2 - 40, sw, 50), "Out of steps!", big);
            GUI.Label(new Rect(0, sh / 2 + 8, sw, 30), "Restarting level...", center);
        }
        else if (!string.IsNullOrEmpty(message) && Time.time < messageUntil)
        {
            GUI.Label(new Rect(20, sh - 70, sw - 40, 60), message, center);
        }

        if (levelComplete && !gameComplete)
        {
            float top = sh / 2 - 70;
            GUI.Box(new Rect(sw / 2 - 190, top, 380, 140), "");
            GUI.Label(new Rect(sw / 2 - 190, top + 10, 380, 36), "Level complete!", new GUIStyle(title) { alignment = TextAnchor.MiddleCenter });
            GUI.Label(new Rect(sw / 2 - 190, top + 55, 380, 24), "Steps remaining: " + model.steps, center);
            GUI.Label(new Rect(sw / 2 - 190, top + 90, 380, 24), "Space: next level    R: retry", center);
        }

        if (gameComplete)
        {
            GUI.Box(new Rect(sw / 2 - 220, 200, 440, 130), "");
            GUI.Label(new Rect(sw / 2 - 220, 215, 440, 40), "You beat the demo!", new GUIStyle(title) { alignment = TextAnchor.MiddleCenter });
            GUI.Label(new Rect(sw / 2 - 200, 260, 400, 50), "Press Space to play again.", center);
        }
    }
}
