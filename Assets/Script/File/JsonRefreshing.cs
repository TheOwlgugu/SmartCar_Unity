using System;
using System.IO;
using TMPro;
using UnityEngine;

public class JsonRefreshing : MonoBehaviour
{
    [Header("Scroll View")]
    [SerializeField] private Transform contentParent;
    [SerializeField] private GameObject itemPrefab;

    [Header("UI Text")]
    [SerializeField] private TMP_Text mapCountText; // 新增：用于显示数量的文本

    [Header("Options")]
    [SerializeField] private bool refreshOnStart = true;

    private const string MapsFolderName = "MAPS";

    private string MapsFolderPath
    {
        get { return Path.Combine(Application.dataPath, MapsFolderName); }
    }

    private void Start()
    {
        if (refreshOnStart)
        {
            Refresh();
        }
    }

    public void Refresh()
    {
        if (contentParent == null || itemPrefab == null)
        {
            Debug.LogWarning("[JsonRefreshing] contentParent or itemPrefab is missing.");
            return;
        }

        ClearItems();

        if (!Directory.Exists(MapsFolderPath))
        {
            Directory.CreateDirectory(MapsFolderPath);
            UpdateMapCountText(0);
            return;
        }

        string[] files = Directory.GetFiles(MapsFolderPath, "*.json");
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        int validMapCount = 0; // 记录成功解析的地图数量

        foreach (string file in files)
        {
            string mapName = Path.GetFileNameWithoutExtension(file);
            float size = 0f;
            bool isValid = false;

            try
            {
                string json = File.ReadAllText(file);
                MapData data = JsonUtility.FromJson<MapData>(json);
                if (data != null)
                {
                    size = data.Height * data.Width;
                    isValid = true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[JsonRefreshing] Failed to read " + file + ": " + e.Message);
                continue;
            }

            if (isValid)
            {
                validMapCount++;

                GameObject item = Instantiate(itemPrefab, contentParent);
                MapItemView view = item.GetComponent<MapItemView>();
                if (view == null)
                {
                    view = item.GetComponentInChildren<MapItemView>();
                }

                if (view != null)
                {
                    view.SetData(mapName, size, this);
                }
                else
                {
                    Debug.LogWarning("[JsonRefreshing] Item prefab is missing MapItemView.");
                }
                //初始化进入场景的脚本
                MapItemTransition transition = item.GetComponent<MapItemTransition>();
                if (transition != null)
                {
                    transition.Init(mapName);
                }
                else
                {
                    Debug.LogWarning("[JsonRefreshing] Item prefab is missing MapItemTransition.");
                }

            }
        }

        // 刷新完成后更新文本
        UpdateMapCountText(validMapCount);
    }

    private void UpdateMapCountText(int count)
    {
        if (mapCountText != null)
        {
            mapCountText.text = count + "  maps";
        }
    }

    private void ClearItems()
    {
        for (int i = contentParent.childCount - 1; i >= 0; i--)
        {
            GameObject child = contentParent.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    [Serializable]
    private class MapData
    {
        public int Height;
        public int Width;
    }
}