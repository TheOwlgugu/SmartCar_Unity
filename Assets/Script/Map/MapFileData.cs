using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 地图的最小单位（Cell）。字段与 docs/map-schema-v1.md 一一对应。
/// v1 中 E/W/S/N 由"邻格是否道路"推导，文件里写出来只为可读；
/// 唯一语义字段是 R（是否道路）。将来做单行道时改为以文件为准。
/// </summary>
[Serializable]
public class MapCell
{
    public int X;
    public int Y;
    public bool N;
    public bool E;
    public bool S;
    public bool W;
    public bool R;
}

/// <summary>
/// 本地可读地图模型（唯一真相）。
/// 索引规则：index = Y * Width + X；X 向东（+X），Y 向北（+Z），(0,0) 在西南角。
/// </summary>
[Serializable]
public class MapFileData
{
    public const int CurrentVersion = 1;
    public const int MaxSide = 200;

    public int Version = CurrentVersion;
    public int Height;
    public int Width;
    public MapCell[] Cells;

    public int CellCount { get { return Width * Height; } }

    public int IndexOf(int x, int y) { return y * Width + x; }

    public bool InBounds(int x, int y) { return x >= 0 && x < Width && y >= 0 && y < Height; }

    public MapCell Get(int x, int y) { return InBounds(x, y) ? Cells[IndexOf(x, y)] : null; }

    public bool IsRoad(int x, int y)
    {
        if (!InBounds(x, y)) return false;
        MapCell c = Cells[IndexOf(x, y)];
        return c != null && c.R;
    }

    /// <summary>全道路地图（新建地图、旧格式补齐都用它）。</summary>
    public static MapFileData Create(int width, int height)
    {
        MapFileData data = new MapFileData();
        data.Version = CurrentVersion;
        data.Width = Mathf.Clamp(width, 1, MaxSide);
        data.Height = Mathf.Clamp(height, 1, MaxSide);
        data.Cells = new MapCell[data.Width * data.Height];
        for (int y = 0; y < data.Height; y++)
        {
            for (int x = 0; x < data.Width; x++)
            {
                data.Cells[data.IndexOf(x, y)] = new MapCell { X = x, Y = y, R = true };
            }
        }
        data.RecomputeAllDirections();
        return data;
    }

