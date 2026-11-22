#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 地图数据链自检（开发工具，可随时删除）。
/// 用法：Unity.exe -batchmode -quit -projectPath ... -executeMethod MapSchemaCheck.Run
/// 结果同时写到 Console 和工程 Temp/schema-check.txt。
/// </summary>
public static class MapSchemaCheck
{
    private static readonly int[] GoldenCells = { 19, 27, 25, 23, 31, 29, 22, 30, 28 };

    private static StringBuilder report;
    private static int failures;

    public static void Run()
    {
        report = new StringBuilder();
        failures = 0;
        report.AppendLine("=== MapSchemaCheck ===");

        try
        {
            CheckLoad();
            CheckMask();
            CheckPayload();
            CheckRoundTrip();
            CheckSetRoad();
            CheckResize();
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

    /// <summary>写在工程根目录：不在 Assets 下不会被当资源导入，也不会被 Unity 退出时清掉的 Temp。</summary>
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

    private static void CheckLoad()
    {
        MapFileData data = MapJson.Load("demo");
        Check("load demo", data != null, data == null ? "返回 null" : "ok");
        if (data == null) return;

        Check("Width=3", data.Width == 3, "got " + data.Width);
        Check("Height=3", data.Height == 3, "got " + data.Height);
        Check("Cells.Length=9", data.Cells != null && data.Cells.Length == 9,
            "got " + (data.Cells == null ? "null" : data.Cells.Length.ToString()));
        Check("Version=1", data.Version == 1, "got " + data.Version);
        Check("RoadCount=9", data.RoadCount() == 9, "got " + data.RoadCount());
        Check("ComponentCount=1", data.ComponentCount() == 1, "got " + data.ComponentCount());
    }

    private static void CheckMask()
    {
        MapFileData data = MapJson.Load("demo");
        if (data == null) { Check("mask", false, "demo 加载失败"); return; }

        int[] mask = TransformMap.EncodeCells(data);
        bool same = mask.Length == GoldenCells.Length;
        string got = "";
        for (int i = 0; i < mask.Length; i++)
        {
            got += (i == 0 ? "" : ",") + mask[i];
            if (i >= GoldenCells.Length || mask[i] != GoldenCells[i]) same = false;
        }
        Check("EncodeCells == 黄金值", same, "got [" + got + "]");
    }

    private static void CheckPayload()
    {
        MapFileData data = MapJson.Load("demo");
        if (data == null) { Check("payload", false, "demo 加载失败"); return; }

        string payload = TransformMap.EncodePayload(data);
        string expected = "{\"Version\":1,\"Width\":3,\"Height\":3,\"Cells\":[19,27,25,23,31,29,22,30,28]}";
        Check("EncodePayload 逐字节一致", payload == expected, payload);
    }

    private static void CheckRoundTrip()
    {
        MapFileData data = MapJson.Load("demo");
        if (data == null) { Check("roundtrip", false, "demo 加载失败"); return; }

        string json = MapJson.Write(data);
        int cellLines = 0;
        string[] lines = json.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("{\"X\":")) cellLines++;
        }
        Check("每格一行（9 行）", cellLines == 9, "got " + cellLines);

        MapFileData back = MapJson.Read(json);
        if (back == null) { Check("roundtrip 解析", false, "返回 null"); return; }

        bool identical = back.Width == data.Width && back.Height == data.Height && back.Cells.Length == data.Cells.Length;
        if (identical)
        {
            for (int i = 0; i < data.Cells.Length; i++)
            {
                MapCell a = data.Cells[i];
                MapCell b = back.Cells[i];
                if (b == null || a.R != b.R || a.X != b.X || a.Y != b.Y || a.N != b.N || a.E != b.E || a.S != b.S || a.W != b.W)
                {
                    identical = false;
                    break;
                }
            }
        }
        Check("Write→Read 无损", identical, "");
    }

    private static void CheckSetRoad()
    {
        MapFileData data = MapJson.Load("demo");
        if (data == null) { Check("setRoad", false, "demo 加载失败"); return; }

        data.SetRoad(1, 1, false); // 中心格变墙

        Check("中心格 R=false", !data.IsRoad(1, 1), "");
        Check("邻格方向位同步清空",
            !data.Get(0, 1).E && !data.Get(2, 1).W && !data.Get(1, 0).N && !data.Get(1, 2).S,
            string.Format("E={0} W={1} N={2} S={3}", data.Get(0, 1).E, data.Get(2, 1).W, data.Get(1, 0).N, data.Get(1, 2).S));
        Check("中心格方向位全清", !data.Get(1, 1).N && !data.Get(1, 1).E && !data.Get(1, 1).S && !data.Get(1, 1).W, "");
        Check("挖空中心后仍是 1 个连通块（环形）", data.ComponentCount() == 1, "got " + data.ComponentCount());

        // 整行封死，切成南北两块
        data.SetRoad(0, 1, false);
        data.SetRoad(2, 1, false);
        Check("整行封死后 2 个连通块", data.ComponentCount() == 2, "got " + data.ComponentCount());
    }

    private static void CheckResize()
    {
        MapFileData data = MapJson.Load("demo");
        if (data == null) { Check("resize", false, "demo 加载失败"); return; }

        data.SetRoad(0, 0, false);   // 西南角画一堵墙，验证锚定
        data.Resize(5, 4);

        Check("Resize 尺寸", data.Width == 5 && data.Height == 4, data.Width + "x" + data.Height);
        Check("西南角旧数据保留", !data.IsRoad(0, 0), "期望 false");
        Check("新增格默认道路", data.IsRoad(4, 3) && data.IsRoad(3, 2), "");
        Check("新边界外向位为 0",
            !data.Get(4, 3).E && !data.Get(4, 3).N && !data.Get(0, 3).W && !data.Get(4, 0).S,
            "");

        data.Resize(3, 3);
        Check("缩回 3x3 尺寸", data.Width == 3 && data.Height == 3, data.Width + "x" + data.Height);
        Check("缩回后西南角仍是墙", !data.IsRoad(0, 0), "");
    }
}
#endif
