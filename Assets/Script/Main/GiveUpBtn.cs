using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GiveUpBtn : MonoBehaviour
{
    public GameObject player; // Player 오브젝트를 참조하기 위한 변수

    private void Start()
    {
        // Player 오브젝트를 찾아서 변수에 할당
        if (player == null)
        {
            player = GameObject.Find("Player");
        }
    }

    // 포기하기 (옵션 창의 해골 버튼): 한 번 확인한 뒤 캐릭터를 즉사시켜 사망과 같은 실패 흐름으로 보냄
    // (Quest Failed → 결과 보기 → 결과 패널, 젬 보상은 사망과 같은 계산)
    public void SceneChange()
    {
        // Player 오브젝트가 활성화된 상태일 때만 작동
        if (player == null || !player.activeInHierarchy || GameManager.instance == null || GameManager.instance.health <= 0)
        {
            Debug.Log("Player 오브젝트가 비활성화 상태입니다. 포기할 수 없습니다.");
            return;
        }
        ConfirmPopup.Show("정말로 포기할 거냥?", "실패로 처리되고 결과 화면으로 갑니다.", "포기하기", GiveUp);
    }

    private void GiveUp()
    {
        // 옵션 창을 닫고 게임을 재개한 뒤 체력을 0으로 → Player가 사망 처리(쓰러짐 연출 + GameOver)
        Option option = FindObjectOfType<Option>();
        if (option != null && option.continueButton != null) option.continueButton.onClick.Invoke();
        else GameManager.instance.Resume();
        GameManager.instance.health = 0;
    }
}