    /// <summary>
    /// 规范化：长度对齐、索引回写、方向位重算。
    /// 旧格式（只有 Height/Width）会走"补齐为全道路"分支。
    /// </summary>
    public void Normalize()
    {
        Version = Version < 1 ? CurrentVersion : Version;
        Width = Mathf.Clamp(Width, 1, MaxSide);
        Height = Mathf.Clamp(Height, 1, MaxSide);

        if (Cells == null || Cells.Length != Width * Height)
        {
            Cells = new MapCell[Width * Height];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    Cells[IndexOf(x, y)] = new MapCell { X = x, Y = y, R = true };
                }
            }
        }
        else
        {
            // X/Y 是冗余字段：以数组下标为准，手改文件对不上时也不受其影响
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = IndexOf(x, y);
                    if (Cells[i] == null)
                    {
                        Cells[i] = new MapCell { X = x, Y = y, R = true };
                        continue;
                    }
                    Cells[i].X = x;
                    Cells[i].Y = y;
                }
            }
        }

        RecomputeAllDirections();
    }

    public void RecomputeAllDirections()
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                RecomputeDirections(x, y);
            }
        }
    }

    private void RecomputeDirections(int x, int y)
    {
        if (!InBounds(x, y)) return;
        MapCell c = Cells[IndexOf(x, y)];
        if (c == null) return;

        if (!c.R)
        {
            c.N = false;
            c.E = false;
            c.S = false;
            c.W = false;
            return;
        }

        // 两个相邻的道路格必然是连通的（墙就是整格不可通行），
        // 所以方向位完全可以由 R 推导，编辑器不允许出现单边门。
        c.N = IsRoad(x, y + 1);
        c.E = IsRoad(x + 1, y);
        c.S = IsRoad(x, y - 1);
        c.W = IsRoad(x - 1, y);
    }

    public void SetRoad(int x, int y, bool road)
    {
        if (!InBounds(x, y)) return;

        int i = IndexOf(x, y);
        MapCell c = Cells[i];
        if (c == null)
        {
            c = new MapCell { X = x, Y = y };
            Cells[i] = c;
        }
        if (c.R == road) return;

        c.R = road;
        RecomputeDirections(x, y);
        RecomputeDirections(x + 1, y);
        RecomputeDirections(x - 1, y);
        RecomputeDirections(x, y + 1);
        RecomputeDirections(x, y - 1);
    }

    /// <summary>西南角锚定：放大时向东加列、向北加行，旧格坐标不变；缩小时丢弃越界格。</summary>
    public void Resize(int newWidth, int newHeight)
    {
        newWidth = Mathf.Clamp(newWidth, 1, MaxSide);
        newHeight = Mathf.Clamp(newHeight, 1, MaxSide);
        if (newWidth == Width && newHeight == Height) return;

        MapCell[] old = Cells;
        int oldWidth = Width;
        int oldHeight = Height;

        MapCell[] next = new MapCell[newWidth * newHeight];
        for (int y = 0; y < newHeight; y++)
        {
            for (int x = 0; x < newWidth; x++)
            {
                bool road = true;
                if (old != null && x < oldWidth && y < oldHeight)
                {
                    MapCell src = old[y * oldWidth + x];
                    road = src != null && src.R;
                }
                next[y * newWidth + x] = new MapCell { X = x, Y = y, R = road };
            }
        }

        Width = newWidth;
        Height = newHeight;
        Cells = next;
        RecomputeAllDirections();
    }

    public int RoadCount()
    {
        if (Cells == null) return 0;
        int n = 0;
        for (int i = 0; i < Cells.Length; i++)
        {
            if (Cells[i] != null && Cells[i].R) n++;
        }
        return n;
    }

    /// <summary>道路连通块数量（4 邻接）。大于 1 时 A* 大概率失败。</summary>
    public int ComponentCount()
    {
        if (Cells == null || Cells.Length == 0) return 0;

        bool[] seen = new bool[Cells.Length];
        int count = 0;
        Stack<int> stack = new Stack<int>();

        for (int i = 0; i < Cells.Length; i++)
        {
            if (seen[i]) continue;
            seen[i] = true;
            if (Cells[i] == null || !Cells[i].R) continue;

            count++;
            stack.Push(i);
            while (stack.Count > 0)
            {
                int index = stack.Pop();
                int x = index % Width;
                int y = index / Width;
                PushRoadNeighbour(x + 1, y, seen, stack);
                PushRoadNeighbour(x - 1, y, seen, stack);
                PushRoadNeighbour(x, y + 1, seen, stack);
                PushRoadNeighbour(x, y - 1, seen, stack);
            }
        }
        return count;
    }

    private void PushRoadNeighbour(int x, int y, bool[] seen, Stack<int> stack)
    {
        if (!InBounds(x, y)) return;
        int i = IndexOf(x, y);
        if (seen[i]) return;
        seen[i] = true;
        if (Cells[i] == null || !Cells[i].R) return;
        stack.Push(i);
    }

    /// <summary>调试用预览：行从北到南，列从西到东；# 不可通行，. 四向全通，o 方向受限。</summary>
    public string ToAscii()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(Width).Append(" x ").Append(Height)
          .Append("   roads ").Append(RoadCount()).Append('/').Append(CellCount)
          .Append("   components ").Append(ComponentCount())
          .Append('\n');

        for (int y = Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < Width; x++)
            {
                MapCell c = Get(x, y);
                char ch;
                if (c == null || !c.R) ch = '#';
                else if (c.N && c.E && c.S && c.W) ch = '.';
                else ch = 'o';

                sb.Append(ch);
                if (x < Width - 1) sb.Append(' ');
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }
}
