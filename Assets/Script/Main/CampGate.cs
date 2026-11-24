using UnityEngine;

// 씬에 직접 배치한 텐트 문: 낮(상점 이용 시간)에는 열리고 밤에는 닫히는 애니메이션 (그림만, 출입 금지는 그대로)
// 프레임은 RunConfig의 gateOpenFrames / gateCloseFrames, 속도는 gateFps
[RequireComponent(typeof(SpriteRenderer))]
public class CampGate : MonoBehaviour
{
    private SpriteRenderer sr;
    private RunConfig rc;
    private bool open;
    private bool animating;
    private float time;

    private void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        rc = RunConfig.Instance;
        open = DayNightCycle.ShopAvailable;
        Sprite[] f = Frames(open);
        if (f != null && f.Length > 0) sr.sprite = f[f.Length - 1];   // 시작 상태 그대로 표시
    }

    private Sprite[] Frames(bool toOpen) => rc == null ? null : toOpen ? rc.gateOpenFrames : rc.gateCloseFrames;

    private void Update()
    {
        if (rc == null) return;
        bool want = DayNightCycle.ShopAvailable;
        if (want != open)
        {
            open = want;                                        // 열림 ↔ 닫힘 시작
            animating = true;
            time = 0f;
        }
        if (!animating) return;
        Sprite[] f = Frames(open);
        if (f == null || f.Length == 0) { animating = false; return; }
        time += Time.deltaTime;
        int i = Mathf.FloorToInt(time * rc.gateFps);
        if (i >= f.Length) { i = f.Length - 1; animating = false; }
        sr.sprite = f[i];
    }
}
