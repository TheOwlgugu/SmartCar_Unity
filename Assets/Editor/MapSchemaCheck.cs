#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 地图数据链自检（开发工具，可随时删除）。
/// 全部用例都在内存里构造数据，不依赖 Assets/MAPS 下的文件——那些文件会被编辑器改写。
/// 用法：Unity.exe -batchmode -quit -projectPath ... -executeMethod MapSchemaCheck.Run
/// </summary>
public static class MapSchemaCheck
{
    private static readonly int[] GoldenCells = { 19, 27, 25, 23, 31, 29, 22, 30, 28 };
    private const string GoldenPayload = "{\"Width\":3,\"Height\":3,\"Cells\":[19,27,25,23,31,29,22,30,28]}";

    private static StringBuilder report;
    private static int failures;

    public static void Run()
    {
        report = new StringBuilder();
        failures = 0;
        report.AppendLine("=== MapSchemaCheck ===");

        try
        {
            CheckGoldenMask();
            CheckRoundTrip();
            CheckLegacyFile();
            CheckMeta();
            CheckSetRoad();
            CheckResize();
            CheckLibraryFiles();
        }
        catch (Exception e)
        {
            failures++;
            report.AppendLine("[EXCEPTION] " + e);
        }

        report.AppendLine(failures == 0 ? "RESULT: ALL PASS" : "RESULT: " + failures + " FAILURE(S)");

        string path = ResultPath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, report.ToString(), new UTF8Encoding(false));
        }
        catch (Exception e)
        {
            Debug.LogError("[MapSchemaCheck] 写结果文件失败: " + e.Message);
        }

        Debug.Log(report.ToString());
        if (failures > 0) Debug.LogError("[MapSchemaCheck] " + failures + " failure(s)");
    }

    private static string ResultPath
    {
        get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "MapSchemaCheck.result.txt")); }
    }

    private static void Check(string name, bool ok, string detail)
    {
        if (ok)
        {
            report.AppendLine("[PASS] " + name + (string.IsNullOrEmpty(detail) ? "" : "  (" + detail + ")"));
        }
        else
        {
            failures++;
            report.AppendLine("[FAIL] " + name + "  " + detail);
        }
    }

    /// <summary>3×3 全道路 → 掩码必须等于黄金值（这是两端唯一的硬约定）。</summary>
    private static void CheckGoldenMask()
    {
        MapFileData data = MapFileData.Create(3, 3);

        Check("Create(3,3) 尺寸", data.Width == 3 && data.Height == 3, data.Width + "x" + data.Height);
        Check("Create 默认全道路", data.RoadCount() == 9 && data.ComponentCount() == 1,
            data.RoadCount() + " roads, " + data.ComponentCount() + " components");

        int[] mask = TransformMap.EncodeCells(data);
        string got = string.Join(",", mask);
        bool same = mask.Length == GoldenCells.Length;
        for (int i = 0; same && i < mask.Length; i++)
        {
            if (mask[i] != GoldenCells[i]) same = false;
        }
        Check("EncodeCells == 黄金值", same, "got [" + got + "]");

        string payload = TransformMap.EncodePayload(data);
        Check("EncodePayload 逐字节一致", payload == GoldenPayload, payload);
    }

    private static void CheckRoundTrip()
    {
        MapFileData data = MapFileData.Create(3, 3);
        data.SetRoad(1, 1, false);

        string json = MapJson.Write(data);
        int cellLines = 0;
        string[] lines = json.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("{\"X\":")) cellLines++;
        }
        Check("每格一行（9 行）", cellLines == 9, "got " + cellLines);

        MapFileData back = MapJson.Read(json);
        if (back == null) { Check("Write→Read", false, "解析返回 null"); return; }

        bool identical = back.Width == data.Width && back.Height == data.Height
                         && back.Cells != null && back.Cells.Length == data.Cells.Length;
        if (identical)
        {
            for (int i = 0; i < data.Cells.Length; i++)
            {
                MapCell a = data.Cells[i];
                MapCell b = back.Cells[i];
                if (b == null || a.R != b.R || a.X != b.X || a.Y != b.Y
                    || a.N != b.N || a.E != b.E || a.S != b.S || a.W != b.W)
                {
                    identical = false;
                    break;
                }
            }
        }
        Check("Write→Read 无损", identical, "");
    }

    /// <summary>只有宽高的旧格式：应补成全道路，Version 归 1。</summary>
    private static void CheckLegacyFile()
    {
        MapFileData data = MapJson.Read("{\"Height\":2,\"Width\":2}");
        Check("旧格式能解析", data != null, data == null ? "null" : "ok");
        if (data == null) return;

        Check("旧格式补齐为全道路", data.Cells != null && data.Cells.Length == 4 && data.RoadCount() == 4,
            (data.Cells == null ? "null" : data.Cells.Length.ToString()) + " cells, " + data.RoadCount() + " roads");
    }

    /// <summary>_M 文件：默认值、顺序约定、去重、往返。</summary>
    private static void CheckMeta()
    {
        MapMetaData meta = new MapMetaData();
        meta.Normalize(4, 3);

        Check("_M 未设置起点 → (-1,-1)",
            meta.Find(MapPointType.Start).X == -1 && meta.Find(MapPointType.Start).Y == -1
            && !meta.IsSet(MapPointType.Start, 4, 3), "");
        Check("_M 未设置终点 → (W,H)",
            meta.Find(MapPointType.Goal).X == 4 && meta.Find(MapPointType.Goal).Y == 3
            && !meta.IsSet(MapPointType.Goal, 4, 3), "");
        Check("_M 起点排第 0、终点排第 1",
            meta.Points.Length == 2 && meta.Points[0].type == 0 && meta.Points[1].type == 1, "");

        meta.SetPoint(MapPointType.Start, 2, 1, 4, 3);
        Check("_M 改起点后仍在第 0 位且已生效",
            meta.Points[0].type == 0 && meta.Points[0].X == 2 && meta.Points[0].Y == 1
            && meta.IsSet(MapPointType.Start, 4, 3), "");

        meta.AddObstacle(1, 1, 4, 3);
        meta.AddObstacle(1, 1, 4, 3);
        Check("_M 同格障碍去重", meta.ObstacleCount() == 1, "got " + meta.ObstacleCount());
        Check("_M 障碍排在最后", meta.Points[meta.Points.Length - 1].type == 2, "");
        Check("_M 越界障碍被拒绝", !meta.AddObstacle(99, 99, 4, 3), "");

        string json = MapMetaJson.Write(meta);
        MapMetaData back = MapMetaJson.Read(json, 4, 3);
        Check("_M Write→Read",
            back != null && back.ObstacleCount() == 1 && back.Find(MapPointType.Start).X == 2,
            back == null ? "null" : "ok");

        meta.ClearObstacles();
        Check("_M 清空障碍", meta.ObstacleCount() == 0, "got " + meta.ObstacleCount());
    }

    private static void CheckSetRoad()
    {
        MapFileData data = MapFileData.Create(3, 3);
        data.SetRoad(1, 1, false);

        Check("中心格 R=false", !data.IsRoad(1, 1), "");
        Check("邻格方向位同步清空",
            !data.Get(0, 1).E && !data.Get(2, 1).W && !data.Get(1, 0).N && !data.Get(1, 2).S,
            string.Format("E={0} W={1} N={2} S={3}", data.Get(0, 1).E, data.Get(2, 1).W, data.Get(1, 0).N, data.Get(1, 2).S));
        Check("中心格方向位全清",
            !data.Get(1, 1).N && !data.Get(1, 1).E && !data.Get(1, 1).S && !data.Get(1, 1).W, "");
        Check("挖空中心后仍是 1 个连通块（环形）", data.ComponentCount() == 1, "got " + data.ComponentCount());

        data.SetRoad(0, 1, false);
        data.SetRoad(2, 1, false);
        Check("整行封死后 2 个连通块", data.ComponentCount() == 2, "got " + data.ComponentCount());
    }

    private static void CheckResize()
    {
        MapFileData data = MapFileData.Create(3, 3);
        data.SetRoad(0, 0, false);

        data.Resize(5, 4);
        Check("Resize 尺寸", data.Width == 5 && data.Height == 4, data.Width + "x" + data.Height);
        Check("西南角旧数据保留", !data.IsRoad(0, 0), "期望 false");
        Check("新增格默认道路", data.IsRoad(4, 3) && data.IsRoad(3, 2), "");
        Check("新边界外向位为 0",
            !data.Get(4, 3).E && !data.Get(4, 3).N && !data.Get(0, 3).W && !data.Get(4, 0).S, "");

        data.Resize(3, 3);
        Check("缩回 3x3 尺寸", data.Width == 3 && data.Height == 3, data.Width + "x" + data.Height);
        Check("缩回后西南角仍是墙", !data.IsRoad(0, 0), "");
    }

    /// <summary>MAPS 下的真实文件只做"能读、长度自洽"的冒烟检查，不比对内容（会被编辑器改写）。</summary>
    private static void CheckLibraryFiles()
    {
        string dir = Path.Combine(Application.dataPath, "MAPS");
        if (!Directory.Exists(dir))
        {
            Check("MAPS 目录存在", false, dir);
            return;
        }

        string[] files = Directory.GetFiles(dir, "*.json");
        if (files.Length == 0)
        {
            report.AppendLine("[SKIP] MAPS 目录里没有地图文件");
            return;
        }

        for (int i = 0; i < files.Length; i++)
        {
            string name = Path.GetFileNameWithoutExtension(files[i]);
            MapFileData data = MapJson.Read(File.ReadAllText(files[i]));
            if (data == null)
            {
                Check("读取 " + name, false, "解析失败");
                continue;
            }
            Check("读取 " + name,
                data.Cells != null && data.Cells.Length == data.Width * data.Height,
                data.Width + "x" + data.Height + ", " + data.RoadCount() + " roads");
        }
    }
}
#endif
