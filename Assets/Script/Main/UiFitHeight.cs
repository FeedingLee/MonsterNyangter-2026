using UnityEngine;

// 부모(캔버스) 높이가 needHeight보다 낮으면 이 묶음을 그만큼 축소 (가로 화면에서 위아래가 잘리지 않게)
public class UiFitHeight : MonoBehaviour
{
    public float needHeight = 860f;
    [Range(0.1f, 1f)] public float minScale = 0.5f;

    private void LateUpdate()
    {
        RectTransform parent = transform.parent as RectTransform;
        if (parent == null || needHeight <= 0f)
            return;
        float s = Mathf.Clamp(parent.rect.height / needHeight, minScale, 1f);
        if (!Mathf.Approximately(transform.localScale.x, s))
            transform.localScale = new Vector3(s, s, 1f);
    }
}
