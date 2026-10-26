using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dữ liệu mê cung: sinh mê cung bằng DFS (Recursive Backtracking)
/// và tìm đường ngắn nhất bằng BFS.
/// Lưới có kích thước (2*cells+1): ô lẻ-lẻ là phòng, ô còn lại là tường/lối đi.
/// </summary>
public class MazeData
{
    public static readonly Vector2Int[] Dirs =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    public readonly int Width;
    public readonly int Height;
    public readonly bool[,] Wall; // true = tường

    public MazeData(int cellsX, int cellsY, float loopChance = 0.05f)
    {
        Width = cellsX * 2 + 1;
        Height = cellsY * 2 + 1;
        Wall = new bool[Width, Height];
        Generate();
        OpenLoops(loopChance);
    }

    // ---------- Sinh mê cung (DFS dùng stack) ----------
    void Generate()
    {
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                Wall[x, y] = true;

        var stack = new Stack<Vector2Int>();
        var start = new Vector2Int(1, 1);
        Wall[start.x, start.y] = false;
        stack.Push(start);

        while (stack.Count > 0)
        {
            var cur = stack.Peek();
            var options = new List<Vector2Int>();
            foreach (var d in Dirs)
            {
                var n = cur + d * 2;
                if (InInterior(n) && Wall[n.x, n.y]) options.Add(d);
            }

            if (options.Count == 0) { stack.Pop(); continue; }

            var pick = options[Random.Range(0, options.Count)];
            Wall[cur.x + pick.x, cur.y + pick.y] = false; // phá tường giữa
            var next = cur + pick * 2;
            Wall[next.x, next.y] = false;
            stack.Push(next);
        }
    }

    // Phá thêm một số tường để mê cung có nhiều đường đi (tránh bẫy/quái chặn đường duy nhất)
    void OpenLoops(float chance)
    {
        for (int x = 1; x < Width - 1; x++)
        {
            for (int y = 1; y < Height - 1; y++)
            {
                if (!Wall[x, y] || (x + y) % 2 == 0) continue; // chỉ xét tường nằm giữa 2 phòng
                bool horizontal = !Wall[x - 1, y] && !Wall[x + 1, y];
                bool vertical = !Wall[x, y - 1] && !Wall[x, y + 1];
                if ((horizontal || vertical) && Random.value < chance)
                    Wall[x, y] = false;
            }
        }
    }

    bool InInterior(Vector2Int p)
    {
        return p.x > 0 && p.y > 0 && p.x < Width - 1 && p.y < Height - 1;
    }

    public bool IsFree(Vector2Int p)
    {
        return p.x >= 0 && p.y >= 0 && p.x < Width && p.y < Height && !Wall[p.x, p.y];
    }

    public List<Vector2Int> FreeCells()
    {
        var list = new List<Vector2Int>();
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (!Wall[x, y]) list.Add(new Vector2Int(x, y));
        return list;
    }

    // ---------- Tìm đường ngắn nhất (BFS) ----------
    // Trả về danh sách ô cần đi qua (không gồm ô xuất phát). Rỗng nếu không có đường.
    public List<Vector2Int> FindPath(Vector2Int from, Vector2Int to, HashSet<Vector2Int> blocked = null)
    {
        var prev = new Dictionary<Vector2Int, Vector2Int>();
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(from);
        prev[from] = from;

        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            if (c == to) break;
            foreach (var d in Dirs)
            {
                var n = c + d;
                if (!IsFree(n) || prev.ContainsKey(n)) continue;
                if (blocked != null && blocked.Contains(n)) continue;
                prev[n] = c;
                queue.Enqueue(n);
            }
        }

        var path = new List<Vector2Int>();
        if (!prev.ContainsKey(to)) return path;
        for (var c = to; c != from; c = prev[c]) path.Add(c);
        path.Reverse();
        return path;
    }
}
