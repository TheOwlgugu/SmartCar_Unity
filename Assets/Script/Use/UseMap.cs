using System.Collections.Generic;
using UnityEngine;

public enum UseMode
{
    None = 0,
    SetStart = 1,
    SetGoal = 2
}

/// <summary>
/// USE 场景的核心：显示地图、标记起点/终点、显示障碍点，并维护 _M 文件。
/// 地图本体只读不写；所有改动都落在 {名称}_M.json 上。
/// </summary>
public class UseMap : MonoBehaviour
{
    [Header("格子渲染")]
    [SerializeField] private Transform mapRoot;
    [SerializeField] private GameObject pointPrefab;
    [SerializeField] private Material roadMaterial;
    [SerializeField] private Material wallMaterial;

    [Header("标记物")]
    [SerializeField] private GameObject startMarkerPrefab;
    [SerializeField] private GameObject goalMarkerPrefab;
    [SerializeField] private GameObject obstacleMarkerPrefab;
    [Tooltip("标记物父节点。留空则挂场景根；绝不能指向 MAP")]
    [SerializeField] private Transform markerRoot;
    [Tooltip("标记物离地高度")]
    public float markerHeight = 0.6f;

    [Header("引导球（可选）")]
    [SerializeField] private GameObject cellGuidePrefab;
    public float guideHeight = 0.5f;

    [Header("墙壁方块（可选）")]
    [Tooltip("在不可通行的格子上方实例化的预制体（比如一个方块），只做视觉，不参与任何逻辑")]
    [SerializeField] private GameObject wallBlockPrefab;
    [Tooltip("墙壁方块的高度。Unity 默认 Cube 边长 1、轴心在中心，填 0.5 正好贴地")]
    public float wallBlockHeight = 0.5f;

    [Header("保存")]
    [Tooltip("只在点 Save 时写盘；这里仅用于 Console 提示")]
    [SerializeField] private bool warnOnExitWithUnsavedChanges = true;

    private MapFileData map;
    private MapMetaData meta;
    private MapGridView grid;
    private string mapName;

    private UseMode mode = UseMode.None;
    private GameObject guide;
    private GameObject startMarker;
    private GameObject goalMarker;
    private readonly List<GameObject> obstacleMarkers = new List<GameObject>();
    private readonly List<GameObject> wallBlocks = new List<GameObject>();

    private bool dirty;

    public MapFileData Map { get { return map; } }
    public MapMetaData Meta { get { return meta; } }
    public string MapName { get { return mapName; } }
    public int Width { get { return map != null ? map.Width : 0; } }
    public int Height { get { return map != null ? map.Height : 0; } }
    public UseMode Mode { get { return mode; } }

    // ---------------- 加载 ----------------

    public void LoadFromMap(string name)
    {
        EnsureRefs();

        mapName = name;
        map = MapJson.Load(name);

        if (map == null)
        {
            Debug.LogError("[UseMap] 地图读取失败，用 10x10 占位: " + name);
            map = MapFileData.Create(10, 10);
        }

        meta = MapMetaJson.LoadOrDefault(name, map.Width, map.Height);
        if (meta == null)
        {
            meta = new MapMetaData();
            meta.Normalize(map.Width, map.Height);
        }

        RebuildGrid();
        BuildWallBlocks();
        RefreshAllMarkers();

        Debug.Log("[UseMap] " + name + "  " + map.Width + "x" + map.Height
                  + "  起点(" + meta.Find(MapPointType.Start).X + "," + meta.Find(MapPointType.Start).Y + ")"
                  + "  终点(" + meta.Find(MapPointType.Goal).X + "," + meta.Find(MapPointType.Goal).Y + ")"
                  + "  障碍 " + meta.ObstacleCount() + " 个");
    }

    private void EnsureRefs()
    {
        if (mapRoot == null)
        {
            GameObject found = GameObject.Find("MAP");
            if (found != null) mapRoot = found.transform;
        }
    }

