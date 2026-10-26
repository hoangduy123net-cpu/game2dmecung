using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Game "Mê Cung Tìm Đường" - toàn bộ game được dựng bằng code, đồ họa pixel-art tạo từ PixelArt.cs.
/// Cách dùng: tạo Empty GameObject trong Scene rỗng, gắn script này rồi bấm Play.
/// Điều khiển: Mũi tên / WASD = di chuyển, H = gợi ý đường, R = chơi lại, Enter = tiếp tục.
/// </summary>
public class GameManager : MonoBehaviour
{
    enum State { Playing, LevelComplete, GameOver, Won }
    enum Kind { Key, Clock, Coin, Trap }

    class Pickup { public Kind kind; public GameObject go; public Vector3 basePos; }
    class Monster { public Vector2Int cell; public GameObject go; }

    [Header("Cấu hình")]
    public int maxLevel = 10;
    public int startLives = 3;
    public int startHints = 3;
    public float moveDelay = 0.12f;     // thời gian giữa 2 bước khi giữ phím
    public float monsterDelay = 0.45f;  // tốc độ quái vật
    public int chaseRange = 8;          // quái đuổi theo nếu cách người chơi <= số ô này
    public int fogRadius = 3;           // tầm nhìn khi có sương mù (màn >= 7)

    // ----- Trạng thái game -----
    State state;
    int level, lives, score, highScore, steps, hintsLeft;
    int keysCollected, keysNeeded;
    float timeLeft, timeMax;
    string message = "";
    string toast = "";
    float toastUntil;

    // ----- Dữ liệu màn chơi -----
    MazeData maze;
    Vector2Int startCell, exitCell, playerCell;
    Transform levelRoot;
    GameObject player, exitGo;
    SpriteRenderer playerSr;
    SpriteRenderer[,] fog;
    readonly Dictionary<Vector2Int, Pickup> pickups = new Dictionary<Vector2Int, Pickup>();
    readonly List<Monster> monsters = new List<Monster>();
    readonly List<GameObject> hintObjects = new List<GameObject>();
    readonly List<Vector2Int> keyCells = new List<Vector2Int>();
    float nextMoveTime, nextMonsterMove;

    Camera cam;
    static Sprite whiteSprite;
    GUIStyle hudStyle, centerStyle, smallStyle, bigStyle;

    // =====================================================================
    void Start()
    {
        PixelArt.Init();

        cam = Camera.main;
        if (cam == null)
        {
            var g = new GameObject("Main Camera");
            g.tag = "MainCamera";
            cam = g.AddComponent<Camera>();
        }
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.55f, 0.82f, 0.98f); // màu trời

