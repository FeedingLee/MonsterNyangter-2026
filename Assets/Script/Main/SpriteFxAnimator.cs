using UnityEngine;

// SpriteFx가 만든 이펙트의 프레임 넘기기 + 등장 시 살짝 커지기 + 마지막에 서서히 사라지기
// 반복(오라) 모드: 프레임을 계속 돌리며 살짝 숨쉬듯 커졌다 작아지고, 시간이 다 되거나 Stop()을 부르면 서서히 사라짐
public class SpriteFxAnimator : MonoBehaviour
{
    private SpriteRenderer target;
    private Sprite[] frames;
    private float size;
    private float frameTime;
    private float holdLast;
    private float time;
    private Color baseColor = Color.white;
    private Vector3 drift;
    public bool unscaled;            // true면 게임 정지(timeScale 0) 중에도 재생

    private bool loop;
    private float lifetime = -1f;     // 반복 모드 유지 시간 (음수면 Stop()까지)
    private float stopTime = -1f;     // 사라지기 시작한 시각
    private const float LoopFadeIn = 0.2f;
    private const float LoopFadeOut = 0.35f;

    public void Init(SpriteRenderer renderer, Sprite[] frames, float size, float fps, float holdLast)
    {
        target = renderer;
        this.frames = frames;
        this.size = size;
        frameTime = 1f / Mathf.Max(1f, fps);
        this.holdLast = Mathf.Max(0f, holdLast);
        baseColor = renderer.color;
        Apply(0);
    }

    public void InitLoop(SpriteRenderer renderer, Sprite[] frames, float size, float fps, float lifetime)
    {
        Init(renderer, frames, size, fps, 0f);
        loop = true;
        this.lifetime = lifetime;
        target.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
    }

    public void SetColor(Color color)
    {
        baseColor = color;
        if (target != null && !loop)
        {
            target.color = color;
        }
    }

    // 매 초 이동 (위로 떠오르는 반짝임 등)
    public void SetDrift(Vector3 velocity)
    {
        drift = velocity;
    }

    // 반복 이펙트를 서서히 끄고 제거
    public void Stop()
    {
        if (stopTime < 0f)
        {
            stopTime = time;
        }
    }

    private void Update()
    {
        float dt = unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
        time += dt;
        transform.position += drift * dt;

        if (loop)
        {
            UpdateLoop();
            return;
        }

        float playTime = frames.Length * frameTime;
        int index = Mathf.Min(frames.Length - 1, Mathf.FloorToInt(time / frameTime));
        Apply(index);

        // 첫 프레임은 작게 시작해 튀어나오듯 커짐
        float pop = Mathf.Clamp01(time / 0.08f);
        transform.localScale *= Mathf.Lerp(0.7f, 1f, pop);

        // 마지막 프레임을 잠깐 유지하며 사라짐
        if (time > playTime)
        {
            float fade = holdLast > 0f ? Mathf.Clamp01((time - playTime) / holdLast) : 1f;
            target.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * (1f - fade));
            if (fade >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }

    private void UpdateLoop()
    {
        if (lifetime >= 0f && stopTime < 0f && time >= lifetime)
        {
            stopTime = time;
        }

        int index = Mathf.FloorToInt(time / frameTime) % frames.Length;
        Apply(index);
        if (!steady) transform.localScale *= 1f + 0.05f * Mathf.Sin(time * 5f);   // 숨쉬듯 커졌다 작아짐 (범위 표시는 고정)

        float alpha = Mathf.Clamp01(time / LoopFadeIn);
        if (stopTime >= 0f)
        {
            float fade = Mathf.Clamp01((time - stopTime) / LoopFadeOut);
            alpha *= 1f - fade;
            if (fade >= 1f)
            {
                Destroy(gameObject);
                return;
            }
        }
        target.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha);
    }

    private void Apply(int index)
    {
        Sprite sprite = frames[index];
        if (sprite == null)
            return;

        target.sprite = sprite;
        float width = Mathf.Max(0.0001f, sprite.bounds.size.x);
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        float scale = size / width / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x));
        transform.localScale = new Vector3(scale, scale * stretchY, 1f);
    }

    public float stretchY = 1f;
    public bool steady;                                      // 범위 표시: 크기 고정 (꾸물거리지 않게)                              // 세로로만 늘림 (납작한 고리를 둥글게)
}
