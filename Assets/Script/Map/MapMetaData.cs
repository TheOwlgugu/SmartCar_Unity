using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>_M 文件里的点类型。数值就是文件里的 type 字段，不要随意改动。</summary>
public enum MapPointType
{
    Start = 0,
    Goal = 1,
    Obstacle = 2
}

/// <summary>_M 里的一个点。X/Y 是地图格索引，和地图本身同一套坐标系。</summary>
[Serializable]
public class MapPoint
{
    public int X;
    public int Y;
    public int type;
}

/// <summary>
/// 地图的补充信息（{名称}_M.json）：版本号 + 节点。
/// 约定：Points 里固定先放起点(type=0)、再放终点(type=1)，之后是障碍点(type=2)。
/// 但读取时**不要依赖下标**，一律按 type 找——写入顺序只是为了人看和 Nano 顺序遍历。
/// </summary>
[Serializable]
public class MapMetaData
{
    public const string DefaultVersion = "1.0";

    /// <summary>未设置起点的占位坐标（地图外，不会和任何道路格冲突）。</summary>
    public const int UnsetX = -1;
    public const int UnsetY = -1;

    public string Version = DefaultVersion;
    public MapPoint[] Points;

    // ---------------- 查询 ----------------

    public MapPoint Find(MapPointType type)
    {
        if (Points == null) return null;
        for (int i = 0; i < Points.Length; i++)
        {
            if (Points[i] != null && Points[i].type == (int)type) return Points[i];
        }
        return null;
    }

    public List<MapPoint> FindAll(MapPointType type)
    {
        List<MapPoint> result = new List<MapPoint>();
        if (Points == null) return result;
        for (int i = 0; i < Points.Length; i++)
        {
            if (Points[i] != null && Points[i].type == (int)type) result.Add(Points[i]);
        }
        return result;
    }

    /// <summary>起点/终点是否已经真正设置过（图外的占位坐标不算）。</summary>
    public bool IsSet(MapPointType type, int width, int height)
    {
        MapPoint p = Find(type);
        return p != null && p.X >= 0 && p.X < width && p.Y >= 0 && p.Y < height;
    }

    public int ObstacleCount()
    {
        return FindAll(MapPointType.Obstacle).Count;
    }

    // ---------------- 修改 ----------------

    /// <summary>设置起点或终点（全局只有一个），越界会被夹回地图内。</summary>
    public void SetPoint(MapPointType type, int x, int y, int width, int height)
    {
        EnsureArray();

        x = Mathf.Clamp(x, 0, Mathf.Max(0, width - 1));
        y = Mathf.Clamp(y, 0, Mathf.Max(0, height - 1));

        MapPoint existing = Find(type);
        if (existing != null)
        {
            existing.X = x;
            existing.Y = y;
            return;
        }

        List<MapPoint> list = new List<MapPoint>(Points);
        list.Add(new MapPoint { X = x, Y = y, type = (int)type });
        Points = list.ToArray();
        Reorder(width, height);
    }

    /// <summary>添加一个障碍点；同一格已存在则不重复添加。返回是否真的加了。</summary>
    public bool AddObstacle(int x, int y, int width, int height)
    {
        if (x < 0 || y < 0 || x >= width || y >= height) return false;

        EnsureArray();

        List<MapPoint> obstacles = FindAll(MapPointType.Obstacle);
        for (int i = 0; i < obstacles.Count; i++)
        {
            if (obstacles[i].X == x && obstacles[i].Y == y) return false;
        }

        List<MapPoint> list = new List<MapPoint>(Points);
        list.Add(new MapPoint { X = x, Y = y, type = (int)MapPointType.Obstacle });
        Points = list.ToArray();
        return true;
    }

    public void ClearObstacles()
    {
        if (Points == null) { Points = new MapPoint[0]; return; }

        List<MapPoint> kept = new List<MapPoint>();
        for (int i = 0; i < Points.Length; i++)
        {
            if (Points[i] != null && Points[i].type != (int)MapPointType.Obstacle) kept.Add(Points[i]);
        }
        Points = kept.ToArray();
    }

    // ---------------- 规范化 ----------------

    /// <summary>
    /// 修正越界坐标、补齐缺失的起点/终点、去掉重复障碍、按"起点→终点→障碍"重排。
    /// 旧文件没有 _M 时也会走到这里，用默认值补出来。
    /// </summary>
    public void Normalize(int width, int height)
    {
        if (string.IsNullOrEmpty(Version)) Version = DefaultVersion;
        if (width < 1) width = 1;
        if (height < 1) height = 1;

        MapPoint start = null;
        MapPoint goal = null;
        List<MapPoint> obstacles = new List<MapPoint>();

        if (Points != null)
        {
            for (int i = 0; i < Points.Length; i++)
            {
                MapPoint p = Points[i];
                if (p == null) continue;

                if (p.type == (int)MapPointType.Start)
                {
                    if (start == null) start = p;      // 多余的起点直接丢弃
                }
                else if (p.type == (int)MapPointType.Goal)
                {
                    if (goal == null) goal = p;
                }
                else if (p.type == (int)MapPointType.Obstacle)
                {
                    // 障碍必须在图内，越界直接丢
                    if (p.X >= 0 && p.X < width && p.Y >= 0 && p.Y < height) obstacles.Add(p);
                }
                // 未知 type 忽略
            }
        }

        // 起点/终点的"未设置"用图外坐标表示，不要夹回图内（那会压到墙上）
        if (start == null)
        {
            start = new MapPoint { X = UnsetX, Y = UnsetY, type = (int)MapPointType.Start };
        }
        else if (start.X < 0 || start.X >= width || start.Y < 0 || start.Y >= height)
        {
            start.X = UnsetX;
            start.Y = UnsetY;
        }

        if (goal == null)
        {
            goal = new MapPoint { X = width, Y = height, type = (int)MapPointType.Goal };
        }
        else if (goal.X < 0 || goal.X >= width || goal.Y < 0 || goal.Y >= height)
        {
            goal.X = width;
            goal.Y = height;
        }

        // 障碍去重（同一格只留一个）
        List<MapPoint> unique = new List<MapPoint>();
        HashSet<int> seen = new HashSet<int>();
        for (int i = 0; i < obstacles.Count; i++)
        {
            int key = obstacles[i].Y * width + obstacles[i].X;
            if (!seen.Add(key)) continue;
            unique.Add(obstacles[i]);
        }

        List<MapPoint> all = new List<MapPoint>(unique.Count + 2);
        all.Add(start);
        all.Add(goal);
        all.AddRange(unique);
        Points = all.ToArray();
    }

    private void EnsureArray()
    {
        if (Points == null) Points = new MapPoint[0];
    }

    private void Reorder(int width, int height)
    {
        Normalize(width, height);
    }
}