        highScore = PlayerPrefs.GetInt("HighScore", 0);
        NewGame();
    }

    void NewGame()
    {
        lives = startLives;
        score = 0;
        hintsLeft = startHints;
        StartLevel(1);
    }

    // =====================================================================
    //  TẠO MÀN CHƠI
    // =====================================================================
    void StartLevel(int lvl)
    {
        ClearLevel();
        level = lvl;
        state = State.Playing;
        steps = 0;
        keysCollected = 0;
        message = "";

        int cells = 4 + level;                          // màn 1: 5x5 ... màn 10: 14x14
        maze = new MazeData(cells, cells);
        keysNeeded = 1 + (level - 1) / 3;               // 1,1,1,2,2,2,3,3,3,4
        timeLeft = Mathf.Max(30f, 20f + cells * cells * 0.8f - level * 2f);
        timeMax = timeLeft;

        // Nền trời + tường đất có cỏ
        for (int x = 0; x < maze.Width; x++)
        {
            for (int y = 0; y < maze.Height; y++)
            {
                var pos = new Vector3(x, y, 0);
                if (maze.Wall[x, y])
                {
                    bool wallAbove = y + 1 < maze.Height && maze.Wall[x, y + 1];
                    var set = wallAbove ? PixelArt.WallDirt : PixelArt.WallGrass;
                    Spawn("Wall", pos, Color.white, 0, 1f, set[Random.Range(0, set.Length)]);
                }
                else
                {
                    var sp = Random.value < 0.12f
                        ? PixelArt.FloorCloud[Random.Range(0, PixelArt.FloorCloud.Length)]
                        : PixelArt.FloorPlain[Random.Range(0, PixelArt.FloorPlain.Length)];
                    Spawn("Floor", pos, Color.white, -10, 1f, sp);
                }
            }
        }

        startCell = new Vector2Int(1, 1);
        exitCell = new Vector2Int(maze.Width - 2, maze.Height - 2);
        var free = maze.FreeCells();
        free.Remove(startCell);
        free.Remove(exitCell);
        Shuffle(free);

        // Cửa thoát (khóa → mở khi đủ chìa)
        exitGo = Spawn("Exit", ToWorld(exitCell), Color.white, 1, 1f, PixelArt.DoorLocked);

        // Chìa khóa
        keyCells.Clear();
        for (int i = 0; i < keysNeeded; i++)
        {
            var c = Take(free);
            keyCells.Add(c);
            AddPickup(Kind.Key, c);
        }

        // Đồng hồ cộng thời gian, đồng xu cộng điểm
        for (int i = 0; i < Mathf.Max(1, level / 2); i++) AddPickup(Kind.Clock, Take(free));
        for (int i = 0; i < 3 + level; i++) AddPickup(Kind.Coin, Take(free));

        // Bẫy (từ màn 4) - đảm bảo bẫy không chặn hết đường tới cửa/chìa khóa
        int trapCount = level >= 4 ? level - 2 : 0;
        var blocked = new HashSet<Vector2Int>();
        for (int i = 0; i < trapCount; i++)
        {
            for (int tries = 0; tries < 30 && free.Count > 0; tries++)
            {
                var c = Take(free);
                blocked.Add(c);
                if (AllReachable(blocked))
                {
                    AddPickup(Kind.Trap, c);
                    break;
                }
                blocked.Remove(c);
            }
        }

        // Quái vật tuần tra (từ màn 7)
        int monsterCount = level >= 7 ? level - 6 : 0;
        for (int i = 0; i < monsterCount; i++)
        {
            var c = TakeFarFrom(free, startCell, 8);
            var m = new Monster { cell = c };
            m.go = Spawn("Monster", ToWorld(c), Color.white, 5, 1f, PixelArt.Monster);
            monsters.Add(m);
        }

        // Người chơi
        playerCell = startCell;
        player = Spawn("Player", ToWorld(playerCell), Color.white, 6, 1f, PixelArt.Player);
        playerSr = player.GetComponent<SpriteRenderer>();

        // Sương mù (từ màn 7)
        fog = null;
        if (level >= 7)
        {
            fog = new SpriteRenderer[maze.Width, maze.Height];
            for (int x = 0; x < maze.Width; x++)
                for (int y = 0; y < maze.Height; y++)
                    fog[x, y] = Spawn("Fog", new Vector3(x, y, 0), Color.black, 20).GetComponent<SpriteRenderer>();
        }
        UpdateFog();

        // Camera: chừa chỗ phía trên cho HUD
        cam.transform.position = new Vector3((maze.Width - 1) / 2f, (maze.Height - 1) / 2f + 1f, -10);
        float h = maze.Height / 2f + 2.6f;
        float w = (maze.Width / 2f + 1f) / cam.aspect;
        cam.orthographicSize = Mathf.Max(h, w);

        Toast("Màn " + level + ": thu " + keysNeeded + " chìa khóa rồi đến cửa thoát!");
    }

    bool AllReachable(HashSet<Vector2Int> blocked)
    {
        if (maze.FindPath(startCell, exitCell, blocked).Count == 0) return false;
        foreach (var k in keyCells)
            if (maze.FindPath(startCell, k, blocked).Count == 0) return false;
        return true;
    }

    void ClearLevel()
    {
        CancelInvoke();
        if (levelRoot != null) Destroy(levelRoot.gameObject);
        levelRoot = new GameObject("Level").transform;
        pickups.Clear();
        monsters.Clear();
        hintObjects.Clear();
    }

    // =====================================================================
    //  VÒNG LẶP GAME
    // =====================================================================
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)) { NewGame(); return; }

        AnimatePickups();

        if (state == State.Playing)
        {
            HandleInput();
            if (state != State.Playing) return;

            if (Input.GetKeyDown(KeyCode.H)) ShowHint();

            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0f) { timeLeft = 0f; EndGame("HẾT GIỜ!"); return; }

            UpdateMonsters();
        }
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (state == State.LevelComplete) StartLevel(level + 1);
            else NewGame();
        }
    }

    // Vật phẩm nhấp nhô nhẹ cho sinh động
    void AnimatePickups()
    {
        foreach (var kv in pickups)
        {
            var p = kv.Value;
            if (p.kind == Kind.Trap || p.go == null) continue;
            float bob = Mathf.Sin(Time.time * 4f + p.basePos.x + p.basePos.y) * 0.05f;
            p.go.transform.position = p.basePos + Vector3.up * bob;
        }
    }

    // ----- Di chuyển người chơi -----
    void HandleInput()
    {
        if (Time.time < nextMoveTime) return;

        Vector2Int d = Vector2Int.zero;
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) d = Vector2Int.up;
        else if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) d = Vector2Int.down;
        else if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) d = Vector2Int.left;
        else if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) d = Vector2Int.right;
        if (d == Vector2Int.zero) return;

        nextMoveTime = Time.time + moveDelay;
        steps++; // đâm vào tường cũng tính là một bước
        if (d.x != 0) playerSr.flipX = d.x < 0; // quay mặt theo hướng đi

        var target = playerCell + d;
        if (!maze.IsFree(target)) return;

        playerCell = target;
        player.transform.position = ToWorld(playerCell);
        OnPlayerMoved();
    }

    void OnPlayerMoved()
    {
        UpdateFog();

        // Quái vật
        foreach (var m in monsters)
            if (m.cell == playerCell) { LoseLife("Quái vật bắt được bạn!"); return; }

        // Vật phẩm / bẫy
        if (pickups.TryGetValue(playerCell, out var p))
        {
            switch (p.kind)
            {
                case Kind.Key:
                    keysCollected++;
                    RemovePickup(playerCell);
                    Toast("Nhặt chìa khóa " + keysCollected + "/" + keysNeeded);
                    if (keysCollected >= keysNeeded)
                        exitGo.GetComponent<SpriteRenderer>().sprite = PixelArt.DoorOpen;
                    break;
                case Kind.Clock:
                    timeLeft += 10f;
                    timeMax = Mathf.Max(timeMax, timeLeft);
                    RemovePickup(playerCell);
                    Toast("+10 giây");
                    break;
                case Kind.Coin:
                    score += 10;
                    RemovePickup(playerCell);
                    break;
                case Kind.Trap:
                    LoseLife("Dính bẫy!");
                    return;
            }
        }

        // Cửa thoát
        if (playerCell == exitCell)
        {
            if (keysCollected >= keysNeeded) CompleteLevel();
            else Toast("Cần thêm " + (keysNeeded - keysCollected) + " chìa khóa!");
        }
    }

    // ----- Quái vật: đuổi theo bằng BFS khi ở gần, ngược lại đi tuần ngẫu nhiên -----
    void UpdateMonsters()
    {
        if (monsters.Count == 0 || Time.time < nextMonsterMove) return;
        nextMonsterMove = Time.time + monsterDelay;

        foreach (var m in monsters)
        {
            var path = maze.FindPath(m.cell, playerCell);
            if (path.Count > 0 && path.Count <= chaseRange)
            {
                m.cell = path[0];
            }
            else
            {
                var options = new List<Vector2Int>();
                foreach (var d in MazeData.Dirs)
                    if (maze.IsFree(m.cell + d)) options.Add(m.cell + d);
                if (options.Count > 0) m.cell = options[Random.Range(0, options.Count)];
            }
            m.go.transform.position = ToWorld(m.cell);

            if (m.cell == playerCell)
            {
                LoseLife("Quái vật bắt được bạn!");
                if (state != State.Playing) return;
            }
        }
    }

    // ----- Gợi ý đường (phím H): hiện đường ngắn nhất tới cửa thoát 3 giây -----
    void ShowHint()
    {
        if (hintsLeft <= 0) { Toast("Hết lượt gợi ý!"); return; }
        hintsLeft--;
        score = Mathf.Max(0, score - 30);
        ClearHint();

        foreach (var c in maze.FindPath(playerCell, exitCell))
            hintObjects.Add(Spawn("Hint", ToWorld(c), new Color(1f, 0.95f, 0.3f, 0.75f), 25, 0.25f));

        CancelInvoke(nameof(ClearHint));
        Invoke(nameof(ClearHint), 3f);
    }

    void ClearHint()
    {
        foreach (var h in hintObjects) if (h != null) Destroy(h);
        hintObjects.Clear();
    }

    // =====================================================================
    //  THẮNG / THUA / ĐIỂM
    // =====================================================================
    void LoseLife(string reason)
    {
        lives--;
        score = Mathf.Max(0, score - 20);
        if (lives <= 0) { EndGame("HẾT MẠNG!"); return; }

        Toast(reason + " (còn " + lives + " mạng)");
        playerCell = startCell;
        player.transform.position = ToWorld(playerCell);
        UpdateFog();
    }

    void CompleteLevel()
    {
        int bonus = level * 100 + Mathf.CeilToInt(timeLeft) * 2 + Mathf.Max(0, 200 - steps);
        score += bonus;
        SaveHighScore();

        if (level >= maxLevel)
        {
            state = State.Won;
            message = "CHIẾN THẮNG!\nTổng điểm: " + score + "\nEnter để chơi lại";
        }
        else
        {
            state = State.LevelComplete;
            message = "Hoàn thành màn " + level + "!  +" + bonus + " điểm\nEnter để chơi tiếp";
        }
    }

    void EndGame(string title)
    {
        state = State.GameOver;
        SaveHighScore();
        message = title + "\nĐiểm: " + score + "\nEnter hoặc R để chơi lại";
    }

    void SaveHighScore()
    {
        if (score > highScore)
        {
            highScore = score;
            PlayerPrefs.SetInt("HighScore", highScore);
            PlayerPrefs.Save();
        }
    }

    // =====================================================================
    //  GIAO DIỆN (HUD) kiểu game pixel: ô viền cam, tim, đồng xu, chìa khóa
    // =====================================================================
    void OnGUI()
    {
        if (hudStyle == null)
        {
            hudStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Overflow };
            centerStyle = new GUIStyle(hudStyle) { alignment = TextAnchor.MiddleCenter };
            smallStyle = new GUIStyle(hudStyle) { alignment = TextAnchor.MiddleRight };
            bigStyle = new GUIStyle(hudStyle) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
        }

        float s = Screen.height / 540f;   // hệ số co giãn theo độ phân giải
        float m = 10f * s;
        hudStyle.fontSize = Mathf.RoundToInt(18f * s);
        centerStyle.fontSize = Mathf.RoundToInt(20f * s);
        smallStyle.fontSize = Mathf.RoundToInt(14f * s);
        bigStyle.fontSize = Mathf.RoundToInt(30f * s);
        hudStyle.normal.textColor = centerStyle.normal.textColor = smallStyle.normal.textColor = bigStyle.normal.textColor = Color.white;

        var panel = new Color(0.17f, 0.12f, 0.20f, 0.92f);
        var edge = new Color(0.95f, 0.60f, 0.20f);

        // --- Trái trên: tim (mạng) + thanh thời gian ---
        Box(new Rect(m, m, 210f * s, 66f * s), panel, edge, 3f * s);
        for (int i = 0; i < startLives; i++)
        {
            GUI.color = i < lives ? Color.white : new Color(0.25f, 0.25f, 0.25f, 0.8f);
            GUI.DrawTexture(new Rect(m + 10f * s + i * 34f * s, m + 8f * s, 32f * s, 32f * s), PixelArt.Heart.texture);
        }
        GUI.color = Color.white;
        Text(new Rect(m + 118f * s, m + 8f * s, 90f * s, 32f * s), Mathf.CeilToInt(timeLeft) + " giây", hudStyle);

        float frac = Mathf.Clamp01(timeLeft / Mathf.Max(1f, timeMax));
        Fill(new Rect(m + 10f * s, m + 46f * s, 190f * s, 12f * s), new Color(0.05f, 0.03f, 0.07f));
        Fill(new Rect(m + 12f * s, m + 48f * s, 186f * s * frac, 8f * s),
             frac > 0.25f ? new Color(1f, 0.65f, 0.15f) : new Color(0.9f, 0.2f, 0.2f));

        // --- Giữa trên: băng rôn xanh ghi số màn ---
        var ribbon = new Rect(Screen.width / 2f - 85f * s, m, 170f * s, 38f * s);
        Box(ribbon, new Color(0.25f, 0.52f, 0.20f), new Color(0.10f, 0.25f, 0.08f), 3f * s);
        Text(ribbon, "Màn " + level + "/" + maxLevel, centerStyle);

        // --- Phải trên: chìa khóa + điểm ---
        float pw = 118f * s, ph = 40f * s;
        var coinR = new Rect(Screen.width - m - pw, m, pw, ph);
        var keyR = new Rect(coinR.x - m - pw, m, pw, ph);
        Box(coinR, panel, edge, 3f * s);
        GUI.DrawTexture(new Rect(coinR.x + 6f * s, coinR.y + 4f * s, 32f * s, 32f * s), PixelArt.Coin.texture);
        Text(new Rect(coinR.x + 44f * s, coinR.y, pw - 48f * s, ph), score.ToString(), hudStyle);
        Box(keyR, panel, edge, 3f * s);
        GUI.DrawTexture(new Rect(keyR.x + 6f * s, keyR.y + 4f * s, 32f * s, 32f * s), PixelArt.Key.texture);
        Text(new Rect(keyR.x + 44f * s, keyR.y, pw - 48f * s, ph), keysCollected + "/" + keysNeeded, hudStyle);
        Text(new Rect(keyR.x, m + ph + 4f * s, 2f * pw + m, 22f * s), "Kỷ lục: " + highScore, smallStyle);

        // --- Dưới: gợi ý, số bước, thông báo ---
        Box(new Rect(m, Screen.height - m - 38f * s, 190f * s, 38f * s), panel, edge, 3f * s);
        Text(new Rect(m + 12f * s, Screen.height - m - 38f * s, 180f * s, 38f * s), "Gợi ý (H): " + hintsLeft, hudStyle);
        Box(new Rect(Screen.width - m - 130f * s, Screen.height - m - 38f * s, 130f * s, 38f * s), panel, edge, 3f * s);
        Text(new Rect(Screen.width - m - 118f * s, Screen.height - m - 38f * s, 120f * s, 38f * s), "Bước: " + steps, hudStyle);

        if (Time.time < toastUntil)
        {
            var tr = new Rect(Screen.width / 2f - 260f * s, Screen.height - m - 38f * s, 520f * s, 38f * s);
            Box(tr, panel, edge, 3f * s);
            Text(tr, toast, centerStyle);
        }

        // --- Màn hình kết thúc màn / thua / thắng ---
        if (state != State.Playing)
        {
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, 0.6f));
            var r = new Rect(Screen.width / 2f - 280f * s, Screen.height / 2f - 130f * s, 560f * s, 260f * s);
            Box(r, panel, edge, 4f * s);
            Text(r, message, bigStyle);
        }
    }

    void Box(Rect r, Color fill, Color border, float b)
    {
        Fill(r, border);
        Fill(new Rect(r.x + b, r.y + b, r.width - 2f * b, r.height - 2f * b), fill);
    }

    void Fill(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    void Text(Rect r, string t, GUIStyle st)
    {
        var c = st.normal.textColor;
        st.normal.textColor = Color.black;
        GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), t, st);
        st.normal.textColor = c;
        GUI.Label(r, t, st);
    }

    // =====================================================================
    //  HÀM TIỆN ÍCH
    // =====================================================================
    void Toast(string t) { toast = t; toastUntil = Time.time + 2.5f; }

    static Vector3 ToWorld(Vector2Int c) { return new Vector3(c.x, c.y, 0); }

    static Sprite WhiteSprite()
    {
        if (whiteSprite == null)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
        return whiteSprite;
    }

    GameObject Spawn(string objName, Vector3 pos, Color color, int order, float size = 1f, Sprite sprite = null)
    {
        var go = new GameObject(objName);
        go.transform.SetParent(levelRoot, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(size, size, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite != null ? sprite : WhiteSprite();
        sr.color = color;
        sr.sortingOrder = order;
        return go;
    }

    void AddPickup(Kind kind, Vector2Int cell)
    {
        Sprite sp;
        switch (kind)
        {
            case Kind.Key: sp = PixelArt.Key; break;
            case Kind.Clock: sp = PixelArt.Clock; break;
            case Kind.Coin: sp = PixelArt.Coin; break;
            default: sp = PixelArt.Trap; break;
        }
        var pos = ToWorld(cell);
        pickups[cell] = new Pickup { kind = kind, basePos = pos, go = Spawn(kind.ToString(), pos, Color.white, 2, 1f, sp) };
    }

    void RemovePickup(Vector2Int cell)
    {
        if (pickups.TryGetValue(cell, out var p))
        {
            Destroy(p.go);
            pickups.Remove(cell);
        }
    }

    void UpdateFog()
    {
        if (fog == null) return;
        for (int x = 0; x < maze.Width; x++)
        {
            for (int y = 0; y < maze.Height; y++)
            {
                float d = Vector2.Distance(new Vector2(x, y), playerCell);
                float a = Mathf.Clamp01((d - fogRadius) / 1.5f);
                fog[x, y].color = new Color(0, 0, 0, a * 0.97f);
            }
        }
    }

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }

    static Vector2Int Take(List<Vector2Int> free)
    {
        var c = free[free.Count - 1];
        free.RemoveAt(free.Count - 1);
        return c;
    }

    static Vector2Int TakeFarFrom(List<Vector2Int> free, Vector2Int from, int minDist)
    {
        for (int i = free.Count - 1; i >= 0; i--)
        {
            var c = free[i];
            if (Mathf.Abs(c.x - from.x) + Mathf.Abs(c.y - from.y) >= minDist)
            {
                free.RemoveAt(i);
                return c;
            }
        }
        return Take(free);
    }
}
