using UnityEngine;

public class ClickSound : MonoBehaviour
{
    private void Start()
    {
        // 클릭될 때 소리가 나도록 OnClick 메서드를 호출
        GetComponent<UnityEngine.UI.Button>().onClick.AddListener(PlayClickSound);
    }

    private void PlayClickSound()
    {
        // 레벨업 카드는 LevelUp.Hide에서 카드 선택 소리를 내므로 여기서는 생략 (소리 겹침 방지)
        if (GetComponent<Item>() != null)
            return;

        // AudioManager를 통해 효과음 재생
        AudioManager.instance.PlaySfx(AudioManager.Sfx.Select);
    }
}
