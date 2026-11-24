using System.Collections.Generic;
using UnityEngine;

// 범위 공격, 범위 마비, 번쩍임 효과 등 여러 스크립트가 함께 쓰는 전투 도우미
public static class CombatUtil
{
    private static Sprite circleSprite;

    // 코드로 만든 흰 원 스프라이트 (범위 표시, 번쩍임에 사용)
    public static Sprite Circle
    {
        get
        {
            if (circleSprite == null)
            {
                circleSprite = UiCircleSprite.Create();
            }
            return circleSprite;
        }
    }

    // 몬스터 프리팹의 정렬 레이어 (바닥 범위 표시를 몬스터 아래에 그릴 때 사용)
    public static int EnemySortingLayer
    {
        get
        {
            GameManager gm = GameManager.instance;
            foreach (GameObject prefab in gm.pool.prefabs)
            {
                if (prefab != null && prefab.GetComponent<Enemy>() != null && prefab.TryGetComponent(out SpriteRenderer renderer))
                    return renderer.sortingLayerID;
            }
            return gm.player.GetComponent<SpriteRenderer>().sortingLayerID;
        }
    }

    // 원 안의 몬스터와 보스에게 바로 피해
    // scaleWithBuff: 공격력 버프 적용 여부, ignoreBossCap: 보스 1회 피해 상한 무시 (폭탄 통)
    // knockChance: 넉백 확률 (0이면 넉백 없음)
    public static void DamageInCircle(Vector3 center, float radius, float damage, bool scaleWithBuff = true, bool ignoreBossCap = false, float knockChance = 0f)
    {
        float hitDamage = scaleWithBuff ? damage * PlayerBuff.Damage : damage;
        HashSet<Object> done = new HashSet<Object>();
        object strike = new object();                       // 이번 범위 공격 1회 (생명의 일격: 맞은 적 모두에게)

        foreach (Collider2D col in Physics2D.OverlapCircleAll(center, radius))
        {
            Enemy enemy = col.GetComponentInParent<Enemy>();
            if (enemy != null)
            {
                if (done.Add(enemy))
                {
                    // 플레이어 공격(공격력 배율 적용)만 전투 기믹 적용. 무기 스킬 등 범위 공격은 메인무기 공격으로 봄
                    float d = scaleWithBuff ? GimmickCards.ModifyHit(hitDamage, true, enemy.HuntKind, strike) : hitDamage;
                    bool wasAlive = enemy.IsAlive;
                    enemy.TakeDamage(d, knockChance);
                    if (scaleWithBuff && wasAlive) GimmickCards.AfterHit(enemy.transform.position, d, !enemy.IsAlive);
                }
                continue;
            }

            ColonyNest nest = col.GetComponentInParent<ColonyNest>();
            if (nest != null)
            {
                if (done.Add(nest) && nest.IsLive)
                {
                    float d = scaleWithBuff ? GimmickCards.ModifyHit(hitDamage, true, GimmickCards.TargetKind.Mob, strike) : hitDamage;   // 군락 둥지 (사냥 본능 없음)
                    nest.TakeDamage(d);
                    if (scaleWithBuff) GimmickCards.AfterHit(nest.transform.position, d, false);   // 처치 폭발 없음
                }
                continue;
            }

            MiniBoss miniBoss = col.GetComponentInParent<MiniBoss>();
            if (miniBoss != null)
            {
                if (done.Add(miniBoss))
                {
                    float d = scaleWithBuff ? GimmickCards.ModifyHit(hitDamage, true, GimmickCards.TargetKind.MiniBoss, strike) : hitDamage;
                    bool wasAlive = miniBoss.IsLive;
                    miniBoss.TakeDamage(d, knockChance);
                    if (scaleWithBuff && wasAlive) GimmickCards.AfterHit(miniBoss.transform.position, d, !miniBoss.IsLive);
                }
                continue;
            }

            BossPattern boss = col.GetComponentInParent<BossPattern>();
            if (boss != null && done.Add(boss))
            {
                float d = scaleWithBuff ? GimmickCards.ModifyHit(hitDamage, true, GimmickCards.TargetKind.Boss, strike) : hitDamage;
                boss.TakeDamage(d, ignoreBossCap);
                if (scaleWithBuff) GimmickCards.AfterHit(boss.transform.position, d, false);
            }
        }
    }

