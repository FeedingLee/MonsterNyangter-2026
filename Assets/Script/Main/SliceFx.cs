using UnityEngine;

// 몸이 사선으로 두 동강 나며 사라지는 처치 연출 (대검 모아 베기 한 방 처치)
// 쓰러진 몬스터 그림을 두 장 복사해, 사선 마스크로 위·아래 반쪽만 보이게 하고 서로 반대로 미끄러지며 사라짐
public class SliceFx : MonoBehaviour
{
    private float duration = 0.45f;
    private SpriteRenderer upper;
    private SpriteRenderer lower;
    private Vector2 cutDir;
    private float time;
    private Color baseColor;

    public static void Play(SpriteRenderer source, float duration = 0.45f)
    {
        if (source == null || source.sprite == null) return;
        GameObject root = new GameObject("Slice Fx");
        root.transform.position = source.transform.position;
        SliceFx fx = root.AddComponent<SliceFx>();
        fx.duration = Mathf.Max(0.1f, duration);
        float angle = Random.Range(-35f, 35f);
        fx.cutDir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        fx.baseColor = source.color;

        // 사선 마스크: 자른 선 위쪽 절반을 덮는 큰 사각형
        int order = source.sortingOrder;
        GameObject maskGo = new GameObject("Cut Mask");
        maskGo.transform.SetParent(root.transform, false);
        maskGo.transform.position = source.bounds.center;
        maskGo.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        SpriteMask mask = maskGo.AddComponent<SpriteMask>();
        mask.sprite = SquareSprite();
        float big = Mathf.Max(source.bounds.size.x, source.bounds.size.y) * 3f;
        maskGo.transform.localScale = new Vector3(big, big * 0.5f, 1f);
        maskGo.transform.position += maskGo.transform.up * big * 0.25f;   // 선 위쪽만 덮음
        mask.isCustomRangeActive = true;
        mask.frontSortingLayerID = mask.backSortingLayerID = source.sortingLayerID;
        mask.frontSortingOrder = order + 2;
        mask.backSortingOrder = order;

        fx.upper = Copy(source, root.transform, SpriteMaskInteraction.VisibleInsideMask, order + 1);
        fx.lower = Copy(source, root.transform, SpriteMaskInteraction.VisibleOutsideMask, order + 1);

        SpriteFx.HitSpark(source.bounds.center, 1.2f);
    }

    private static SpriteRenderer Copy(SpriteRenderer src, Transform parent, SpriteMaskInteraction mode, int order)
    {
        SpriteRenderer r = new GameObject("Half").AddComponent<SpriteRenderer>();
        r.transform.SetParent(parent, false);
        r.transform.position = src.transform.position;
        r.transform.localScale = src.transform.lossyScale;
        r.sprite = src.sprite;
        r.flipX = src.flipX;
        r.color = Color.white;
        r.sharedMaterial = src.sharedMaterial;
        r.sortingLayerID = src.sortingLayerID;
        r.sortingOrder = order;
        r.maskInteraction = mode;
        return r;
    }

    private static Sprite square;

    private static Sprite SquareSprite()
    {
        if (square == null)
        {
            Texture2D t = new Texture2D(4, 4);
            Color[] px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = Color.white;
            t.SetPixels(px);
            t.Apply();
            square = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        }
        return square;
    }

    private void Update()
    {
        time += Time.deltaTime;
        float k = Mathf.Clamp01(time / duration);
        Vector2 normal = new Vector2(-cutDir.y, cutDir.x);
        float slide = 0.35f * k;
        upper.transform.localPosition = (Vector3)(cutDir * slide + normal * slide * 0.4f);
        lower.transform.localPosition = (Vector3)(-cutDir * slide - normal * slide * 0.2f);
        // 처음엔 하얗게 번쩍인 뒤 원래 색으로, 끝에서 서서히 사라짐
        Color c = Color.Lerp(Color.white * 2f, baseColor, Mathf.Clamp01(k * 4f));
        c.a = 1f - Mathf.Clamp01((k - 0.4f) / 0.6f);
        upper.color = c;
        lower.color = c;
        if (k >= 1f) Destroy(gameObject);
    }
}