    private void RebuildGrid()
    {
        grid = new MapGridView(mapRoot, pointPrefab, roadMaterial, wallMaterial);

        if (!grid.IsReady)
        {
            Debug.LogError("[UseMap] mapRoot / pointPrefab 未绑定，无法生成格子");
            return;
        }

        grid.Build(map);
    }

    // ---------------- 墙壁方块 ----------------

    /// <summary>
    /// 在每个不可通行的格子上方铺一层预制体（纯视觉）。
    /// 高度沿用 markerHeight，和起点/终点预制体保持一致。
    /// </summary>
    private void BuildWallBlocks()
    {
        ClearWallBlocks();

        if (wallBlockPrefab == null || map == null || grid == null) return;

        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                if (map.IsRoad(x, y)) continue;   // 只在墙格上放方块

                GameObject block = Instantiate(wallBlockPrefab, ResolveMarkerParent());
                block.name = string.Format("WallBlock_{0}_{1}", x, y);
                block.transform.position = CellPosition(x, y, wallBlockHeight);
                wallBlocks.Add(block);
            }
        }

        Debug.Log("[UseMap] 墙壁方块: " + wallBlocks.Count + " 个");
    }

    private void ClearWallBlocks()
    {
        for (int i = 0; i < wallBlocks.Count; i++) DestroyMarker(wallBlocks[i]);
        wallBlocks.Clear();
    }

    // ---------------- 模式 ----------------

    public void SetMode(UseMode next)
    {
        mode = next;
        if (mode == UseMode.None) HideGuide();
    }

    private void Update()
    {
        UpdateCursorAndClick();
    }

    private void UpdateCursorAndClick()
    {
        if (mode == UseMode.None || map == null || meta == null)
        {
            HideGuide();
            return;
        }

        if (MapRaycast.IsPointerOverUI())
        {
            HideGuide();
            return;
        }

        int cellX;
        int cellY;
        if (!MapRaycast.TryGetCell(Camera.main, map, out cellX, out cellY)
            || !map.IsRoad(cellX, cellY))   // 墙格不引导，也不允许放起点/终点
        {
            HideGuide();
            return;
        }

        ShowGuide(cellX, cellY);

        // 用"按下"而不是"按住"，避免一次拖动连改成串
        if (!Input.GetMouseButtonDown(0)) return;
        PlacePoint(cellX, cellY);
    }

    private void PlacePoint(int x, int y)
    {
        if (mode == UseMode.SetStart)
        {
            meta.SetPoint(MapPointType.Start, x, y, map.Width, map.Height);
            startMarker = MoveOrSpawn(startMarker, startMarkerPrefab, "StartMarker", x, y);
        }
        else if (mode == UseMode.SetGoal)
        {
            meta.SetPoint(MapPointType.Goal, x, y, map.Width, map.Height);
            goalMarker = MoveOrSpawn(goalMarker, goalMarkerPrefab, "GoalMarker", x, y);
        }

        MarkDirty();
    }

    // ---------------- 障碍（Nano 上报后调用） ----------------

    /// <summary>新增一个障碍点，返回是否真的加上了（重复格会被忽略）。</summary>
    public bool AddObstacle(int x, int y)
    {
        if (meta == null || map == null) return false;
        if (!meta.AddObstacle(x, y, map.Width, map.Height)) return false;

        GameObject marker = SpawnMarker(obstacleMarkerPrefab, "ObstacleMarker", x, y);
        if (marker != null) obstacleMarkers.Add(marker);

        MarkDirty();
        return true;
    }

    public void ClearObstacles()
    {
        if (meta == null) return;

        meta.ClearObstacles();
        for (int i = 0; i < obstacleMarkers.Count; i++) DestroyMarker(obstacleMarkers[i]);
        obstacleMarkers.Clear();

        MarkDirty();
    }

    // ---------------- 标记物 ----------------

    private void RefreshAllMarkers()
    {
        startMarker = DestroyMarker(startMarker);
        goalMarker = DestroyMarker(goalMarker);
        for (int i = 0; i < obstacleMarkers.Count; i++) DestroyMarker(obstacleMarkers[i]);
        obstacleMarkers.Clear();

        if (meta == null || map == null) return;

        MapPoint start = meta.Find(MapPointType.Start);
        if (start != null && meta.IsSet(MapPointType.Start, map.Width, map.Height))
        {
            startMarker = SpawnMarker(startMarkerPrefab, "StartMarker", start.X, start.Y);
        }

        if (meta.IsSet(MapPointType.Goal, map.Width, map.Height))
        {
            MapPoint goal = meta.Find(MapPointType.Goal);
            goalMarker = SpawnMarker(goalMarkerPrefab, "GoalMarker", goal.X, goal.Y);
        }

        List<MapPoint> obstacles = meta.FindAll(MapPointType.Obstacle);
        for (int i = 0; i < obstacles.Count; i++)
        {
            GameObject marker = SpawnMarker(obstacleMarkerPrefab, "ObstacleMarker", obstacles[i].X, obstacles[i].Y);
            if (marker != null) obstacleMarkers.Add(marker);
        }
    }

    private GameObject MoveOrSpawn(GameObject marker, GameObject prefab, string name, int x, int y)
    {
        if (prefab == null) return marker;

        if (marker == null) return SpawnMarker(prefab, name, x, y);

        if (!marker.activeSelf) marker.SetActive(true);
        marker.transform.position = CellPosition(x, y);
        return marker;
    }

    private GameObject SpawnMarker(GameObject prefab, string name, int x, int y)
    {
        if (prefab == null || grid == null) return null;

        GameObject marker = Instantiate(prefab, ResolveMarkerParent());
        marker.name = name;
        marker.transform.position = CellPosition(x, y);
        return marker;
    }

    private Vector3 CellPosition(int x, int y)
    {
        return CellPosition(x, y, markerHeight);
    }

    private Vector3 CellPosition(int x, int y, float height)
    {
        return grid != null ? grid.CellToWorld(x, y, height) : Vector3.zero;
    }

    private Transform ResolveMarkerParent()
    {
        if (markerRoot == null) return null;
        if (markerRoot == mapRoot) return null;
        if (mapRoot != null && markerRoot.IsChildOf(mapRoot)) return null;
        return markerRoot;
    }

    /// <summary>销毁标记物并返回 null，方便写成 startMarker = DestroyMarker(startMarker)。</summary>
    private GameObject DestroyMarker(GameObject marker)
    {
        if (marker == null) return null;

        marker.SetActive(false);
        if (Application.isPlaying) Destroy(marker);
        else DestroyImmediate(marker);
        return null;
    }

    // ---------------- 引导球 ----------------

    private void ShowGuide(int x, int y)
    {
        if (cellGuidePrefab == null || grid == null) return;

        if (guide == null)
        {
            guide = Instantiate(cellGuidePrefab, ResolveMarkerParent());
            guide.name = "CellGuide";
        }

        if (!guide.activeSelf) guide.SetActive(true);
        guide.transform.position = grid.CellToWorld(x, y, guideHeight);
    }

    private void HideGuide()
    {
        if (guide != null && guide.activeSelf) guide.SetActive(false);
    }

    // ---------------- 落盘 ----------------

    private void MarkDirty()
    {
        dirty = true;
    }

    public void SaveNow()
    {
        if (meta == null || map == null || string.IsNullOrEmpty(mapName)) return;

        if (MapMetaJson.Save(mapName, meta, map.Width, map.Height))
        {
            dirty = false;
            Debug.Log("[UseMap] 已保存: " + MapMetaJson.FileFor(mapName));
        }
    }

    private void OnDisable()
    {
        WarnUnsaved();
    }

    private void OnApplicationQuit()
    {
        WarnUnsaved();
    }

    private void WarnUnsaved()
    {
        if (!warnOnExitWithUnsavedChanges || !dirty) return;
        Debug.LogWarning("[UseMap] 有未保存的改动（没点 Save），已丢弃；文件仍是上次保存的内容。");
    }
}
