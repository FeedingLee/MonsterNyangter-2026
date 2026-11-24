using UnityEngine;
using UnityEngine.SceneManagement;

// 맵을 편집할 때 가리지 않도록 씬에서 꺼 둔 UI 캔버스를, 게임이 실행되면 자동으로 켬
// (Main 씬의 최상위 "Canvas")
public static class SceneCanvasActivator
{
    private static readonly string[] CanvasNames = { "Canvas" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Main")
            return;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (!root.activeSelf && System.Array.IndexOf(CanvasNames, root.name) >= 0)
            {
                root.SetActive(true);
            }
        }
    }
}
