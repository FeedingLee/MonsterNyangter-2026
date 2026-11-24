using System.Collections.Generic;
using UnityEngine;

// 울타리 앞뒤 자동 정렬 (Camp Zone에 붙임)
// - 울타리끼리: 아래(화면 앞)에 있는 조각일수록 위에 그림 → 기둥끼리 끼어 보이지 않음
// - 플레이어와: 플레이어 발보다 아래에 있는 조각은 플레이어 앞(frontLayer), 위에 있는 조각은 플레이어 뒤(behindLayer)
//   → 문쪽 울타리는 플레이어가 앞에, 뒤쪽 울타리는 플레이어가 울타리 뒤에 보임
// 같은 줄(rowSize 안)에 있는 조각끼리는 인스펙터에서 정한 원래 순서를 그대로 유지
[ExecuteAlways]
public class FenceDepthSort : MonoBehaviour
{
    [Tooltip("플레이어보다 앞(아래)에 있는 울타리가 쓰는 정렬 레이어 (Player 레이어보다 위)")]
    public string frontLayer = "Bottom Fence";
    [Tooltip("플레이어보다 뒤(위)에 있는 울타리가 쓰는 정렬 레이어 (Player 레이어보다 아래)")]
    public string behindLayer = "Fence";
    [Tooltip("이 높이 차이 안의 조각은 같은 줄로 보고 원래 순서를 유지")]
    public float rowSize = 0.12f;
    [Tooltip("이 오브젝트 밖에 있는 울타리 그림 (문 등). CampGate가 붙은 문은 자동 포함")]
    public List<SpriteRenderer> extraRenderers = new List<SpriteRenderer>();

    private struct Item { public SpriteRenderer r; public int baseOrder; public int row; }
    private readonly List<Item> items = new List<Item>();
    private bool collected;

    private void OnEnable() { collected = false; }

    private void Collect()
    {
        items.Clear();
        var list = new List<SpriteRenderer>(GetComponentsInChildren<SpriteRenderer>(true));
        foreach (SpriteRenderer r in extraRenderers) if (r != null && !list.Contains(r)) list.Add(r);
        foreach (CampGate g in FindObjectsOfType<CampGate>(true))
        {
            SpriteRenderer r = g.GetComponent<SpriteRenderer>();
            if (r != null && !list.Contains(r)) list.Add(r);
        }
        list.RemoveAll(r => r.GetComponentInParent<CampZoneGenerated>() != null);
        list.Sort((x, y) => x.transform.position.y.CompareTo(y.transform.position.y));
        // 아래부터 차례로 줄을 나눔: 바로 아래 조각과 rowSize 이내면 같은 줄
        int row = 0;
        float prevY = float.NegativeInfinity;
        foreach (SpriteRenderer r in list)
        {
            float y = r.transform.position.y;
            if (!float.IsNegativeInfinity(prevY) && y - prevY > rowSize) row++;   // 바로 아래 조각과 rowSize보다 떨어지면 새 줄
            prevY = y;
            // 원래 순서(-50~49)만 꺼냄 (이미 정렬된 값이어도 같은 결과)
            int baseOrder = ((r.sortingOrder % 100) + 150) % 100 - 50;
            items.Add(new Item { r = r, baseOrder = baseOrder, row = row });
        }
        collected = true;
    }

    // 에디터에서 플레이 전 모습 (플레이어가 캠프 앞에 있다고 보고 모두 뒤 레이어)
    [ContextMenu("지금 정렬 적용")]
    public void ApplyEditor()
    {
        Collect();
        Apply(float.NegativeInfinity);
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying) return;
        if (!collected) Collect();
        GameManager gm = GameManager.instance;
        float py = gm != null && gm.player != null ? gm.player.transform.position.y : float.NegativeInfinity;
        Apply(py);
    }

    private void Apply(float playerY)
    {
        foreach (Item it in items)
        {
            if (it.r == null) continue;
            string layer = it.r.transform.position.y < playerY ? frontLayer : behindLayer;
            int order = it.baseOrder - it.row * 100;               // 아래 줄일수록 위에 그림
            if (it.r.sortingLayerName != layer) it.r.sortingLayerName = layer;
            if (it.r.sortingOrder != order) it.r.sortingOrder = order;
        }
    }
}
