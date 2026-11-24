using System.Collections.Generic;
using UnityEngine;

// 밤 웨이브 경로: 자식 오브젝트(웨이포인트)를 순서대로 잇는 길 (숲 스폰 지점 → 캠프 입구 문)
// 씬에서 자식 위치를 옮겨 경로를 고칠 수 있고, 에디터에 경로 선이 보인다.
public class WavePath : MonoBehaviour
{
    public static WavePath Instance { get; private set; }

    public Color gizmoColor = new Color(1f, 0.6f, 0.1f, 1f);
    [Min(0f)] public float gateRadius = 0.6f;                   // 입구 판정 표시 (경로 끝)
    [Min(0f)] public float cornerBlend = 1.2f;                  // 꺾이는 곳 앞뒤 이 거리 동안 옆줄 방향을 부드럽게 돌림 (옆줄 몬스터가 모서리에서 튀지 않게)

    private readonly List<Vector2> points = new List<Vector2>();
    private readonly List<float> cumulative = new List<float>();   // 각 점까지 누적 거리

    public float Length { get; private set; }
    public Vector2 Start => points.Count > 0 ? points[0] : (Vector2)transform.position;
    public Vector2 End => points.Count > 0 ? points[points.Count - 1] : (Vector2)transform.position;

    private void OnEnable()
    {
        Instance = this;
        Rebuild();
    }

    private void OnDisable()
    {
        if (Instance == this) Instance = null;
    }

    // 웨이포인트 위치로 경로 다시 계산
    public void Rebuild()
    {
        points.Clear();
        cumulative.Clear();
        foreach (Transform child in transform) points.Add(child.position);
        float total = 0f;
        for (int i = 0; i < points.Count; i++)
        {
            if (i > 0) total += Vector2.Distance(points[i - 1], points[i]);
            cumulative.Add(total);
        }
        Length = total;
    }

    private int Segment(float distance)
    {
        for (int i = 1; i < cumulative.Count; i++)
            if (distance <= cumulative[i]) return i;
        return Mathf.Max(1, cumulative.Count - 1);
    }

    // 경로 시작에서 distance만큼 간 지점
    public Vector2 PositionAt(float distance)
    {
        if (points.Count == 0) return transform.position;
        if (points.Count == 1) return points[0];
        distance = Mathf.Clamp(distance, 0f, Length);
        int i = Segment(distance);
        float len = cumulative[i] - cumulative[i - 1];
        float t = len > 0.0001f ? (distance - cumulative[i - 1]) / len : 0f;
        return Vector2.Lerp(points[i - 1], points[i], t);
    }

    // 그 지점의 진행 방향
    public Vector2 DirectionAt(float distance)
    {
        if (points.Count < 2) return Vector2.right;
        int i = Segment(Mathf.Clamp(distance, 0f, Length));
        return (points[i] - points[i - 1]).normalized;
    }

    // 진행 방향의 왼쪽 수직 방향 (길 폭 안에서 흩어질 때)
    public Vector2 SideAt(float distance)
    {
        Vector2 d = DirectionAt(distance);
        return new Vector2(-d.y, d.x);
    }

    // 길 폭 안 옆줄(lane) 위치: 꺾이는 곳에서 방향을 섞어 위치가 끊기지 않게
    public Vector2 LanePositionAt(float distance, float lane)
    {
        Vector2 center = PositionAt(distance);
        if (points.Count < 2 || Mathf.Abs(lane) < 0.0001f) return center;
        distance = Mathf.Clamp(distance, 0f, Length);
        int i = Segment(distance);
        Vector2 dir = DirectionAt(distance);
        float segStart = cumulative[i - 1], segEnd = cumulative[i];
        if (i > 1)
        {
            float r = Mathf.Min(cornerBlend, (segEnd - segStart) * 0.5f, (segStart - cumulative[i - 2]) * 0.5f);
            if (r > 0.0001f && distance < segStart + r)
            {
                Vector2 prev = (points[i - 1] - points[i - 2]).normalized;
                dir = Vector2.Lerp(prev, dir, 0.5f + 0.5f * (distance - segStart) / r).normalized;
            }
        }
        if (i < points.Count - 1)
        {
            float r = Mathf.Min(cornerBlend, (segEnd - segStart) * 0.5f, (cumulative[i + 1] - segEnd) * 0.5f);
            if (r > 0.0001f && distance > segEnd - r)
            {
                Vector2 next = (points[i + 1] - points[i]).normalized;
                dir = Vector2.Lerp(dir, next, 0.5f * (distance - (segEnd - r)) / r).normalized;
            }
        }
        return center + new Vector2(-dir.y, dir.x) * lane;
    }

    [ContextMenu("경로 길이 로그")]
    private void LogLength()
    {
        Rebuild();
        Debug.Log("[웨이브 경로] 웨이포인트 " + points.Count + "개, 길이 " + Length.ToString("0.0") + "칸");
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Vector3 prev = Vector3.zero;
        int i = 0;
        foreach (Transform child in transform)
        {
            Gizmos.DrawWireSphere(child.position, i == 0 ? 0.5f : 0.25f);
            if (i > 0) Gizmos.DrawLine(prev, child.position);
            prev = child.position;
            i++;
        }
        if (i > 0) Gizmos.DrawWireSphere(prev, gateRadius);         // 끝 = 입구 판정
    }
}
