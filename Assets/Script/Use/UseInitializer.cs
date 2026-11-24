using TMPro;
using UnityEngine;

/// <summary>
/// USE 场景的引导脚本：拿地图名 → 交给 UseMap 加载地图和 _M → 显示名字。
/// </summary>
public class UseInitializer : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text nameDisplayText;

    [Header("引用（留空会自动在场景里找）")]
    [SerializeField] private UseMap useMap;

    private void Start()
    {
        if (useMap == null) useMap = FindObjectOfType<UseMap>();

        string mapName = SceneData.SelectedMapName;

        if (nameDisplayText != null)
        {
            nameDisplayText.text = string.IsNullOrEmpty(mapName) ? "No Map Selected" : mapName;
        }

        if (string.IsNullOrEmpty(mapName))
        {
            Debug.LogWarning("[UseInitializer] 没有收到地图名字");
            return;
        }

        if (useMap == null)
        {
            Debug.LogError("[UseInitializer] 场景里找不到 UseMap，请新建空物体挂上 UseMap 并绑定字段");
            return;
        }

        useMap.LoadFromMap(mapName);
    }
}
