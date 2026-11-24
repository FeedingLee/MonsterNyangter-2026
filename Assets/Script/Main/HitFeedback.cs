using UnityEngine;

// 타격 등급 연출 (히트스톱 없음)
// 강타: 화면 약흔들림 + 큰 타격 불꽃 / 약타: 작은 타격 불꽃
public static class HitFeedback
{
    public static void Strong(Vector3 position, float size)
    {
        CameraShake.Weak();
        SpriteFx.StarBurst(position, size);
    }

    public static void Weak(Vector3 position, float size)
    {
        SpriteFx.HitSpark(position, Mathf.Max(0.5f, size * 0.5f));
    }
}
