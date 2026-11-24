using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 本地可读 JSON 的读写。
/// 读：JsonUtility（容忍缩进/换行，新旧格式同一入口）；写：自定义逐行输出（每格一行，git diff 只亮改动的行）。
/// </summary>
public static class MapJson
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    public static string MapsDir
    {
        get { return Path.Combine(Application.dataPath, "MAPS"); }
    }

    public static string FileFor(string mapName)
    {
        return Path.Combine(MapsDir, mapName + ".json");
    }

    private static string TempDir
    {
        // 写临时文件放在工程的 Temp/ 下：同盘、原子替换、又不会被当成资源导入
        get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp")); }
    }

    public static MapFileData Load(string mapName)
    {
        string path = FileFor(mapName);
        if (!File.Exists(path))
        {
            Debug.LogWarning("[MapJson] 文件不存在: " + path);
            return null;
        }

        try
        {
            return Read(File.ReadAllText(path, Encoding.UTF8));
        }
        catch (Exception e)
        {
            Debug.LogError("[MapJson] 读取失败: " + e.Message);
            return null;
        }
    }

    public static MapFileData Read(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;

        MapFileData data;
        try
        {
            // 关键：JsonUtility 不执行字段初始化器，未知字段（比如旧文件里的 Version）会被忽略。
            data = JsonUtility.FromJson<MapFileData>(json);
        }
        catch (Exception e)
        {
            Debug.LogError("[MapJson] JSON 解析失败: " + e.Message);
            return null;
        }

        if (data == null) return null;
        data.Normalize();
        return data;
    }

    public static bool Save(string mapName, MapFileData data)
    {
        if (data == null) return false;
        if (string.IsNullOrEmpty(mapName)) return false;

        string path = FileFor(mapName);
        try
        {
            data.Normalize();
            Directory.CreateDirectory(MapsDir);

            string tmp = Path.Combine(TempDir, mapName + ".map.tmp");
            Directory.CreateDirectory(TempDir);
            File.WriteAllText(tmp, Write(data), Utf8NoBom);

            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[MapJson] 保存失败: " + e.Message);
            return false;
        }
    }

    /// <summary>每格一行，便于阅读与 diff。字段顺序固定：X, Y, N, E, S, W, R。</summary>
    public static string Write(MapFileData data)
    {
        if (data == null) throw new ArgumentNullException("data");
        data.Normalize();

        StringBuilder sb = new StringBuilder(128 + data.Cells.Length * 72);
        sb.Append("{\n");
        sb.Append("  \"Height\": ").Append(Int(data.Height)).Append(",\n");
        sb.Append("  \"Width\": ").Append(Int(data.Width)).Append(",\n");
        sb.Append("  \"Cells\": [\n");

        int last = data.Cells.Length - 1;
        for (int i = 0; i <= last; i++)
        {
            MapCell c = data.Cells[i];
            sb.Append("    {\"X\":").Append(Int(c.X))
              .Append(",\"Y\":").Append(Int(c.Y))
              .Append(",\"N\":").Append(Bool(c.N))
              .Append(",\"E\":").Append(Bool(c.E))
              .Append(",\"S\":").Append(Bool(c.S))
              .Append(",\"W\":").Append(Bool(c.W))
              .Append(",\"R\":").Append(Bool(c.R))
              .Append('}');
            if (i < last) sb.Append(',');
            sb.Append('\n');
        }

        sb.Append("  ]\n}\n");
        return sb.ToString();
    }

    private static string Int(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string Bool(bool value)
    {
        return value ? "true" : "false";
    }
}