    // 원 안 광역 피해: 일반 몬스터와 보스·중간보스 피해 수치를 따로 (정밀 저격, 강철의 영역)
    // 공통 규칙: 공격력·기믹 적용, 연쇄 번개 적중 수·처치 폭발 포함, 보스·중간보스 1회 상한 적용
    public static void DamageInCircleSplit(Vector3 center, float radius, float mobDamage, float bossDamage, float knockChance = 0f)
    {
        HashSet<Object> done = new HashSet<Object>();
        object strike = new object();
        float buff = PlayerBuff.Damage;
        foreach (Collider2D col in Physics2D.OverlapCircleAll(center, radius))
        {
            Enemy enemy = col.GetComponentInParent<Enemy>();
            if (enemy != null)
            {
                if (done.Add(enemy))
                {
                    float d = GimmickCards.ModifyHit(mobDamage * buff, true, enemy.HuntKind, strike);
                    bool wasAlive = enemy.IsAlive;
                    enemy.TakeDamage(d, knockChance);
                    if (wasAlive) GimmickCards.AfterHit(enemy.transform.position, d, !enemy.IsAlive);
                }
                continue;
            }
            ColonyNest nest = col.GetComponentInParent<ColonyNest>();
            if (nest != null)
            {
                if (done.Add(nest) && nest.IsLive)
                {
                    float d = GimmickCards.ModifyHit(mobDamage * buff, true, GimmickCards.TargetKind.Mob, strike);   // 군락 둥지: 일반 몬스터 피해
                    nest.TakeDamage(d);
                    GimmickCards.AfterHit(nest.transform.position, d, false);
                }
                continue;
            }
            MiniBoss miniBoss = col.GetComponentInParent<MiniBoss>();
            if (miniBoss != null)
            {
                if (done.Add(miniBoss))
                {
                    float d = Mathf.Min(GimmickCards.ModifyHit(bossDamage * buff, true, GimmickCards.TargetKind.MiniBoss, strike), SynergyAwakening.BossHitCap);
                    bool wasAlive = miniBoss.IsLive;
                    miniBoss.TakeDamage(d, knockChance);
                    if (wasAlive) GimmickCards.AfterHit(miniBoss.transform.position, d, !miniBoss.IsLive);
                }
                continue;
            }
            BossPattern bossPattern = col.GetComponentInParent<BossPattern>();
            if (bossPattern != null && done.Add(bossPattern))
            {
                float d = GimmickCards.ModifyHit(bossDamage * buff, true, GimmickCards.TargetKind.Boss, strike);
                SynergyAwakening.DamageBoss(bossPattern, d);
                GimmickCards.AfterHit(bossPattern.transform.position, d, false);
            }
        }
    }

    // 원 안의 적 이동 불가 (보스는 이동만 멈추고 원거리 패턴은 계속)
    public static void StunInCircle(Vector3 center, float radius, float mobSeconds, float bossSeconds)
    {
        HashSet<Object> done = new HashSet<Object>();

        foreach (Collider2D col in Physics2D.OverlapCircleAll(center, radius))
        {
            Enemy enemy = col.GetComponentInParent<Enemy>();
            if (enemy != null)
            {
                if (done.Add(enemy))
                {
                    enemy.Stun(mobSeconds);
                    SpriteFx.StunSpark(enemy.transform, mobSeconds);      // 머리 위 파란 반짝임
                }
                continue;
            }

            MiniBoss miniBoss = col.GetComponentInParent<MiniBoss>();
            if (miniBoss != null)
            {
                if (done.Add(miniBoss))
                {
                    miniBoss.Stun(bossSeconds);
                    SpriteFx.StunSpark(miniBoss.transform, bossSeconds);
                }
                continue;
            }

            BossPattern boss = col.GetComponentInParent<BossPattern>();
            if (boss != null && done.Add(boss))
            {
                boss.Stun(bossSeconds);
                SpriteFx.StunSpark(boss.transform, bossSeconds);
            }
        }
    }

    // 원형으로 퍼지며 사라지는 번쩍임 효과
    public static void Flash(Vector3 position, float diameter, Color color, float duration)
    {
        GameManager gm = GameManager.instance;
        SpriteRenderer playerSprite = gm != null ? gm.player.GetComponent<SpriteRenderer>() : null;

        GameObject go = new GameObject("Flash");
        go.transform.position = position;
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = Circle;
        if (playerSprite != null)
        {
            renderer.sharedMaterial = playerSprite.sharedMaterial;
            renderer.sortingLayerID = playerSprite.sortingLayerID;
            renderer.sortingOrder = playerSprite.sortingOrder + 4;
        }

        go.AddComponent<FlashEffect>().Init(renderer, diameter, color, duration);
    }
}
