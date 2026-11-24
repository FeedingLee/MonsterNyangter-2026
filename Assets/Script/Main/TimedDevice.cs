using UnityEngine;
using UnityEngine.UI;

// 설치형 소모 아이템 (폭탄 통, 마비 두꺼비)
// 플레이어 위치에 설치 → 남은 시간을 숫자로 띄우고 깜빡이다가 터짐. 바닥에 효과 범위를 흐리게 표시한다.
// 폭탄 통: 범위 안의 적에게 큰 피해 (보스 포함, 보스 1회 피해 상한 무시). 플레이어는 피해 없음
// 마비 두꺼비: 범위 안의 적/보스 이동 불가 (보스는 원거리 패턴은 계속 사용)
public class TimedDevice : MonoBehaviour
{
    public enum Kind { Bomb, Toad }

    private static Canvas labelCanvas;

    private Kind kind;
    private float remain;
    private float delay;
    private float radius;
    private SpriteRenderer body;
    private SpriteRenderer range;
    private Text label;
    private Color baseColor = Color.white;
    private Color rangeColor = Color.white;

    public static void Spawn(Kind kind, Vector3 position)
    {
        RunConfig c = RunConfig.Instance;
        GameManager gm = GameManager.instance;
        SpriteRenderer playerSprite = gm.player.GetComponent<SpriteRenderer>();

        GameObject go = new GameObject(kind == Kind.Bomb ? "Bomb Barrel" : "Paralysis Toad");
        position = CampZone.FreeSpot(position, 0.5f);           // 텐트·동굴 등에 끼지 않게
        go.transform.position = position;
        TimedDevice device = go.AddComponent<TimedDevice>();
        device.kind = kind;
        device.delay = kind == Kind.Bomb ? c.bombDelay : c.toadDelay;
        device.remain = device.delay;
        device.radius = kind == Kind.Bomb ? c.bombRadius : c.toadRadius;

        // 효과 범위 (바닥에 흐리게, 몬스터보다 아래)
        GameObject rangeObject = new GameObject("Range");
        rangeObject.transform.SetParent(go.transform, false);
        device.range = rangeObject.AddComponent<SpriteRenderer>();
        device.range.sprite = CombatUtil.Circle;
        device.range.sharedMaterial = playerSprite.sharedMaterial;
        device.range.sortingLayerID = CombatUtil.EnemySortingLayer;      // 화살비 범위처럼 몬스터 아래에 그림
        device.range.sortingOrder = gm.mainWeapon.bow.rainAreaSortingOrder;
        device.rangeColor = kind == Kind.Bomb ? c.bombRangeColor : c.toadRangeColor;   // 폭탄: 옅은 붉은색, 두꺼비: 옅은 노란색
        device.range.color = device.rangeColor;
        float circleSize = Mathf.Max(0.0001f, CombatUtil.Circle.bounds.size.x);
        rangeObject.transform.localScale = Vector3.one * (device.radius * 2f / circleSize);

        // 범위는 이펙트 없이 원으로만 (실제 범위 크기 그대로, 깜빡임)
        device.range.enabled = true;

        // 본체
        GameObject bodyObject = new GameObject("Body");
        bodyObject.transform.SetParent(go.transform, false);
        bodyObject.transform.localScale = Vector3.one * (kind == Kind.Bomb ? c.bombScale : c.toadScale);
        device.body = bodyObject.AddComponent<SpriteRenderer>();
        device.body.sprite = kind == Kind.Bomb ? c.bombSprite : c.toadSprite;
        device.body.sharedMaterial = playerSprite.sharedMaterial;
        device.body.sortingLayerID = playerSprite.sortingLayerID;
        device.body.sortingOrder = playerSprite.sortingOrder;

        // 남은 시간 숫자
        if (labelCanvas == null)
        {
            labelCanvas = RunUI.CreateCanvas("Device Labels", -6, false);   // 레벨업 창, 상점보다 뒤
        }
        device.label = RunUI.Label("Countdown", labelCanvas.transform, "", 34, Color.white, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80f, 50f), 3f);

        ExtraCards.PlayClip(c.placeSound);                               // 설치 소리 (톡)
    }

    private void OnDestroy()
    {
        if (label != null)
        {
            Destroy(label.gameObject);
        }
    }

    private void Update()
    {
        GameManager gm = GameManager.instance;
        if (gm.isLive)
        {
            remain -= Time.deltaTime;
        }

        // 남은 시간이 줄수록 빠르게 깜빡임
        float speed = Mathf.Lerp(3f, 14f, 1f - Mathf.Clamp01(remain / delay));
        bool bright = Mathf.Repeat(Time.time * speed, 1f) < 0.5f;
        Color flash = kind == Kind.Bomb ? new Color(1f, 0.45f, 0.35f, 1f) : new Color(0.55f, 0.85f, 1f, 1f);
        body.color = bright ? flash : baseColor;

        // 범위 원: 본체 깜빡임과 같은 빠르기로 은은하게 깜빡임 (남은 시간이 줄수록 빨라짐)
        float wave = 0.5f + 0.5f * Mathf.Cos(Mathf.Repeat(Time.time * speed, 1f) * Mathf.PI * 2f);
        float blink = Mathf.Lerp(RunConfig.Instance.rangeBlinkMin, 1f, wave);
        range.color = new Color(rangeColor.r, rangeColor.g, rangeColor.b, rangeColor.a * blink);

        UpdateLabel();

        if (remain <= 0f)
        {
            Trigger();
        }
    }

    private void UpdateLabel()
    {
        Camera cam = Camera.main;
        if (cam == null || label == null)
            return;

        Vector3 top = transform.position + Vector3.up * (body.bounds.extents.y + 0.35f);
        Vector3 screen = cam.WorldToScreenPoint(top);
        label.gameObject.SetActive(screen.z > 0f);
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)labelCanvas.transform, screen, null, out Vector2 local);
        label.rectTransform.anchoredPosition = local;
        label.text = Mathf.CeilToInt(Mathf.Max(0f, remain)).ToString();
        label.color = remain <= 1f ? new Color(1f, 0.4f, 0.3f) : Color.white;
    }

    private void Trigger()
    {
        RunConfig c = RunConfig.Instance;
        Vector3 center = transform.position;

        if (kind == Kind.Bomb)
        {
            // 고정 피해 (공격력 버프 영향 없음), 보스 1회 피해 상한 무시
            CombatUtil.DamageInCircle(center, radius, c.bombDamage, false, true);
            SpriteFx.Explosion(center, radius * 1.6f);                       // 불꽃 → 폭발 → 연기 애니메이션
            ExtraCards.PlayClip(c.bombSound);
        }
        else
        {
            CombatUtil.StunInCircle(center, radius, c.toadMobStun, c.toadBossStun);
            SpriteFx.Zap(center, radius * 2f);                               // 파란 마법진 + 물결 + 거품
            ExtraCards.PlayClip(c.toadSound);
        }

        Destroy(gameObject);
    }
}
