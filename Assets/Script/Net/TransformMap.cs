using System.Text;

/// <summary>
/// 本地可读地图（Cell 对象数组）→ 传输用紧凑掩码（int[]）。
/// 纯函数：不碰文件、不碰 Unity API，只做数据转换，方便单独验证。
/// 位定义：N=1, E=2, S=4, W=8, R=16。
/// </summary>
public static class TransformMap
{
    public const int BitN = 1;
    public const int BitE = 2;
    public const int BitS = 4;
    public const int BitW = 8;
    public const int BitR = 16;

    public static int EncodeCell(MapCell cell)
    {
        if (cell == null || !cell.R) return 0;

        int value = BitR;
        if (cell.N) value |= BitN;
        if (cell.E) value |= BitE;
        if (cell.S) value |= BitS;
        if (cell.W) value |= BitW;
        return value;
    }

    public static int[] EncodeCells(MapFileData data)
    {
        if (data == null || data.Cells == null) return new int[0];

        int[] mask = new int[data.Cells.Length];
        for (int i = 0; i < data.Cells.Length; i++)
        {
            mask[i] = EncodeCell(data.Cells[i]);
        }
        return mask;
    }

    /// <summary>网络层发送前调用：得到 {"Version":1,"Width":..,"Height":..,"Cells":[..]} 的紧凑串。</summary>
    public static string EncodePayload(MapFileData data)
    {
        if (data == null) return null;

        int[] mask = EncodeCells(data);
        StringBuilder sb = new StringBuilder(64 + mask.Length * 3);
        sb.Append("{\"Version\":").Append(data.Version.ToString())
          .Append(",\"Width\":").Append(data.Width.ToString())
          .Append(",\"Height\":").Append(data.Height.ToString())
          .Append(",\"Cells\":[");
        for (int i = 0; i < mask.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(mask[i].ToString());
        }
        sb.Append("]}");
        return sb.ToString();
    }
}
