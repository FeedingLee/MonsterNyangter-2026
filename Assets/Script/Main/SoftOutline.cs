using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 8방향 글자 외곽선
// Unity 기본 Outline은 대각선 4방향으로만 복사해서, 두껍게 주면 글자 모서리가 뾰족뾰족(톱니)해 보인다.
// 위·아래·좌·우 + 대각선 8방향으로 한 번에 복사해 둥글고 고른 테두리를 만든다 (Outline을 여러 개 겹치는 것보다 정점도 적음).
[AddComponentMenu("UI/Effects/Soft Outline (8방향)")]
public class SoftOutline : BaseMeshEffect
{
    public Color color = Color.black;
    [Min(0f)] public float distance = 2f;
    public bool useGraphicAlpha = true;                      // 글자가 서서히 사라질 때 테두리도 함께

    private static readonly List<UIVertex> source = new List<UIVertex>();
    private static readonly List<UIVertex> result = new List<UIVertex>();

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0 || distance <= 0f)
            return;
        source.Clear();
        vh.GetUIVertexStream(source);
        int count = source.Count;
        result.Clear();
        if (result.Capacity < count * 9) result.Capacity = count * 9;

        float d = distance, k = distance * 0.7071f;
        for (int dir = 0; dir < 8; dir++)
        {
            Vector3 offset;
            switch (dir)
            {
                case 0: offset = new Vector3(d, 0f); break;
                case 1: offset = new Vector3(-d, 0f); break;
                case 2: offset = new Vector3(0f, d); break;
                case 3: offset = new Vector3(0f, -d); break;
                case 4: offset = new Vector3(k, k); break;
                case 5: offset = new Vector3(k, -k); break;
                case 6: offset = new Vector3(-k, k); break;
                default: offset = new Vector3(-k, -k); break;
            }
            for (int i = 0; i < count; i++)
            {
                UIVertex v = source[i];
                v.position += offset;
                Color32 c = color;
                if (useGraphicAlpha) c.a = (byte)(c.a * source[i].color.a / 255);
                v.color = c;
                result.Add(v);
            }
        }
        result.AddRange(source);                             // 글자 본체는 맨 위
        vh.Clear();
        vh.AddUIVertexTriangleStream(result);
    }
}
