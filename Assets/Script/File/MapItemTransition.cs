using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MapItemTransition : MonoBehaviour
{
    [SerializeField] private Button editButton;
    [SerializeField] private Button useButton;

    private const string EditSceneName = "Edit";
    private const string UseSceneName = "USE";

    private string mapName;

    private void Awake()
    {
        if (editButton != null) editButton.onClick.AddListener(GoToEditScene);
        if (useButton != null) useButton.onClick.AddListener(GoToUseScene);
    }

    // 由 JsonRefreshing 调用，传入名字
    public void Init(string name)
    {
        mapName = name;
    }

    private void GoToEditScene() { EnterScene(EditSceneName); }
    private void GoToUseScene() { EnterScene(UseSceneName); }

    private void EnterScene(string sceneName)
    {
        if (string.IsNullOrEmpty(mapName))
        {
            Debug.LogWarning("[MapItemTransition] 地图名为空，无法进入 " + sceneName);
            return;
        }

        SceneData.SelectedMapName = mapName;
        SceneManager.LoadScene(sceneName);
    }
}
