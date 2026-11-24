using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.UIElements;

public class JoystickController : MonoBehaviour
{
    public RectTransform joystickBackground;            // 조이스틱 배경 RectTransform
    public RectTransform joystickHandle;                // 조이스틱 핸들 RectTransform
    public Canvas canvas;                               // 조이스틱이 위치할 캔버스
    public Camera mainCamera;                           // 메인 카메라 (사용되지 않았지만 추가 가능성)
    public GameObject player;                           // 플레이어 객체
    public float moveSpeed = 5f;                        // 플레이어의 이동 속도
    public GameObject LevelUpUi;                        // 레벨 업 UI

    private Vector2 inputVec;                           // 조이스틱 입력 벡터
    private bool isJoy;                                 // 조이스틱이 활성 상태인지 여부

    private void Start()
    {
        joystickBackground.gameObject.SetActive(false); // 시작 시 조이스틱 배경 비활성화
        joystickHandle.gameObject.SetActive(false);     // 시작 시 조이스틱 핸들 비활성화
        isJoy = false;                                  // 조이스틱 비활성 상태로 초기화
    }

    private void Update()
    {
        if (Input.touchCount > 0)                                  // 터치가 하나 이상 감지되었을 때
        {
            Touch touch = Input.GetTouch(0);                       // 첫 번째 터치 정보 가져오기

            if (touch.phase == TouchPhase.Began)                   // 터치가 시작될 때
            {
                if (!IsTouchingUI(touch))                          // 터치가 UI 요소를 누른 것이 아니라면
                {
                    SetJoystickPosition(touch.position);           // 터치 위치에 조이스틱 배경 위치 설정
                    joystickBackground.gameObject.SetActive(true); // 조이스틱 배경 표시
                    joystickHandle.gameObject.SetActive(true);     // 조이스틱 핸들 표시
                    isJoy = true;                                  // 조이스틱 활성화 상태로 설정
                }
            }
            else if (isJoy && (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary))        // 터치가 이동 중이거나 가만히 있을 때
            {
                MoveJoystickHandle(touch.position);                                                             // 조이스틱 핸들을 터치 위치에 맞게 이동
                inputVec = (joystickHandle.anchoredPosition / (joystickBackground.sizeDelta.x / 2)).normalized; // 입력 벡터를 계산하여 정규화
            }
            else if (isJoy && (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled))          // 터치가 끝나거나 취소되었을 때
            {
                ResetJoystick();                                                                                // 터치가 끝났을 때 조이스틱을 초기화하고 숨김
            }
        }
    }

    // 터치가 UI를 눌렀는지 확인하는 함수
    private bool IsTouchingUI(Touch touch)
    {
        PointerEventData pointerData = new PointerEventData(EventSystem.current);                               // 터치 위치에 대한 포인터 데이터 생성
        pointerData.position = touch.position;                                                                  // 터치의 위치를 설정

        List<RaycastResult> results = new List<RaycastResult>();                                                // Raycast 결과를 저장할 리스트 생성
        EventSystem.current.RaycastAll(pointerData, results);                                                   // 현재 터치가 UI에 맞는지 확인

        foreach (RaycastResult result in results)                                                               // 결과를 순회하면서
        {
            if (result.gameObject.name == "Option Button" || result.gameObject.name == UltimateManager.ButtonObjectName || result.gameObject.name == DodgeCooldownUI.ButtonObjectName || result.gameObject.name == MainWeaponSkillButton.ButtonObjectName || result.gameObject.name == StatPanel.SlotButtonName)                                                    // 만약 "Option Button"이라는 UI를 터치했다면
            {
                return true;                                                                                    // 터치가 UI를 누른 것임
            }
        }

        return false;                                                                                           // 터치가 UI가 아님
    }

    // 터치 위치에 맞춰 조이스틱의 배경 위치를 설정하는 함수
    private void SetJoystickPosition(Vector2 screenPosition)
    {
        if (GameManager.instance.isLive)                                                                        // 게임이 진행 중일 때만 조이스틱 설정
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform, screenPosition, null, out Vector2 anchoredPosition);         // 화면의 터치 좌표를 로컬 좌표로 변환

            joystickBackground.anchoredPosition = anchoredPosition;                                             // 변환된 좌표를 조이스틱 배경에 적용
            joystickHandle.anchoredPosition = Vector2.zero;                                                     // 핸들을 배경의 중앙에 위치
        }
    }

    // 조이스틱 핸들을 터치 위치에 맞게 이동시키는 함수
    private void MoveJoystickHandle(Vector2 screenPosition)
    {
        // 터치 위치를 캔버스의 로컬 좌표로 변환
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform, screenPosition, null, out Vector2 localPoint);

        // 배경의 위치와 터치 위치 간의 차이를 계산
        Vector2 offset = localPoint - joystickBackground.anchoredPosition;

        // 조이스틱 배경의 반지름을 계산
        float radius = joystickBackground.sizeDelta.x / 2;

        // 만약 터치 위치가 조이스틱 배경의 범위를 넘으면, 범위 내로 제한
        if (offset.magnitude > radius)
        {
            offset = offset.normalized * radius; // 정규화된 값으로 조정
        }

        // 핸들의 위치를 터치에 맞게 설정
        joystickHandle.anchoredPosition = offset;
    }

    // 조이스틱을 초기화하고 숨기는 함수
    public void ResetJoystick()
    {
        joystickBackground.gameObject.SetActive(false); // 조이스틱 배경 숨기기
        joystickHandle.gameObject.SetActive(false);     // 조이스틱 핸들 숨기기
        isJoy = false;                                  // 조이스틱 비활성화 상태로 설정
        inputVec = Vector2.zero;                        // 입력 벡터 초기화

        //Debug.Log("조이스틱 초기화");                 // 함수가 제대로 작동하는지 확인하는 로그
    }

    // 조이스틱 입력 벡터를 반환하는 함수
    public Vector2 GetInputVector()
    {
        if (isJoy)                                      // 터치 조이스틱 사용 중이면 조이스틱 입력 우선
        {
            return inputVec;                            // 현재 입력 벡터 반환
        }

        if (!GameManager.instance.isLive)               // 게임이 멈춘 동안에는 키보드 입력 무시
        {
            return Vector2.zero;
        }

        return GetKeyboardVector();                     // PC 빌드용 키보드 입력 반환
    }

    // WASD와 방향키 입력을 정규화된 벡터로 반환하는 함수
    private Vector2 GetKeyboardVector()
    {
        Vector2 keyboardVec = Vector2.zero;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) keyboardVec.y += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) keyboardVec.y -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) keyboardVec.x += 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) keyboardVec.x -= 1f;

        return keyboardVec.normalized;                  // 대각선 이동 시 속도가 빨라지지 않도록 정규화
    }
}
