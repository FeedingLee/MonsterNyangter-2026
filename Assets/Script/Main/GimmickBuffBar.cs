using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 활성 기믹 표시: 한계 돌파·위기 탈출·생명의 일격·집중·회피 반격·반격 본능 중 보유한 것만 아이콘으로 가로 나열
// - 위치: 머리 위 우클릭 쿨타임 표시(OverheadChargeUI)가 보이면 그 바로 위, 없으면 캐릭터 위 buffHeadOffsetY
// - 상태는 아이콘 진하기로 (기믹 상태는 읽기만 하고 효과는 바꾸지 않음)
//   발동(진함) / 비발동(흐림) 두 단계만. 집중 = 1중첩 이상 / 생명의 일격 = 준비 / 위기 탈출 = 이번 낮밤 사용 가능
//   한계 돌파 = 체력 조건 충족(발동 중) 진함 / 회피 반격 = 구르기 직후 준비 진함 / 반격 본능 = 발동 중 진함
//   비발동은 흐리고 회색, 발동 중은 진하고 살짝 크게
public class GimmickBuffBar : MonoBehaviour
{
    private const float PixelToWorld = 0.01f;                // 캔버스 1px = 0.01 월드 (머리 위 우클릭 표시와 같은 비율)

    private static readonly ItemData.ItemType[] Types =
    {
        ItemData.ItemType.LastStand, ItemData.ItemType.Escape, ItemData.ItemType.LifeStrike, ItemData.ItemType.Focus,
        ItemData.ItemType.DodgeCounter, ItemData.ItemType.CounterInstinct,
    };

    private RectTransform root;
    private readonly List<Image> icons = new List<Image>();

    public static GimmickBuffBar Create()
    {
        GameManager gm = GameManager.instance;
        if (gm == null || gm.player == null)
            return null;

        GameObject go = new GameObject("Gimmick Buff Bar", typeof(RectTransform));
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        int weaponLayer = SortingLayer.NameToID("Weapon");
        SpriteRenderer body = gm.player.GetComponent<SpriteRenderer>();
        bool valid = SortingLayer.IsValid(weaponLayer);
        canvas.sortingLayerID = valid ? weaponLayer : body.sortingLayerID;
        canvas.sortingOrder = valid ? 51 : body.sortingOrder + 51;   // 우클릭 표시(50) 위

        GimmickBuffBar bar = go.AddComponent<GimmickBuffBar>();
        bar.root = (RectTransform)go.transform;
        bar.root.sizeDelta = new Vector2(240f, 60f);
        bar.root.localScale = Vector3.one * PixelToWorld;
        foreach (ItemData.ItemType type in Types)
        {
            GameObject ig = new GameObject(type.ToString(), typeof(RectTransform));
            ig.transform.SetParent(bar.root, false);
            Image image = ig.AddComponent<Image>();
            image.sprite = GimmickCards.Icon(type);
            image.preserveAspect = true;
            image.raycastTarget = false;
            bar.icons.Add(image);
        }
        return bar;
    }

    private void LateUpdate()
    {
        GameManager gm = GameManager.instance;
        GimmickCatalog c = GimmickCatalog.Instance;
        if (gm == null || gm.player == null)
            return;

        // 보유한 기믹만, 상태별 진하기
        int shown = 0;
        for (int i = 0; i < Types.Length; i++)
        {
            int level = GimmickCards.Level(Types[i]);
            bool on = level > 0 && gm.isLive && gm.player.gameObject.activeInHierarchy;
            icons[i].enabled = on;
            if (!on) continue;
            float k = Strength(Types[i], level);
            float gray = Mathf.Lerp(c.buffInactiveGray, 0f, k);      // 비발동일수록 회색
            float v = Mathf.Lerp(1f, 0.55f, gray);
            icons[i].color = new Color(v, v, v, Mathf.Lerp(c.buffAlphaMin, c.buffAlphaMax, k));
            icons[i].rectTransform.sizeDelta = Vector2.one * c.buffIconSize;
            icons[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, c.buffActiveScale, k);
            shown++;
        }

        // 가로 나열 (가운데 정렬)
        float total = shown * c.buffIconSize + Mathf.Max(0, shown - 1) * c.buffIconSpacing;
        float x = -total * 0.5f + c.buffIconSize * 0.5f;
        for (int i = 0; i < icons.Count; i++)
        {
            if (!icons[i].enabled) continue;
            icons[i].rectTransform.anchoredPosition = new Vector2(x, 0f);
            x += c.buffIconSize + c.buffIconSpacing;
        }

        // 아이콘 크기를 1920x1080 화면 기준 px로 맞춤 (카메라 확대와 상관없이 buffIconSize px)
        Camera cam = Camera.main;
        float worldPerPx = cam != null && cam.orthographic ? cam.orthographicSize * 2f / 1080f : PixelToWorld;
        root.localScale = Vector3.one * worldPerPx;

        // 위치: 우클릭 쿨타임 표시 바로 위, 없으면 캐릭터 위
        Vector3 pos;
        OverheadChargeUI charge = OverheadChargeUI.Active;
        float half = c.buffIconSize * 0.5f * worldPerPx;
        if (charge != null && charge.Visible)
        {
            pos = charge.transform.position + Vector3.up * (charge.TopOffset + c.buffAboveChargeGap + half);
        }
        else
        {
            pos = gm.player.transform.position + Vector3.up * c.buffHeadOffsetY;
        }
        transform.position = pos;
    }

    // 0(흐림) ~ 1(진함)
    private static float Strength(ItemData.ItemType type, int level)
    {
        switch (type)
        {
            case ItemData.ItemType.Focus:
                return GimmickCards.FocusStacks > 0 ? 1f : 0f;
            case ItemData.ItemType.LifeStrike:
                return GimmickCards.LifeStrikeReady ? 1f : 0f;
            case ItemData.ItemType.Escape:
                return GimmickCards.EscapeAvailable ? 1f : 0f;
            case ItemData.ItemType.LastStand:
                return ExtraCards.LastStandActive ? 1f : 0f;              // 체력 조건을 채워 발동 중일 때만 진하게
            case ItemData.ItemType.DodgeCounter:
                return GimmickCards.DodgeCounterReady ? 1f : 0f;
            case ItemData.ItemType.CounterInstinct:
                return GimmickCards.CounterInstinctActive ? 1f : 0f;
            default:
                return 1f;
        }
    }
}
