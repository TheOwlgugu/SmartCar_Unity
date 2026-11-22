using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public enum MapMode
{
    None = 0,
    Road = 1,
    Wall = 2
}

/// <summary>
/// 地图编辑的核心：持有模型、铺格子、鼠标命中判定、引导球、脏标记与落盘。
/// 它不关心 UI 长什么样，模式由 EditManager 通过 SetMode() 推过来。
/// </summary>
public class EditMap : MonoBehaviour
{
    [Header("格子渲染")]
    [SerializeField] private Transform mapRoot;
    [SerializeField] private GameObject pointPrefab;
    [SerializeField] private Material roadMaterial;
    [SerializeField] private Material wallMaterial;

    [Header("引导球")]
    [Tooltip("指示当前选中格子的预制体，为空则不显示引导")]
    [SerializeField] private GameObject cellGuidePrefab;
    [Tooltip("可选：引导球的父节点。留空则挂在场景根节点，绝不会被重建地图时误删")]
    [SerializeField] private Transform guideRoot;
    [Tooltip("引导球相对地面的高度，编辑界面里直接调")]
    public float height = 0.5f;

    [Header("保存")]
    [Tooltip("最后一次绘制后多久自动落盘（秒）")]
    [SerializeField] private float autoSaveDelay = 1f;

    private MapFileData data;
    private string mapName;
    private readonly List<GameObject> points = new List<GameObject>();
    private readonly List<MeshRenderer> renderers = new List<MeshRenderer>();
    private GameObject guide;
    private MapMode mode = MapMode.None;
    private bool dirty;
    private float lastEditTime;
    private int lastPaintedX = int.MinValue;
    private int lastPaintedY = int.MinValue;

    public MapFileData Data { get { return data; } }
    public string MapName { get { return mapName; } }
    public int Width { get { return data != null ? data.Width : 0; } }
    public int Height { get { return data != null ? data.Height : 0; } }
    public MapMode Mode { get { return mode; } }

    private void Start()
    {
        EnsureRefs();
    }

    /// <summary>
    /// EditMap.Start 和 EditInitializer.Start 的先后顺序没有保证，
    /// 所以每个入口都先补齐引用，避免依赖脚本执行顺序。
    /// </summary>
    private void EnsureRefs()
    {
        if (mapRoot == null)
        {
            GameObject found = GameObject.Find("MAP");
            if (found != null) mapRoot = found.transform;
        }
    }

    // ---------------- 加载 / 重建 ----------------

    public void LoadFromMap(string name)
    {
        EnsureRefs();
        mapName = name;
        data = MapJson.Load(name);

        if (data == null)
        {
            Debug.LogWarning("[EditMap] 读取失败，用 10x10 全道路占位: " + name);
            data = MapFileData.Create(10, 10);
            dirty = true;
        }

        RebuildGrid();
        LogAscii();
    }

    public void ApplySize(int width, int height)
    {
        if (data == null) return;

        data.Resize(width, height);
        RebuildGrid();
        LogAscii();

        dirty = true;
        SaveNow(); // 改尺寸属于"结构性变更"，立即落盘更稳
    }

    private void RebuildGrid()
    {
        ClearGrid();

        if (data == null) return;
        if (mapRoot == null)
        {
            Debug.LogError("[EditMap] mapRoot 未绑定，无法生成格子");
            return;
        }
        if (pointPrefab == null)
        {
            Debug.LogError("[EditMap] pointPrefab 未绑定，无法生成格子");
            return;
        }

        float startX = -(data.Width - 1) * 0.5f;
        float startZ = -(data.Height - 1) * 0.5f;

        for (int y = 0; y < data.Height; y++)
        {
            for (int x = 0; x < data.Width; x++)
            {
                GameObject go = Instantiate(pointPrefab, mapRoot);
                go.transform.localPosition = new Vector3(startX + x, 0f, startZ + y);
                go.name = string.Format("Cell_{0}_{1}", x, y);

                points.Add(go);
                renderers.Add(go.GetComponentInChildren<MeshRenderer>());
            }
        }

        for (int y = 0; y < data.Height; y++)
        {
            for (int x = 0; x < data.Width; x++)
            {
                RefreshCellVisual(x, y);
            }
        }
    }

