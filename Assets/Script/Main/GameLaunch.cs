using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 타이틀 메뉴에서 고른 캐릭터·스테이지를 Main 씬의 기존 게임 시작에 전달
// - Main 씬이 열리면 기존 캐릭터 버튼(Character N)의 클릭을 그대로 실행한다 (GameStart 패널 끄기 → HUD 켜기 → GameManager.GameStart)
// - 선택은 그만두기 전까지 유지되므로, 다시하기(Main 씬 다시 불러오기)는 같은 캐릭터·스테이지로 처음부터 시작한다
// - 선택 없이 Main 씬을 바로 실행하면(에디터 테스트) 기존 캐릭터 선택 화면이 그대로 나온다
public static class GameLaunch
{
    public static int CharacterId { get; private set; } = -1;
    public static int StageIndex { get; private set; } = -1;
    public static string SceneName { get; private set; }
    public static bool HasSelection => CharacterId >= 0;

    public static void Select(int characterId, int stageIndex, string sceneName)
    {
        CharacterId = characterId;
        StageIndex = stageIndex;
        SceneName = sceneName;
    }

    public static void Clear()
    {
        CharacterId = -1;
        StageIndex = -1;
        SceneName = null;
    }

    // 그만두기: 선택을 지우고, 타이틀이 열리면 메인 메뉴 대신 캐릭터 선택 화면(마지막 캐릭터)을 보여 줌
    public static int LastCharacterId { get; private set; } = -1;
    public static bool OpenCharacterSelect { get; private set; }

    public static void QuitToCharacterSelect()
    {
        LastCharacterId = CharacterId;
        OpenCharacterSelect = true;
        Clear();
    }

    // 타이틀 메뉴가 요청을 읽은 뒤 지움
    public static bool ConsumeOpenCharacterSelect()
    {
        bool open = OpenCharacterSelect;
        OpenCharacterSelect = false;
        return open;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!HasSelection || scene.name != SceneName)
            return;

        // 기존 캐릭터 선택 화면이 한 프레임도 보이지 않게 바로 숨김
        Transform oldPanel = FindInScene(scene, MenuCatalog.Instance.oldSelectPanelPath);
        if (oldPanel != null)
        {
            oldPanel.gameObject.SetActive(false);
        }
        new GameObject("Game Launch").AddComponent<GameLaunchRunner>().Init(scene);
    }

    // "루트/자식/자식" 경로로 찾기 (꺼진 오브젝트 포함)
    public static Transform FindInScene(Scene scene, string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        int slash = path.IndexOf('/');
        string rootName = slash < 0 ? path : path.Substring(0, slash);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != rootName)
                continue;
            return slash < 0 ? root.transform : root.transform.Find(path.Substring(slash + 1));
        }
        return null;
    }
}

// Main 씬에서 한 프레임 뒤(모든 Start 이후) 기존 시작 버튼을 누르고, 검은 화면에서 밝아짐
public class GameLaunchRunner : MonoBehaviour
{
    private Scene scene;
    private Image overlay;

    public void Init(Scene target)
    {
        scene = target;

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        GameObject go = new GameObject("Fade", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        overlay = go.AddComponent<Image>();
        overlay.color = Color.black;
        overlay.raycastTarget = false;
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private IEnumerator Start()
    {
        yield return null;

        MenuCatalog cat = MenuCatalog.Instance;
        int id = GameLaunch.CharacterId;
        Transform buttonTransform = GameLaunch.FindInScene(scene, string.Format(cat.startButtonPath, id));
        Button button = buttonTransform != null ? buttonTransform.GetComponent<Button>() : null;
        if (button != null)
        {
            button.onClick.Invoke();
        }
        else
        {
            Debug.LogError($"[GameLaunch] 캐릭터 {id}의 기존 시작 버튼을 찾지 못했습니다: {string.Format(cat.startButtonPath, id)}");
        }

        float t = 0f;
        while (t < cat.mainFadeInTime)
        {
            t += Time.unscaledDeltaTime;
            overlay.color = new Color(0f, 0f, 0f, 1f - Mathf.Clamp01(t / cat.mainFadeInTime));
            yield return null;
        }
        Destroy(gameObject);
    }
}
