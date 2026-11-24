using System.Collections.Generic;
using UnityEngine;

// 아이루 필살기로 설치되는 꿀벌레. 플레이어가 닿으면 체력을 회복하고 사라진다.
public class HoneyBug : MonoBehaviour
{
    private static readonly List<HoneyBug> activeBugs = new List<HoneyBug>();

    private float healPercent;
    private AudioClip healSound;
    private GameObject pickupEffect;
    private float effectLifetime;
    private bool isUsed;

    // 지정한 위치의 spacing 거리 안에 이미 꿀벌레가 있는지 확인
    public static bool IsNear(Vector2 position, float spacing)
    {
        foreach (HoneyBug bug in activeBugs)
        {
            if (Vector2.Distance(bug.transform.position, position) < spacing)
            {
                return true;
            }
        }
        return false;
    }

    public void Init(float healPercent, AudioClip healSound, GameObject pickupEffect, float effectLifetime)
    {
        this.healPercent = healPercent;
        this.healSound = healSound;
        this.pickupEffect = pickupEffect;
        this.effectLifetime = effectLifetime;
    }

    private void OnEnable()
    {
        activeBugs.Add(this);
    }

    private void OnDisable()
    {
        activeBugs.Remove(this);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isUsed || !collision.CompareTag("Player") || !GameManager.instance.isLive)
            return;

        isUsed = true;

        GameManager manager = GameManager.instance;
        manager.Heal(manager.maxHealth * healPercent * 0.01f);   // 회복 배율 적용 (단식 수행이면 회복 없음)
        SpriteFx.Heal(manager.player.transform);                  // 초록 십자 → 하트

        AudioManager.instance.PlayClip(healSound);

        if (pickupEffect != null)
        {
            Destroy(Instantiate(pickupEffect, transform.position, Quaternion.identity), effectLifetime);
        }

        Destroy(gameObject);
    }
}