    private void ClearGrid()
    {
        for (int i = 0; i < points.Count; i++)
        {
            GameObject go = points[i];
            if (go == null) continue;
            go.SetActive(false);
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
        points.Clear();
        renderers.Clear();

        // 兜底：清掉 MAP 下所有残留子物体（历史上 EditInitializer 生成的那批也算）
        if (mapRoot != null)
        {
            for (int i = mapRoot.childCount - 1; i >= 0; i--)
            {
                GameObject child = mapRoot.GetChild(i).gameObject;
                if (child == null) continue;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }
    }

    private void RefreshCellVisual(int x, int y)
    {
        if (data == null) return;

        int index = data.IndexOf(x, y);
        if (index < 0 || index >= renderers.Count) return;

        MeshRenderer renderer = renderers[index];
        if (renderer == null) return;

        Material target = data.IsRoad(x, y) ? roadMaterial : wallMaterial;
        if (target != null) renderer.sharedMaterial = target;
    }

    // ---------------- 模式 / 绘制 ----------------

    public void SetMode(MapMode next)
    {
        mode = next;
        lastPaintedX = int.MinValue;
        lastPaintedY = int.MinValue;
        if (mode == MapMode.None) HideGuide();
    }

    private void Update()
    {
        UpdateGuideAndPaint();
        UpdateAutoSave();
    }

    private void UpdateAutoSave()
    {
        if (!dirty) return;
        if (Time.unscaledTime - lastEditTime < autoSaveDelay) return;
        SaveNow();
    }

    private void UpdateGuideAndPaint()
    {
        if (mode == MapMode.None || data == null)
        {
            HideGuide();
            return;
        }

        if (IsPointerOverUI())
        {
            HideGuide();
            return;
        }

        int cellX;
        int cellY;
        if (!TryGetCellUnderPointer(out cellX, out cellY))
        {
            HideGuide();
            lastPaintedX = int.MinValue;
            return;
        }

        ShowGuide(cellX, cellY);

        if (!Input.GetMouseButton(0))
        {
            lastPaintedX = int.MinValue;
            lastPaintedY = int.MinValue;
            return;
        }

        if (cellX == lastPaintedX && cellY == lastPaintedY) return;
        Paint(cellX, cellY);
    }

    private void Paint(int x, int y)
    {
        if (data == null) return;

        bool wall = mode == MapMode.Wall;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            wall = !wall; // Shift 临时反色
        }
        bool road = !wall;

        lastPaintedX = x;
        lastPaintedY = y;

        if (data.IsRoad(x, y) == road) return;

        data.SetRoad(x, y, road);
        RefreshCellVisual(x, y);   // 视觉只取决于 R，邻格无需重绘

        dirty = true;
        lastEditTime = Time.unscaledTime;
    }

    private bool TryGetCellUnderPointer(out int cellX, out int cellY)
    {
        cellX = 0;
        cellY = 0;

        if (data == null) return false;

        Camera cam = Camera.main;
        if (cam == null) return false;
        if (cam.transform.position.y <= 0f) return false; // 相机在地面以下时不画

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Plane ground = new Plane(Vector3.up, Vector3.zero);
        float distance;
        if (!ground.Raycast(ray, out distance) || distance <= 0f) return false;

        Vector3 hit = ray.GetPoint(distance);
        float startX = -(data.Width - 1) * 0.5f;
        float startZ = -(data.Height - 1) * 0.5f;

        cellX = Mathf.RoundToInt(hit.x - startX);
        cellY = Mathf.RoundToInt(hit.z - startZ);
        return data.InBounds(cellX, cellY);
    }

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void ShowGuide(int x, int y)
    {
        if (cellGuidePrefab == null || data == null) return;

        if (guide == null)
        {
            // 引导球绝不能挂在 MAP 下：重建地图会把子物体清空
            Transform parent = guideRoot;
            if (parent == null || parent == mapRoot || (mapRoot != null && parent.IsChildOf(mapRoot)))
            {
                parent = null;
            }

            guide = Instantiate(cellGuidePrefab, parent);
            guide.name = "CellGuide";
        }

        if (!guide.activeSelf) guide.SetActive(true);

        float startX = -(data.Width - 1) * 0.5f;
        float startZ = -(data.Height - 1) * 0.5f;
        guide.transform.position = new Vector3(startX + x, height, startZ + y);
    }

    private void HideGuide()
    {
        if (guide != null && guide.activeSelf) guide.SetActive(false);
    }

    // ---------------- 落盘 ----------------

    public void SaveNow()
    {
        if (data == null || string.IsNullOrEmpty(mapName)) return;

        if (MapJson.Save(mapName, data))
        {
            dirty = false;
            Debug.Log("[EditMap] 已保存: " + MapJson.FileFor(mapName));
        }
    }

    private void LogAscii()
    {
        if (data == null) return;
        Debug.Log("[EditMap] " + mapName + "\n" + data.ToAscii());
    }

    private void OnDisable()
    {
        if (dirty) SaveNow();
    }

    private void OnApplicationQuit()
    {
        if (dirty) SaveNow();
    }
}
