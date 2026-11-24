using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MapItemView : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text sizeText;
    [SerializeField] private Button deleteButton;

    private const string MapsFolderName = "MAPS";

    private string mapName;
    private JsonRefreshing refresher;

    private void Awake()
    {
        if (deleteButton != null)
        {
            deleteButton.onClick.AddListener(DeleteSelf);
        }
    }

    public void SetData(string mapName, float size, JsonRefreshing refresher)
    {
        this.mapName = mapName;
        this.refresher = refresher;

        if (nameText != null)
        {
            nameText.text = mapName;
        }

        if (sizeText != null)
        {
            sizeText.text = size.ToString("0.##") + "   blocks";
        }
    }

    private void DeleteSelf()
    {
        if (string.IsNullOrEmpty(mapName))
        {
            return;
        }

        string assetPath = "Assets/" + MapsFolderName + "/" + mapName + ".json";
        string metaAssetPath = "Assets/" + MapsFolderName + "/META/" + mapName + "_M.json";

#if UNITY_EDITOR
        if (!UnityEditor.AssetDatabase.DeleteAsset(assetPath))
        {
            Debug.LogWarning("[MapItemView] DeleteAsset failed: " + assetPath);
            return;
        }

        // _M 可能不存在（旧地图），删不掉也不算错
        UnityEditor.AssetDatabase.DeleteAsset(metaAssetPath);
#else
        string path = Path.Combine(Application.dataPath, MapsFolderName, mapName + ".json");
        string metaPath = Path.Combine(Application.dataPath, MapsFolderName, "META", mapName + "_M.json");

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            else
            {
                Debug.LogWarning("[MapItemView] File not found: " + path);
                return;
            }

            if (File.Exists(metaPath)) File.Delete(metaPath);
        }
        catch (Exception e)
        {
            Debug.LogError("[MapItemView] Delete failed: " + e.Message);
            return;
        }
#endif

        if (refresher != null)
        {
            refresher.Refresh();
        }
    }
}