using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 캐릭터 머리 위 우클릭 충전·쿨타임 표시 (쌍검 백대쉬, 활 부채꼴, 랜스 패링 공용)
// 충전형: 칸 수 (충전 중인 칸은 아래부터 차오름) / 쿨타임형: 원형 게이지가 차오르고 다 차면 아이콘
public class OverheadChargeUI : MonoBehaviour
{
    private const float PixelToWorld = 0.01f;                // 캔버스 1px = 0.01 월드

    [Min(1f)] public float pipSize = 26f;                    // 칸 크기 (px)
    [Min(0f)] public float pipGap = 6f;
    [Min(1f)] public float ringSize = 44f;                   // 원형 게이지 크기 (px)
    [Min(1f)] public float iconSize = 60f;                   // 준비 완료 아이콘 크기 (px)
    public float headGap = 0.3f;                             // 머리 위 간격 (월드)
    public Color backColor = new Color(0.12f, 0.09f, 0.06f, 0.9f);
    public Color fillColor = new Color(1f, 0.82f, 0.3f, 1f);
    public Color chargingColor = new Color(1f, 0.82f, 0.3f, 0.6f);

    private Transform target;
    private SpriteRenderer body;
    private RectTransform root;
    private readonly List<Image> pipBacks = new List<Image>();
    private readonly List<Image> pipFills = new List<Image>();
    private Image ringBack, ringFill, icon;
    private int lastCount = -1;
    private bool lastReady = true;
    private int popIndex = -1;
    private float pop;

    public static OverheadChargeUI Create(Transform target, SpriteRenderer body)
    {
        GameObject go = new GameObject("Overhead Charge UI", typeof(RectTransform));
        go.transform.SetParent(target, false);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        int weaponLayer = SortingLayer.NameToID("Weapon");
        bool valid = SortingLayer.IsValid(weaponLayer);
        canvas.sortingLayerID = valid ? weaponLayer : body.sortingLayerID;
        canvas.sortingOrder = valid ? 50 : body.sortingOrder + 50;   // 무기·캐릭터 위 (휘두르는 칼에 가리지 않게)

        OverheadChargeUI ui = go.AddComponent<OverheadChargeUI>();
        Active = ui;
        ui.target = target;
        ui.body = body;
        ui.root = (RectTransform)go.transform;
        ui.root.sizeDelta = new Vector2(240f, 120f);
        ui.root.localScale = Vector3.one * PixelToWorld;
        return ui;
    }

    private Image NewImage(string name, Sprite sprite, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(root, false);
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.rectTransform.sizeDelta = size;
        return image;
    }

