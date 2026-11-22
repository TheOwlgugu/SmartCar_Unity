using TMPro;
using UnityEngine;

/// <summary>
/// Edit 场景的引导脚本：拿地图名 → 交给 EditMap 加载 → 把宽高回填给 EditManager。
/// 本身不持有任何地图数据。
/// </summary>
public class EditInitializer : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text nameDisplayText;

    [Header("引用（留空会自动在场景里找）")]
    [SerializeField] private EditMap editMap;
    [SerializeField] private EditManager editManager;

    private void Start()
    {
        if (editMap == null) editMap = FindObjectOfType<EditMap>();
        if (editManager == null) editManager = FindObjectOfType<EditManager>();

        string mapName = SceneData.SelectedMapName;

        if (nameDisplayText != null)
        {
            nameDisplayText.text = string.IsNullOrEmpty(mapName) ? "No Map Selected" : mapName;
        }

        if (string.IsNullOrEmpty(mapName))
        {
            Debug.LogWarning("[EditInitializer] 没有收到地图名字");
            return;
        }

        if (editMap == null)
        {
            Debug.LogError("[EditInitializer] 场景里找不到 EditMap，请新建一个空物体挂上 EditMap 并绑定字段");
            return;
        }

        editMap.LoadFromMap(mapName);

        if (editManager != null)
        {
            editManager.SetSizeText(editMap.Width, editMap.Height);
        }
    }
}
