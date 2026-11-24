using UnityEngine;

// UI가 나타날 때 작게 시작해 살짝 튀어나오며 커지는 효과 (게임이 멈춘 상태에서도 동작)
public class CardPop : MonoBehaviour
{
    private float time;
    private bool done;

    // 다시 나타날 때마다 효과를 처음부터 재생
    private void OnEnable()
    {
        time = 0f;
        done = false;
        transform.localScale = Vector3.one * 0.6f;
    }

    private void Update()
    {
        if (done)
            return;

        time += Time.unscaledDeltaTime;
        float p = Mathf.Clamp01(time / 0.25f);
        float overshoot = 1f + Mathf.Sin(p * Mathf.PI) * 0.08f;
        transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, p) * overshoot;
        if (p >= 1f)
        {
            transform.localScale = Vector3.one;
            done = true;
        }
    }
}
