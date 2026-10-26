using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tạo toàn bộ hình pixel-art (16x16) bằng code, không cần import hình ảnh.
/// Phong cách: trời xanh, đất tím có cỏ trên mặt, vật phẩm màu rực như game 2D kiểu Sunnyland.
/// Mọi texture dùng FilterMode.Point nên nét căng, không bị mờ.
/// </summary>
public static class PixelArt
{
    const int PPU = 16; // 16 pixel = 1 đơn vị (1 ô mê cung)

    public static Sprite[] WallGrass, WallDirt, FloorPlain, FloorCloud;
    public static Sprite Player, Key, Coin, Clock, Trap, Monster, DoorLocked, DoorOpen, Heart;

    static Color C(int r, int g, int b) { return new Color(r / 255f, g / 255f, b / 255f, 1f); }

    static readonly Dictionary<char, Color> Pal = new Dictionary<char, Color>
    {
        {'k', new Color(0.10f, 0.07f, 0.12f)},
        {'w', Color.white},
        {'y', new Color(1.00f, 0.85f, 0.20f)}, {'Y', new Color(0.80f, 0.55f, 0.10f)},
        {'o', new Color(1.00f, 0.60f, 0.15f)},
        {'r', new Color(0.88f, 0.20f, 0.20f)}, {'R', new Color(0.55f, 0.10f, 0.15f)},
        {'p', new Color(1.00f, 0.45f, 0.60f)}, {'P', new Color(0.80f, 0.25f, 0.50f)},
        {'b', new Color(0.30f, 0.60f, 1.00f)}, {'B', new Color(0.15f, 0.35f, 0.80f)},
        {'g', new Color(0.35f, 0.75f, 0.20f)}, {'G', new Color(0.20f, 0.50f, 0.15f)},
        {'l', new Color(0.60f, 0.90f, 0.30f)},
        {'m', new Color(0.55f, 0.30f, 0.40f)}, {'M', new Color(0.40f, 0.20f, 0.32f)},
        {'n', new Color(0.85f, 0.50f, 0.30f)},
        {'s', new Color(1.00f, 0.80f, 0.65f)},
        {'u', new Color(0.60f, 0.30f, 0.80f)}, {'U', new Color(0.40f, 0.15f, 0.60f)},
        {'e', new Color(0.62f, 0.62f, 0.65f)}, {'E', new Color(0.35f, 0.35f, 0.40f)},
        {'D', new Color(0.60f, 0.35f, 0.15f)}, {'d', new Color(0.40f, 0.22f, 0.10f)},
    };

    static readonly Color Sky = new Color(0.55f, 0.82f, 0.98f);
    static readonly Color SkyLight = new Color(0.68f, 0.90f, 1.00f);
    static readonly Color CloudShade = new Color(0.84f, 0.93f, 1.00f);

    public static void Init()
    {
        if (Player != null) return;

        WallGrass = new[] { DirtTile(11, true), DirtTile(12, true), DirtTile(13, true) };
        WallDirt = new[] { DirtTile(21, false), DirtTile(22, false), DirtTile(23, false) };
        FloorPlain = new[] { SkyTile(31, false), SkyTile(32, false), SkyTile(33, false) };
        FloorCloud = new[] { SkyTile(41, true), SkyTile(42, true) };

        Player = Make(1,
            "....PPPPPP......",
            "...PppppppP.....",
            "...PpppppppP....",
            "...PsssssssP....",
            "...PsskssksP....",
            "....ssssssss....",
            "....BBBBBBBB....",
            "...sBBBBBBBBs...",
            "...sBBBkkBBBs...",
            "....BBBBBBBB....",
            "....BBBBBBBB....",
            "....EE....EE....",
            "....EE....EE....",
            "...kkk....kkk...");

        Key = Make(1,
            "......yyyy......",
            ".....yYYYYy.....",
            ".....yY..Yy.....",
            ".....yY..Yy.....",
            ".....yYYYYy.....",
            "......yyyy......",
            ".......yY.......",
            ".......yY.......",
            ".......yYyy.....",
            ".......yY.......",
            ".......yYyy.....",
            ".......yY.......",
            ".......yyY......");

        Coin = Make(4,
            "......kkkk......",
            ".....kyyyyk.....",
            "....kyyyyyyk....",
            "....kywyyYyk....",
            "....kyyyyYyk....",
            "....kyyyYYyk....",
            ".....kyYYYk.....",
            "......kkkk......");

        Clock = Make(2,
            "...gg......gg...",
            "....GGGGGGGG....",
            "...GwwwwwwwwG...",
            "..GwwwwwwwwwwG..",
            "..GwwwwkwwwwwG..",
            "..GwwwwkwwwwwG..",
            "..GwwwwkkkkwwG..",
            "..GwwwwwwwwwwG..",
            "...GwwwwwwwwG...",
            "....GGGGGGGG....");

        Trap = Make(6,
            "...e....e....e..",
            "..eEe..eEe..eEe.",
            "..eEe..eEe..eEe.",
            "..eEe..eEe..eEe.",
            "RRRRRRRRRRRRRRRR",
            "rRrRrRrRrRrRrRrR");

        Monster = Make(3,
            "....U......U....",
            "....UU....UU....",
            "...UUuuuuuuUU...",
            "..UuuuuuuuuuuU..",
            "..UuwwuuuuwwuU..",
            "..UuwkuuuukwuU..",
            "..UuuuuuuuuuuU..",
            "..UuuukkkkuuuU..",
            "...UuuuuuuuuU...",
            "....UUUUUUUU....");

        DoorLocked = Make(1,
            "....kkkkkkkk....",
            "...kDDDDDDDDk...",
            "..kDDDDDDDDDDk..",
            "..kDDdDDDDdDDk..",
            "..kDDDDDDDDDDk..",
            "..kDDDDDDDDDDk..",
            "..kDDDDyyDDDDk..",
            "..kDDDyYYyDDDk..",
            "..kDDDyYkYyDDk..",
            "..kDDDyYYYyDDk..",
            "..kDDDDDDDDDDk..",
            "..kDDDDDDDDDDk..",
            "..kDDdDDDDDDDk..",
            "..kkkkkkkkkkkk..");

        DoorOpen = Make(1,
            "....kkkkkkkk....",
            "...kDDDDDDDDk...",
            "..kDDkkkkkkDDk..",
            "..kDkllllllkDk..",
            "..kDkllllllkDk..",
            "..kDklllgggkDk..",
            "..kDklllgggkDk..",
            "..kDklllgggkDk..",
            "..kDklllgggkDk..",
            "..kDkgggggGkDk..",
            "..kDkgggggGkDk..",
            "..kDkgggggGkDk..",
            "..kDkgggggGkDk..",
            "..kkkkkkkkkkkk..");

        Heart = Make(3,
            "..rrrr..rrrr....",
            ".rrwrrrrrrrrr...",
            ".rrrrrrrrrrrr...",
            ".rrrrrrrrrrrr...",
            "..rrrrrrrrrr....",
            "...rrrrrrrr.....",
            "....rrrrrr......",
            ".....rrrr.......",
            "......rr........");
    }

