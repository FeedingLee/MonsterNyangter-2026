using UnityEngine;
using UnityEngine.UI;

// 랜스 철벽 스택 표시: 화면 왼쪽, 보관 슬롯 바로 위 (헤비보우건 잔탄 표시와 같은 자리)
// 어두운 둥근 판 + 금색 테두리, 왼쪽 방패 아이콘, 오른쪽 "스택/최대"
// 스택이 오르면 톡 커지며 금빛으로, 헛방어로 초기화되면 좌우로 흔들리며 붉게 번쩍임
public class LanceStackHud : MonoBehaviour
{
    private LanceMainSettings s;
    private RectTransform canvasRect;
    private RectTransform row;
    private Image back;
    private Image icon;
    private Text text;
    private float pop;                         // 커짐 연출 (1 → 0)
    private float shake;                       // 흔들림 연출 (1 → 0)
    private Color flashColor = Color.white;

    public static LanceStackHud Create(LanceMainSettings s)
    {
        Canvas canvas = RunUI.CreateCanvas("Lance Stack HUD", -3, false);
        LanceStackHud hud = canvas.gameObject.AddComponent<LanceStackHud>();
        hud.s = s;
        hud.canvasRect = (RectTransform)canvas.transform;

        hud.row = UltimateManager.CreateRect("Iron Stack", hud.canvasRect);
        hud.row.anchorMin = hud.row.anchorMax = Vector2.zero;
        hud.row.sizeDelta = s.stackRowSize;
        hud.back = hud.row.gameObject.AddComponent<Image>();
        hud.back.sprite = HeavyAmmoHud.PillSprite();
        hud.back.type = Image.Type.Sliced;
        hud.back.raycastTarget = false;

        Sprite shield = SpriteFx.ShieldIconSprite;
        if (shield != null)
        {
            hud.icon = RunUI.Icon("Icon", hud.row, shield, new Vector2(0.5f, 0.5f),
                new Vector2(-s.stackRowSize.x * 0.5f + 25f, 0f), new Vector2(34f, 34f));
            hud.icon.preserveAspect = true;
            hud.icon.raycastTarget = false;
        }

        hud.text = RunUI.Label("Count", hud.row, "", s.stackFontSize, Color.white, TextAnchor.MiddleRight,
            new Vector2(1f, 0.5f), new Vector2(-s.stackRowSize.x * 0.3f - s.stackTextRightPadding, 0f), new Vector2(s.stackRowSize.x * 0.6f, s.stackRowSize.y), 0f);   // 오른쪽 테두리에서 여백만큼 안쪽
        hud.text.fontStyle = FontStyle.Bold;
        hud.text.raycastTarget = false;
        RunUI.ApplyPanelFont(hud.text);                          // 헤비보우건 잔탄 표시와 같은 글꼴
        Outline line = hud.text.gameObject.AddComponent<Outline>();
        line.effectColor = Color.black;
        line.effectDistance = new Vector2(1.5f, -1.5f);
        return hud;
    }

    // 짧은 연출 (shakeRow: 초기화처럼 좌우로 흔들림)
    public void Pulse(Color color, bool shakeRow)
    {
        flashColor = color;
        if (shakeRow) shake = 1f;
        else pop = 1f;
    }

    public void Refresh(bool visible, int stacks, int max)
    {
        if (canvasRect.gameObject.activeSelf != visible) canvasRect.gameObject.SetActive(visible);
        if (!visible) return;

        // 보관 슬롯(빈 슬롯) 바로 위
        float baseY = s.stackRowBottom, x = s.stackRowLeft;
        GameObject slot = GameObject.Find(StatPanel.SlotButtonName);
        if (slot != null)
        {
            Vector3[] c = new Vector3[4];
            ((RectTransform)slot.transform).GetWorldCorners(c);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, c[1], null, out Vector2 topLeft);
            Vector2 offset = canvasRect.rect.size * 0.5f;
            baseY = topLeft.y + offset.y + 12f;
            x = topLeft.x + offset.x;
        }

        pop = Mathf.MoveTowards(pop, 0f, Time.unscaledDeltaTime / 0.3f);
        shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime / 0.4f);
        float shakeX = shake > 0f ? Mathf.Sin(Time.unscaledTime * 70f) * 9f * shake : 0f;

        row.anchoredPosition = new Vector2(x + s.stackRowSize.x * 0.5f + shakeX, baseY + s.stackRowSize.y * 0.5f);
        row.localScale = Vector3.one * (1f + 0.25f * pop);
        text.text = stacks + "/" + max;

        float flash = Mathf.Max(pop, shake);
        Color normal = stacks >= max ? s.stackGainColor : Color.white;   // 최대 스택은 금빛
        float alpha = stacks > 0 ? 1f : 0.7f;
        text.color = Color.Lerp(new Color(normal.r, normal.g, normal.b, alpha), flashColor, flash);
        back.color = new Color(1f, 1f, 1f, alpha);
        if (icon != null)
        {
            icon.color = Color.Lerp(new Color(1f, 1f, 1f, alpha), flashColor, flash * 0.6f);
        }
    }
}
