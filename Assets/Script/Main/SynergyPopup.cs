using UnityEngine;
using UnityEngine.UI;

// 시너지 달성 팝업 (화면 중앙 카드: 시너지 이름, 단계, 효과 설명)
// 등장: 카드가 확대되며 뒤로 빛이 번짐 (unscaled time). 효과음 1회, 이펙트 1회. X 버튼으로 닫기
// 팝업이 떠 있는 동안 게임 일시정지 (GameManager.Stop / Resume)
public class SynergyPopup : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    private RectTransform card;
    private Image glow;
    private float time;
    private float scaleTime;
    private Sprite glowSprite;
    private Sprite[] ringFrames;
    private Sprite[] starFrames;
    private readonly System.Collections.Generic.List<Image> stars = new System.Collections.Generic.List<Image>();

    public static void Show(WeaponSynergyData.Entry entry, WeaponSynergy.Stage stage)
    {
        GameManager gm = GameManager.instance;
        if (gm == null)
            return;

        WeaponSynergyData d = WeaponSynergy.Data;
        RunConfig c = RunConfig.Instance;
        bool awakening = stage == WeaponSynergy.Stage.Awakening;
        Color accent = awakening ? d.awakeningColor : d.resonanceColor;

        Canvas canvas = RunUI.CreateCanvas("Synergy Popup", 40, true);
        SynergyPopup popup = canvas.gameObject.AddComponent<SynergyPopup>();
        popup.scaleTime = d.popupScaleTime;

        // 어두운 배경 (뒤 클릭 막기)
        Image dim = RunUI.Panel("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000f, 4000f));
        dim.raycastTarget = true;

        // 빛 번짐 (카드 뒤 둥근 빛이 커지며 옅어짐)
        // 빛 번짐: effect_2 금빛 고리가 카드 뒤에서 퍼짐 (없으면 둥근 빛)
        popup.ringFrames = SpriteFx.PopupRingFrames;
        bool hasRing = popup.ringFrames != null && popup.ringFrames.Length > 0 && popup.ringFrames[0] != null;
        popup.glowSprite = hasRing ? null : UiCircleSprite.Create();
        popup.glow = RunUI.Icon("Glow", canvas.transform, hasRing ? popup.ringFrames[0] : popup.glowSprite, new Vector2(0.5f, 0.5f), d.popupGlowOffset, new Vector2(760f, 330f));
        popup.glow.preserveAspect = false;
        popup.glow.color = hasRing ? new Color(1f, 1f, 1f, 0f) : new Color(accent.r, accent.g, accent.b, 0f);
        popup.glow.raycastTarget = false;
        popup.glow.enabled = false;                          // 카드 뒤 오라는 쓰지 않고 반짝임 파티클만

        // 반짝임 별 (effect_2): 카드 둘레에 몇 개
        popup.starFrames = SpriteFx.PopupStarFrames;
        if (popup.starFrames != null && popup.starFrames.Length > 0 && popup.starFrames[0] != null)
        {
            Vector2[] spots = { new Vector2(-270f, 150f), new Vector2(255f, 175f), new Vector2(-240f, -170f), new Vector2(275f, -140f), new Vector2(0f, 215f) };
            foreach (Vector2 spot in spots)
            {
                Image star = RunUI.Icon("Star", canvas.transform, popup.starFrames[0], new Vector2(0.5f, 0.5f), spot, Vector2.one * 56f);
                star.color = new Color(1f, 1f, 1f, 0f);
                popup.stars.Add(star);
            }
        }

        // 카드 (양피지)
        Sprite panel = d.popupPanelSprite != null ? d.popupPanelSprite : c.cardSprite != null ? c.cardSprite : c.panelSprite;
        Vector4 border = d.popupPanelSprite != null || c.cardSprite != null ? c.cardBorder : c.panelBorder;
        Image cardImage = RunUI.AssetPanel("Card", canvas.transform, panel, border, new Color(0.95f, 0.88f, 0.72f, 1f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 380f));
        popup.card = cardImage.rectTransform;
        foreach (Image s in popup.stars) s.transform.SetAsLastSibling();   // 반짝임이 판넬 위에 보이게
        Color text = c.parchmentTextColor;

        string stageName = awakening ? "각성" : "공명";
        RunUI.Label("Stage", popup.card, "시너지 " + stageName + "!", 32, accent, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0f, -55f), new Vector2(460f, 44f), 2f);
        // 이름 (큰 글씨) → 구분선 → 설명 (작은 글씨), 이름과 설명 사이 간격 popupTextGap
        string name = awakening ? entry.awakeningName : entry.resonanceName;
        const float nameY = -112f;
        const float nameHeight = 54f;
        RunUI.Label("Name", popup.card, "「" + name + "」", 40, text, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0f, nameY), new Vector2(480f, nameHeight), 0f);
        float nameBottom = nameY - nameHeight * 0.5f;
        Image divider = RunUI.Panel("Divider", popup.card, new Color(text.r, text.g, text.b, 0.45f),
            new Vector2(0.5f, 1f), new Vector2(0f, nameBottom - d.popupTextGap * 0.5f), new Vector2(360f, 3f));
        divider.sprite = null;
        float descTop = nameBottom - d.popupTextGap;

        string body = awakening
            ? entry.awakeningDesc + "\n\n<size=20>공명 「" + entry.resonanceName + "」 효과 유지\n" + entry.resonanceDesc + "</size>"
            : entry.resonanceDesc;
        // 설명: 구분선 아래 남은 칸의 가운데 (카드 아래 여백 40)
        const float cardBottom = -380f + 40f;
        float descHeight = descTop - cardBottom;
        Text desc = RunUI.Label("Desc", popup.card, body, 24, text, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0f, descTop - descHeight * 0.5f), new Vector2(440f, descHeight), 0f);
        desc.supportRichText = true;
        desc.horizontalOverflow = HorizontalWrapMode.Wrap;

        // X 버튼 (카드 오른쪽 위)
        Button close = RunUI.Button("Close", popup.card, "X", 28, new Color(0.55f, 0.3f, 0.2f, 1f),
            new Vector2(1f, 1f), new Vector2(-38f, -38f), new Vector2(52f, 52f), popup.Close);
        RunUI.SetButtonSprite(close, c.buttonRed);

        popup.card.localScale = Vector3.zero;
        IsOpen = true;
        gm.Stop();                                           // 팝업 동안 일시정지

        // 효과음 1회, 이펙트 1회 (레벨업 리소스 재활용)
        RunUI.PlayOr(d.popupSound, AudioManager.Sfx.Player_LevelUp);
        SpriteFx.LevelUp(gm.player.transform);
    }

    private void Update()
    {
        time += Time.unscaledDeltaTime;

        // 확대: 0 → 1.12 → 1 (강화 성공 느낌)
        float k = Mathf.Clamp01(time / scaleTime);
        float scale = k < 1f ? Mathf.Lerp(0f, 1.12f, 1f - (1f - k) * (1f - k)) : Mathf.Lerp(1.12f, 1f, Mathf.Clamp01((time - scaleTime) / 0.12f));
        card.localScale = Vector3.one * scale;

        if (ringFrames != null && ringFrames.Length > 0 && glowSprite == null)
        {
            // 금빛 고리 프레임이 퍼지며 사라지고, 1초마다 다시 은은하게 퍼짐
            float ringTime = scaleTime * 2.2f;
            float loop = time < ringTime ? time / ringTime : Mathf.Repeat((time - ringTime) / 1.2f, 1f);
            int frame = Mathf.Min(ringFrames.Length - 1, Mathf.FloorToInt(loop * ringFrames.Length));
            glow.sprite = ringFrames[frame];
            float strength = time < ringTime ? 1f : 0.45f;
            glow.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.15f, loop);
            glow.color = new Color(1f, 1f, 1f, strength * (1f - loop * 0.7f));
        }
        else
        {
            // 빛 번짐: 카드가 커질 때 같이 커지며 밝아졌다가 은은하게 남음
            float glowK = Mathf.Clamp01(time / (scaleTime * 2f));
            glow.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.4f, 2.2f, glowK);
            float alpha = Mathf.Lerp(0.85f, 0.25f, glowK) * (1f + 0.15f * Mathf.Sin(time * 4f));
            Color g = glow.color;
            glow.color = new Color(g.r, g.g, g.b, alpha);
        }

        // 반짝임 별: 각자 다른 박자로 반짝
        for (int i = 0; i < stars.Count; i++)
        {
            float t = Mathf.Repeat((time - scaleTime) * 1.3f + i * 0.37f, 1f);
            if (time < scaleTime)
            {
                t = 0f;
            }
            int frame = Mathf.Min(starFrames.Length - 1, Mathf.FloorToInt(t * starFrames.Length));
            stars[i].sprite = starFrames[frame];
            stars[i].color = new Color(1f, 1f, 1f, time < scaleTime ? 0f : Mathf.Sin(t * Mathf.PI));
        }
    }

    private void Close()
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        RunUI.ClickSound();
        if (GameManager.instance != null)
        {
            GameManager.instance.Resume();
        }
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        UiCircleSprite.Release(glowSprite);
        if (IsOpen)
        {
            IsOpen = false;
        }
    }
}
