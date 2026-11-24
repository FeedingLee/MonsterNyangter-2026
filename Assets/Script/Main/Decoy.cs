using UnityEngine;

// 보와보와 필살기로 소환되는 미끼. 지속시간 동안 몬스터의 목표가 되며 피해를 받지 않는다.
public class Decoy : MonoBehaviour
{
    private static Decoy current;

    private float endTime;
    private float bossEndTime;
    private GameObject aura;

    // 잡몹이 향할 위치 (미끼가 있으면 미끼, 없으면 플레이어)
    public static Vector2 GetEnemyTarget(Vector2 playerPosition)
    {
        return current != null ? (Vector2)current.transform.position : playerPosition;
    }

    // 보스가 향할 위치 (미끼의 보스 적용 시간 안에서만 미끼)
    public static Vector2 GetBossTarget(Vector2 playerPosition)
    {
        if (current != null && Time.time < current.bossEndTime)
            return current.transform.position;
        return HeavyStealth.Active ? HeavyStealth.LastSeen : playerPosition;   // 헤비보우건 은신: 마지막으로 본 위치
    }

    public void Init(float duration, float bossRatio)
    {
        // 쿨타임이 지속시간보다 짧아 이전 미끼가 남아 있으면 새 미끼로 교체
        if (current != null)
        {
            Destroy(current.gameObject);
        }

        endTime = Time.time + duration;
        if (aura == null) aura = SpriteFx.DecoyAura(transform);     // 지속시간 동안 주위에 나뭇잎·반짝임
        bossEndTime = Time.time + duration * bossRatio;
        current = this;
    }

    private void Update()
    {
        if (Time.time >= endTime)
        {
            Destroy(gameObject);
        }
    }

    private void OnDisable()
    {
        if (current == this)
        {
            current = null;
        }
    }
}