    // ---------------------------------------------------------------
    static Texture2D NewTex()
    {
        var tex = new Texture2D(PPU, PPU, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;   // nét căng, không làm mờ
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    static Sprite ToSprite(Texture2D tex)
    {
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, PPU, PPU), new Vector2(0.5f, 0.5f), PPU);
    }

    static void Fill(Texture2D tex, Color c)
    {
        for (int x = 0; x < PPU; x++)
            for (int y = 0; y < PPU; y++)
                tex.SetPixel(x, y, c);
    }

    // Vẽ sprite từ chuỗi ký tự. 'top' = hàng đầu tiên của hình trong ô 16x16 (0 = trên cùng).
    static Sprite Make(int top, params string[] rows)
    {
        var tex = NewTex();
        Fill(tex, Color.clear);
        for (int i = 0; i < rows.Length; i++)
        {
            int y = PPU - 1 - (top + i);
            if (y < 0) break;
            for (int x = 0; x < PPU && x < rows[i].Length; x++)
            {
                Color col;
                if (Pal.TryGetValue(rows[i][x], out col)) tex.SetPixel(x, y, col);
            }
        }
        return ToSprite(tex);
    }

    // Ô tường: đất tím + đá cam, có thể có cỏ ở mặt trên
    static Sprite DirtTile(int seed, bool grass)
    {
        var rng = new System.Random(seed);
        var tex = NewTex();
        Fill(tex, Pal['m']);

        for (int i = 0; i < 5; i++) // đá cam 2x2
        {
            int rx = rng.Next(1, 13), ry = rng.Next(1, 9);
            for (int dx = 0; dx < 2; dx++)
                for (int dy = 0; dy < 2; dy++)
                    tex.SetPixel(rx + dx, ry + dy, Pal['n']);
        }
        for (int i = 0; i < 10; i++) // chấm tối
            tex.SetPixel(rng.Next(0, PPU), rng.Next(1, 13), Pal['M']);
        for (int x = 0; x < PPU; x++) // viền đáy
            tex.SetPixel(x, 0, Pal['M']);

        if (grass)
        {
            for (int x = 0; x < PPU; x++)
            {
                tex.SetPixel(x, 15, rng.Next(3) == 0 ? Pal['g'] : Pal['l']);
                tex.SetPixel(x, 14, Pal['g']);
                tex.SetPixel(x, 13, Pal['G']);
                if (rng.Next(3) == 0) tex.SetPixel(x, 12, Pal['G']);
            }
        }
        return ToSprite(tex);
    }

    // Ô nền: trời xanh, đôi khi có mây
    static Sprite SkyTile(int seed, bool cloud)
    {
        var rng = new System.Random(seed);
        var tex = NewTex();
        Fill(tex, Sky);
        for (int i = 0; i < 6; i++)
            tex.SetPixel(rng.Next(0, PPU), rng.Next(0, PPU), SkyLight);

        if (cloud)
        {
            int ox = rng.Next(0, 6), oy = rng.Next(3, 10);
            for (int dx = 0; dx < 10; dx++) tex.SetPixel(ox + dx, oy, CloudShade);
            for (int dx = 0; dx < 10; dx++) tex.SetPixel(ox + dx, oy + 1, Color.white);
            for (int dx = 2; dx < 8; dx++) tex.SetPixel(ox + dx, oy + 2, Color.white);
            for (int dx = 3; dx < 6; dx++) tex.SetPixel(ox + dx, oy + 3, Color.white);
        }
        return ToSprite(tex);
    }
}
