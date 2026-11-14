using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MapItemTransition : MonoBehaviour
{
    [SerializeField] private Button editButton;

    private string mapName;

    private void Awake()
    {
        if (editButton != null)
        {
            editButton.onClick.AddListener(GoToEditScene);
        }
    }

    // 由 JsonRefreshing 调用，传入名字
    public void Init(string name)
    {
        mapName = name;
    }

    private void GoToEditScene()
    {
        if (string.IsNullOrEmpty(mapName))
        {
            Debug.LogWarning("[MapItemTransition] Map name is empty, cannot enter Edit scene.");
            return;
        }

        // 1. 把名字存入静态类
        SceneData.SelectedMapName = mapName;

        // 2. 切换场景
        SceneManager.LoadScene("Edit");
    }
}