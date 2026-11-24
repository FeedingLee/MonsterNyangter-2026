using UnityEngine;

// [임시 확인용] 캠프 비비기 처벌 테스트용 표시 - 테스트하고 잘 작동하면 이 파일과 CampLoiterPenalty.Create의 AddComponent 한 줄을 지울 것
// - 경고 영역: 붉은 테두리 / 상점 예외 영역: 파란 테두리
// - 화면 왼쪽: 누적 경고 시간, 처벌 여부, 남은 처벌 시간
public class CampLoiterDebug : MonoBehaviour
{
    public static bool Visible;                                // 테스트 모드 "캠프 처벌 확인창" 버튼으로 열고 닫음 (처음엔 닫힘)

    private CampLoiterPenalty penalty;
    private LineRenderer zoneLine, shopLine;
    private GUIStyle style;

    private void Start()
    {
        penalty = GetComponent<CampLoiterPenalty>();
        zoneLine = MakeLine("Debug Loiter Zone", new Color(1f, 0.15f, 0.15f, 0.9f));
        shopLine = MakeLine("Debug Shop Range", new Color(0.2f, 0.5f, 1f, 0.9f));
    }

    private LineRenderer MakeLine(string name, Color color)
    {
        LineRenderer l = new GameObject(name).AddComponent<LineRenderer>();
        l.transform.SetParent(transform, false);
        l.material = new Material(Shader.Find("Sprites/Default"));
        l.startColor = l.endColor = color;
        l.startWidth = l.endWidth = 0.08f;
        l.loop = true;
        l.positionCount = 4;
        l.useWorldSpace = true;
        l.sortingLayerName = "Weapon";
        l.sortingOrder = 100;
        return l;
    }

    private static void SetRect(LineRenderer l, Rect r)
    {
        l.SetPosition(0, new Vector3(r.xMin, r.yMin, 0f));
        l.SetPosition(1, new Vector3(r.xMax, r.yMin, 0f));
        l.SetPosition(2, new Vector3(r.xMax, r.yMax, 0f));
        l.SetPosition(3, new Vector3(r.xMin, r.yMax, 0f));
    }

    private void LateUpdate()
    {
        if (penalty == null) return;
        zoneLine.enabled = Visible;
        if (!Visible)
        {
            shopLine.enabled = false;
            return;
        }
        SetRect(zoneLine, penalty.Zone);
        bool shop = DayNightCycle.ShopAvailable && ShopKeeper.Instance != null && ShopKeeper.Instance.Present;
        shopLine.enabled = shop;
        if (shop)
        {
            Bounds b = UpgradeShop.RangeAnchor;
            float r = RunConfig.Instance.interactRadius;
            SetRect(shopLine, Rect.MinMaxRect(b.min.x - r, b.min.y - r, b.max.x + r, b.max.y + r));   // 실제 판정은 모서리가 둥근 범위
        }
    }

    private void OnGUI()
    {
        if (penalty == null || !Visible) return;
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = Mathf.RoundToInt(Screen.height / 50f) };
            style.normal.textColor = Color.white;
        }
        RunConfig rc = RunConfig.Instance;
        string text =
            "[캠프 처벌 확인용]\n" +
            "동작: " + (penalty.Running ? "낮" : "멈춤 (밤/보스/대기)") + "\n" +
            "영역 안: " + (penalty.InZone ? "예" : "아니오") + (penalty.Exempt ? " (상점 예외)" : "") + "\n" +
            "누적 경고: " + penalty.Accumulated.ToString("0.0") + " / " + penalty.Threshold.ToString("0") + "초\n" +
            "오늘 처벌 횟수: " + penalty.Stage + "\n" +
            "처벌: " + (CampLoiterPenalty.Punished ? "켜짐" : "꺼짐") + "\n" +
            "버틴 시간: " + penalty.Overtime.ToString("0.0") + "초 / 스택 " + penalty.Stack + " (새 몬스터 체력 +" + Mathf.RoundToInt(penalty.Stack * rc.penaltyHealthPerStack * 100f) + "%)\n" +
            "남은 처벌: " + (CampLoiterPenalty.Punished ? (penalty.Gauge / Mathf.Max(0.01f, rc.penaltyDecayPerSec)).ToString("0.0") + "초 (게이지 " + penalty.Gauge.ToString("0.0") + ")" : "-");
        float w = Screen.height * 0.52f, h = Screen.height * 0.23f;
        GUI.Box(new Rect(10f, Screen.height * 0.45f, w, h), text, style);
    }
}
