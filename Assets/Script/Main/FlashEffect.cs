using UnityEngine;

// 작게 시작해 diameter 크기로 퍼지며 투명해지는 원형 효과 (끝나면 스스로 삭제)
public class FlashEffect : MonoBehaviour
{
    private SpriteRenderer target;
    private float diameter;
    private Color color;
    private float duration;
    private float time;

    public void Init(SpriteRenderer renderer, float diameter, Color color, float duration)
    {
        target = renderer;
        this.diameter = diameter;
        this.color = color;
        this.duration = Mathf.Max(0.01f, duration);
        Apply(0f);
    }

    private void Update()
    {
        time += Time.deltaTime;
        float p = Mathf.Clamp01(time / duration);
        Apply(p);
        if (p >= 1f)
        {
            Destroy(gameObject);
        }
    }

    private void Apply(float p)
    {
        if (target == null || target.sprite == null)
            return;

        float size = target.sprite.bounds.size.x;
        transform.localScale = Vector3.one * (Mathf.Lerp(diameter * 0.3f, diameter, p) / Mathf.Max(0.0001f, size));
        target.color = new Color(color.r, color.g, color.b, color.a * (1f - p));
    }
}
