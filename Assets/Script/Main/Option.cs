using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class Option : MonoBehaviour
{
    public GameObject player;        // 플레이 유무를 파악할 PlayerObject 받기
    public GameObject panel;         // Option Panel 오브젝트 배치 필요
    public Button optionButton;      // Option Button 오브젝트 배치 필요
    public Button continueButton;    // Option Panle -> Continue 배치 필요
    public GameObject uiJoy;         // JoyStick 오브젝트 배치 필요
    public GameObject GameResult;    // GameResult 오브젝트 배치 필요
    public Button giveUpButton;      // 사망하기(포기) 버튼 (비워두면 패널에서 GiveUpBtn과 연결된 버튼을 자동으로 찾음)

    private bool isPaused = false;
    private bool OptionPanel = false;
    private RectTransform continueRect;
    private Vector2 continueOriginalPos;
    private Vector2 continueCenterPos;

    private void Start()
    {
        // 게임 시작 시 Option Panel을 비활성화
        panel.SetActive(false);

        // 게임 시작 전에는 사망하기 버튼을 숨기고 뒤로가기 버튼을 가운데로 옮기기 위해 위치를 기억
        if (giveUpButton == null)
        {
            giveUpButton = FindGiveUpButton();
        }
        continueRect = continueButton.transform as RectTransform;
        if (continueRect != null)
        {
            continueOriginalPos = continueRect.anchoredPosition;
            continueCenterPos = continueOriginalPos;

            // 두 버튼이 같은 부모 아래 나란히 있으면 두 버튼의 가운데, 아니면 가로 중앙
            RectTransform giveUpRect = giveUpButton != null ? giveUpButton.transform as RectTransform : null;
            if (giveUpRect != null && giveUpRect.parent == continueRect.parent)
            {
                continueCenterPos = (continueOriginalPos + giveUpRect.anchoredPosition) * 0.5f;
            }
            else
            {
                continueCenterPos.x = 0f;
            }
        }

        // Option 버튼에 클릭 이벤트 리스너 등록
        optionButton.onClick.AddListener(ToggleOption);

        // Continue 버튼에 클릭 이벤트 리스너 등록
        continueButton.onClick.AddListener(ContinueGame);
    }

    private void Update()
    {
        // 게임 진행 중 ESC를 누르면 옵션창을 열고, 열려 있으면 닫음
        if (Input.GetKeyDown(KeyCode.Escape) && CanToggleByEscape())
        {
            // 버튼의 클릭 이벤트를 실행해 효과음(ClickSound)까지 클릭과 동일하게 동작시킴
            (OptionPanel ? continueButton : optionButton).onClick.Invoke();
        }

        /* [ 조건 ]
        1. LevelUpFinish 가 true 일때
        2. Player 오브젝트가 true 일때
        3. 게임결과 판넬이 False 일때 
        4. 옵션창이 꺼져있을 때
        =  조이스틱 활성화.*/
        if (LevelUp.instance.LevelUpFinish == true &&
            player.gameObject.activeInHierarchy == true &&
            GameResult.gameObject.activeInHierarchy == false &&
            OptionPanel == false)
        {
            uiJoy.SetActive(true);
        }
        // 레벨업이 끝나지 않았다면, 정지함수 실행
        else if (LevelUp.instance.LevelUpFinish == false)
        {
            GameManager.instance.Stop();
        }
        // 게임을 승리하거나 패배하여 종료된다면, 옵션 아이콘 비활성화
        else if (GameResult.gameObject.activeInHierarchy == true)
        {
            optionButton.gameObject.SetActive(false);
        }
    }

    // 캐릭터 선택, 레벨업, 게임 결과 화면에서는 ESC로 옵션을 열지 않음
    private bool CanToggleByEscape()
    {
        return !ConfirmPopup.IsOpen &&                         // 포기 확인 창이 떠 있으면 옵션을 닫지 않음
               player.gameObject.activeInHierarchy &&
               !GameResult.gameObject.activeInHierarchy &&
               LevelUp.instance.LevelUpFinish &&
               (GameManager.instance.isLive || OptionPanel);
    }

    private void ToggleOption()
    {
        isPaused = !isPaused;

        // Option Panel 활성화 상태 전환
        panel.SetActive(isPaused);

        if (isPaused)
        {
            ApplyButtonLayout();
        }

        // 게임 일시정지 및 재개
        if (isPaused)
        {
            OptionPanel = true;
            GameManager.instance.Stop();
            uiJoy.SetActive(false);
        }
        else
        {
            ContinueGame();
        }
    }

    // 게임 시작 전(캐릭터 선택 화면)에는 사망하기 버튼을 숨기고 뒤로가기 버튼을 가운데에 배치
    private void ApplyButtonLayout()
    {
        bool inGame = player.gameObject.activeInHierarchy;

        if (giveUpButton != null)
        {
            giveUpButton.gameObject.SetActive(inGame);
        }
        if (continueRect != null)
        {
            continueRect.anchoredPosition = inGame || giveUpButton == null ? continueOriginalPos : continueCenterPos;
        }
    }

    // 패널 안에서 GiveUpBtn.SceneChange가 연결된 버튼, 또는 GiveUpBtn이 붙은 버튼을 찾음
    private Button FindGiveUpButton()
    {
        foreach (Button button in panel.GetComponentsInChildren<Button>(true))
        {
            if (button.GetComponent<GiveUpBtn>() != null)
                return button;

            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentTarget(i) is GiveUpBtn)
                    return button;
            }
        }
        return null;
    }

    private void ContinueGame()
    {
        // 플레이어 오브젝트가 꺼져있으면, 게임 실행중이 아니기 때문에
        if (player.gameObject.activeInHierarchy == false)
        {
            panel.SetActive(false);
            uiJoy.SetActive(false); // 조이스틱 꺼진상태 유지
            isPaused = false;
        }
        else
        {
            panel.SetActive(false);
            GameManager.instance.Resume();
            isPaused = false;
            OptionPanel = false;
        }
    }
}