    // 충전형: count칸 가득, 다음 칸은 progress만큼
    public void ShowCharges(int count, int max, float progress)
    {
        gameObject.SetActive(true);
        SetRing(false);
        if (icon != null) icon.enabled = false;

        while (pipBacks.Count < max)
        {
            Image back = NewImage("Pip Back", CombatUtil.Circle, Vector2.one * pipSize);
            Image fill = NewImage("Pip Fill", CombatUtil.Circle, Vector2.one * pipSize * 0.72f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            pipBacks.Add(back);
            pipFills.Add(fill);
        }

        if (lastCount >= 0 && count > lastCount)
        {
            popIndex = count - 1;                            // 막 충전된 칸이 톡 커짐
            pop = 1f;
        }
        lastCount = count;
        pop = Mathf.MoveTowards(pop, 0f, Time.deltaTime / 0.25f);

        float total = max * pipSize + (max - 1) * pipGap;
        for (int i = 0; i < pipBacks.Count; i++)
        {
            bool on = i < max;
            pipBacks[i].enabled = on;
            pipFills[i].enabled = on;
            if (!on) continue;
            Vector2 pos = new Vector2(-total * 0.5f + pipSize * 0.5f + i * (pipSize + pipGap), 0f);
            float scale = i == popIndex ? 1f + 0.4f * pop : 1f;
            pipBacks[i].rectTransform.anchoredPosition = pos;
            pipFills[i].rectTransform.anchoredPosition = pos;
            pipBacks[i].rectTransform.localScale = Vector3.one * scale;
            pipFills[i].rectTransform.localScale = Vector3.one * scale;
            pipBacks[i].color = backColor;
            pipFills[i].fillAmount = i < count ? 1f : i == count ? Mathf.Clamp01(progress) : 0f;
            pipFills[i].color = i < count ? fillColor : chargingColor;
        }
    }

    // 쿨타임형: 원형 게이지가 차오르고, 준비되면 아이콘
    public void ShowCooldown(float progress, bool ready, Sprite iconSprite)
    {
        gameObject.SetActive(true);
        foreach (Image p in pipBacks) p.enabled = false;
        foreach (Image p in pipFills) p.enabled = false;

        if (ringBack == null)
        {
            ringBack = NewImage("Ring Back", CombatUtil.Circle, Vector2.one * ringSize);
            ringFill = NewImage("Ring Fill", CombatUtil.Circle, Vector2.one * ringSize * 0.8f);
            ringFill.type = Image.Type.Filled;
            ringFill.fillMethod = Image.FillMethod.Radial360;
            ringFill.fillOrigin = (int)Image.Origin360.Top;
            ringFill.fillClockwise = true;
        }
        if (icon == null)
        {
            icon = NewImage("Ready Icon", iconSprite, Vector2.one * iconSize);
            icon.preserveAspect = true;
        }
        if (iconSprite != null) icon.sprite = iconSprite;

        if (ready && !lastReady)
        {
            pop = 1f;                                        // 다시 쓸 수 있게 된 순간 살짝 커졌다 돌아옴
        }
        lastReady = ready;
        pop = Mathf.MoveTowards(pop, 0f, Time.deltaTime / 0.25f);

        SetRing(!ready);
        icon.enabled = ready && icon.sprite != null;
        icon.rectTransform.localScale = Vector3.one * (1f + 0.35f * pop);
        ringBack.color = backColor;
        ringFill.color = fillColor;
        ringFill.fillAmount = Mathf.Clamp01(progress);
    }

    // 활성 기믹 표시가 이 표시 바로 위에 붙도록 (읽기만)
    public static OverheadChargeUI Active { get; private set; }
    public bool Visible => gameObject.activeInHierarchy;
    public float TopOffset => Mathf.Max(pipSize, Mathf.Max(ringSize, iconSize)) * 0.5f * PixelToWorld;   // 가운데에서 윗변까지 (월드)

    public void Hide()
    {
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    private void SetRing(bool on)
    {
        if (ringBack != null) ringBack.enabled = on;
        if (ringFill != null) ringFill.enabled = on;
    }

    private void LateUpdate()
    {
        if (target == null || body == null)
            return;
        float top = body.bounds.max.y - target.position.y;
        transform.position = target.position + Vector3.up * (top + headGap);
    }
}

// 우클릭 충전 횟수 (최대 max회, 1회 충전에 chargeTime초, 한 칸씩 차례로)
public class ChargeCounter
{
    public int Count = -1;                                   // -1: 처음에 가득 채움
    public float Progress;                                   // 충전 중인 칸 진행도 (0~1)

    // 매 프레임 호출. 1회 충전이 끝난 순간 true
    public bool Tick(int max, float chargeTime)
    {
        if (Count < 0 || TestMode.NoSkillCooldown)
        {
            bool filled = Count >= 0 && Count < max;
            Count = max;
            Progress = 0f;
            return filled;
        }
        if (Count >= max)
        {
            Count = max;
            Progress = 0f;
            return false;
        }
        Progress += Time.deltaTime / Mathf.Max(0.01f, chargeTime);
        if (Progress < 1f)
            return false;
        Progress = 0f;
        Count++;
        return true;
    }

    public void Use()
    {
        Count = Mathf.Max(0, Count - 1);
    }
}
