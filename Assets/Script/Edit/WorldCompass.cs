using TMPro;
using UnityEngine;

public class WorldCompass : MonoBehaviour
{
    [Header("Compass Settings")]
    [Tooltip("指示牌距离中心的距离，建议比地图最大尺寸稍微大一点")]
    [SerializeField] private float distance = 50f;
    [SerializeField] private float textHeight = 5f; // 字体的 Y 轴高度，稍微抬高防遮挡
    [SerializeField] private int fontSize = 50;
    [SerializeField] private Color textColor = Color.white;

    private void Start()
    {
        GenerateCompass();
    }

    [ContextMenu("Generate Direction Markers")]
    public void GenerateCompass()
    {
        // 清空旧指示器
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        // 根据新的坐标系：+Z是北，+X是东，-Z是南，-X是西
        CreateMarker("N", new Vector3(0, 0, distance), Color.red);        // 北
        CreateMarker("S", new Vector3(0, 0, -distance), Color.blue);      // 南
        CreateMarker("E", new Vector3(distance, 0, 0), Color.green);      // 东
        CreateMarker("W", new Vector3(-distance, 0, 0), Color.yellow);    // 西
    }

    private void CreateMarker(string text, Vector3 position, Color color)
    {
        GameObject markerObj = new GameObject("Marker_" + text);
        markerObj.transform.SetParent(transform);

        // 设置位置，把高度抬高一点，这样平铺在地上也能看见
        markerObj.transform.localPosition = position + new Vector3(0, textHeight, 0);

        // 添加并设置 TextMeshPro
        TextMeshPro tmp = markerObj.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.fontStyle = FontStyles.Bold;

        // 让文字平躺在地上，并面向原点方向，这样俯视角看过去很自然
        // 计算朝向原点的旋转
        Vector3 lookDir = -position.normalized;
        if (lookDir != Vector3.zero)
        {
            // 让文字面向摄像机或者说朝向原点
            markerObj.transform.rotation = Quaternion.LookRotation(lookDir) * Quaternion.Euler(0, 180, 0);
        }

        // 如果摄像机是 (90,0,0) 看向下方，文字需要跟着旋转一下躺平
        // 这里我们直接强制让它旋转成和默认摄像机垂直的角度
        markerObj.transform.rotation = Quaternion.Euler(90, markerObj.transform.eulerAngles.y, 0);
    }
}