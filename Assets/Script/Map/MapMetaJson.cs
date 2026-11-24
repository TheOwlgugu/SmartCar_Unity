using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// _M 文件（{名称}_M.json）的读写，放在 Assets/MAPS/META/ 下。
/// 排版和地图一致：每个点一行，方便 diff。
/// </summary>
public static class MapMetaJson
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    public static string MetaDir
    {
        get { return Path.Combine(MapJson.MapsDir, "META"); }
    }

    public static string FileFor(string mapName)
    {
        return Path.Combine(MetaDir, mapName + "_M.json");
    }

    private static string TempDir
    {
        get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp")); }
    }

    /// <summary>读不到就返回 null，由调用方决定用默认值还是报错。</summary>
    public static MapMetaData Load(string mapName, int width, int height)
    {
        string path = FileFor(mapName);
        if (!File.Exists(path)) return null;

        try
        {
            return Read(File.ReadAllText(path, Encoding.UTF8), width, height);
        }
        catch (Exception e)
        {
            Debug.LogError("[MapMetaJson] 读取失败: " + e.Message);
            return null;
        }
    }

    public static MapMetaData LoadOrDefault(string mapName, int width, int height)
    {
        MapMetaData meta = Load(mapName, width, height);
        if (meta != null) return meta;

        meta = new MapMetaData();
        meta.Normalize(width, height);   // 起点 (0,0)、终点 (W-1,H-1)
        return meta;
    }

    public static MapMetaData Read(string json, int width, int height)
    {
        if (string.IsNullOrEmpty(json)) return null;

        MapMetaData meta;
        try
        {
            meta = JsonUtility.FromJson<MapMetaData>(json);
        }
        catch (Exception e)
        {
            Debug.LogError("[MapMetaJson] JSON 解析失败: " + e.Message);
            return null;
        }

        if (meta == null) return null;
        meta.Normalize(width, height);
        return meta;
    }

    public static bool Save(string mapName, MapMetaData meta, int width, int height)
    {
        if (meta == null || string.IsNullOrEmpty(mapName)) return false;

        string path = FileFor(mapName);
        try
        {
            meta.Normalize(width, height);
            Directory.CreateDirectory(MetaDir);

            string tmp = Path.Combine(TempDir, mapName + "_M.tmp");
            Directory.CreateDirectory(TempDir);
            File.WriteAllText(tmp, Write(meta), Utf8NoBom);

            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[MapMetaJson] 保存失败: " + e.Message);
            return false;
        }
    }

    /// <summary>每个点一行，字段顺序固定：X, Y, type。</summary>
    public static string Write(MapMetaData meta)
    {
        if (meta == null) throw new ArgumentNullException("meta");

        StringBuilder sb = new StringBuilder(128 + (meta.Points != null ? meta.Points.Length * 32 : 0));
        sb.Append("{\n");
        sb.Append("  \"Version\": \"").Append(Escape(meta.Version)).Append("\",\n");
        sb.Append("  \"Points\": [\n");

        int count = meta.Points != null ? meta.Points.Length : 0;
        for (int i = 0; i < count; i++)
        {
            MapPoint p = meta.Points[i];
            if (p == null) continue;

            sb.Append("    {\"X\":").Append(Int(p.X))
              .Append(",\"Y\":").Append(Int(p.Y))
              .Append(",\"type\":").Append(Int(p.type))
              .Append('}');
            if (i < count - 1) sb.Append(',');
            sb.Append('\n');
        }

        sb.Append("  ]\n}\n");
        return sb.ToString();
    }

    private static string Int(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return MapMetaData.DefaultVersion;
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
