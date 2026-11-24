using UnityEngine;

// 오브젝트 발밑에 검고 반투명한 타원 그림자를 붙임 (보물상자, 아이루 필살기 꿀벌레)
public static class GroundShadow
{
    // owner: 그림자를 붙일 오브젝트 (자식으로 붙어 함께 움직이고 함께 사라짐)
    // size: 월드 크기 (가로, 세로), offsetY: 스프라이트 아래 끝 기준 위아래 보정
    public static SpriteRenderer Attach(GameObject owner, Vector2 size, float offsetY)
    {
        if (owner == null)
            return null;

        SpriteRenderer body = owner.GetComponent<SpriteRenderer>();
        GameObject go = new GameObject("Ground Shadow");
        go.transform.SetParent(owner.transform, false);

        SpriteRenderer shadow = go.AddComponent<SpriteRenderer>();
        shadow.sprite = CombatUtil.Circle;
        RunConfig rc = RunConfig.Instance;
        shadow.color = rc != null ? rc.groundShadowColor : new Color(0f, 0f, 0f, 0.35f);
        if (body != null)
        {
            shadow.sortingLayerID = body.sortingLayerID;
            shadow.sortingOrder = body.sortingOrder - 1;         // 몸 바로 아래
        }

        // 부모 크기와 상관없이 월드 크기로 맞춤
        Vector3 parentScale = owner.transform.lossyScale;
        float spriteSize = shadow.sprite != null ? shadow.sprite.bounds.size.x : 1f;
        go.transform.localScale = new Vector3(
            size.x / Mathf.Max(0.0001f, spriteSize * Mathf.Abs(parentScale.x)),
            size.y / Mathf.Max(0.0001f, spriteSize * Mathf.Abs(parentScale.y)), 1f);

        float bottom = body != null && body.sprite != null ? body.bounds.min.y : owner.transform.position.y;
        Vector3 world = new Vector3(owner.transform.position.x, bottom + offsetY, owner.transform.position.z);
        go.transform.position = world;
        return shadow;
    }
}
