using UnityEngine;

// 조준 레이저 설정 (GameManager 인스펙터에서 조정)
[System.Serializable]
public class AimLaserSettings
{
    public Color color = new Color(1f, 0f, 0f, 0.8f);   // 레이저 색상
    [Min(0.001f)] public float width = 0.05f;           // 레이저 굵기
    [Min(0f)] public float length = 25f;                // 레이저 길이
    public int sortingOrder = 15;                       // 플레이어와 같은 정렬 레이어 안에서의 순서
}

// 메인무기 헤비보우건의 마우스 조준 방향을 레이저로 표시
public class AimLaser : MonoBehaviour
{
    private LineRenderer line;
    private SpriteRenderer playerSprite;

    private void Awake()
    {
        playerSprite = GameManager.instance.player.GetComponent<SpriteRenderer>();

        line = gameObject.AddComponent<LineRenderer>();
        line.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        line.positionCount = 2;
        line.useWorldSpace = true;
    }

    private void LateUpdate()
    {
        MainWeaponController main = MainWeaponController.Current;
        Vector3 direction = main != null ? main.AimDirection : Vector3.zero;

        line.enabled = GameManager.instance.isLive && direction != Vector3.zero;
        if (!line.enabled)
            return;

        ApplySettings();

        Vector3 origin = transform.position;
        line.SetPosition(0, origin);
        line.SetPosition(1, origin + direction.normalized * GameManager.instance.aimLaser.length);
    }

    // 플레이 중 인스펙터에서 바꾼 값도 바로 반영되도록 매 프레임 적용
    private void ApplySettings()
    {
        AimLaserSettings settings = GameManager.instance.aimLaser;

        line.startColor = settings.color;
        line.endColor = settings.color;
        line.startWidth = settings.width;
        line.endWidth = settings.width;
        line.sortingLayerID = playerSprite.sortingLayerID;
        line.sortingOrder = settings.sortingOrder;
    }

    private void OnDestroy()
    {
        Destroy(line.sharedMaterial);
    }
}
