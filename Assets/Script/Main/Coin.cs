using System.Collections.Generic;
using UnityEngine;

// 상점 강화용 코인 보유량
public static class CoinWallet
{
    public static int Coins { get; private set; }

    public static void Reset()
    {
        Coins = 0;
    }

    public static void Add(int amount)
    {
        Coins = Mathf.Max(0, Coins + amount);
    }

    public static bool TrySpend(int amount)
    {
        if (Coins < amount)
            return false;

        Coins -= amount;
        return true;
    }

    // 몬스터 처치 시 코인 드롭 (밤에 더 많이, 엘리트는 추가)
    public static void OnEnemyKilled(Vector3 position, bool isElite)
    {
        RunConfig config = RunConfig.Instance;

        float chance;
        if (DayNightCycle.IsNight)
        {
            chance = config.nightCoinChance * RunStats.Get(RunStat.NightCoinDrop);
        }
        else
        {
            chance = config.dayCoinChance;
        }
        chance *= RunStats.Get(RunStat.CoinDrop);

        // 확률이 1을 넘으면 그만큼 여러 개
        int count = Mathf.FloorToInt(chance);
        if (Random.value < chance - count)
        {
            count++;
        }

        if (isElite)
        {
            count += config.eliteCoins + Mathf.RoundToInt(RunStats.Get(RunStat.EliteBonusCoins));
        }

        if (count > 0)
        {
            Coin.Drop(position, count);
        }
    }
}

// 필드에 떨어지는 코인. 경험치 구슬과 같은 레이어라 플레이어의 흡수 범위에 끌려온다.
public class Coin : MonoBehaviour
{
    private static readonly Stack<Coin> pool = new Stack<Coin>();
    private static Transform root;

    private SpriteRenderer spriteRenderer;
    private float age;
    private float popTime;
    private Vector3 baseScale;
    private bool picked;

    public static void Drop(Vector3 position, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Coin coin = Spawn();
            Vector2 scatter = count > 1 ? Random.insideUnitCircle * 0.6f : Vector2.zero;
            coin.transform.position = CampZone.FreeSpot(position + (Vector3)scatter, 0.25f);   // 텐트·동굴 등에 끼지 않게
            coin.Begin();
        }
    }

    private static Coin Spawn()
    {
        while (pool.Count > 0)
        {
            Coin pooled = pool.Pop();
            if (pooled != null)
            {
                pooled.gameObject.SetActive(true);
                return pooled;
            }
        }

        if (root == null)
        {
            root = new GameObject("Coins").transform;
        }

        RunConfig config = RunConfig.Instance;
        GameObject go = new GameObject("Coin");
        go.transform.SetParent(root, false);

        // 경험치 구슬과 같은 레이어 (자석 범위에 끌려오도록)
        PoolManager pools = GameManager.instance.pool;
        if (pools.enemyPrefabs.Length > 0 && pools.enemyPrefabs[0] != null)
        {
            go.layer = pools.enemyPrefabs[0].layer;
        }

        SpriteRenderer playerSprite = GameManager.instance.player.GetComponent<SpriteRenderer>();
        Coin coin = go.AddComponent<Coin>();
        coin.spriteRenderer = go.AddComponent<SpriteRenderer>();
        coin.spriteRenderer.sprite = config.coinSprite;
        coin.spriteRenderer.sharedMaterial = playerSprite.sharedMaterial;
        coin.spriteRenderer.sortingLayerID = playerSprite.sortingLayerID;
        coin.spriteRenderer.sortingOrder = 1;

        Rigidbody2D rigid = go.AddComponent<Rigidbody2D>();
        rigid.bodyType = RigidbodyType2D.Kinematic;

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;

        coin.baseScale = Vector3.one * config.coinScale;
        return coin;
    }

    private void Begin()
    {
        age = 0f;
        popTime = 0f;
        picked = false;
        transform.localScale = baseScale;
        spriteRenderer.color = Color.white;
    }

    private void Update()
    {
        if (!GameManager.instance.isLive)
            return;

        float delta = Time.deltaTime;
        age += delta;

        if (picked)
        {
            // 주웠을 때: 살짝 커지며 위로 떠오르고 사라짐
            popTime += delta;
            float p = popTime / 0.2f;
            transform.localScale = baseScale * (1f + p * 0.6f);
            transform.position += Vector3.up * (delta * 3f);
            spriteRenderer.color = new Color(1f, 1f, 1f, 1f - p);
            if (p >= 1f)
            {
                Release();
            }
            return;
        }

        // 떨어진 직후 튀어나오는 효과 + 반짝임
        float spawnPop = Mathf.Clamp01(age / 0.15f);
        float shine = 1f + Mathf.Sin(age * 8f) * 0.06f;
        transform.localScale = baseScale * Mathf.Lerp(1.6f, 1f, spawnPop) * shine;

        // 사라지기 전 깜빡임
        float lifetime = RunConfig.Instance.coinLifetime;
        if (age > lifetime - 5f)
        {
            spriteRenderer.color = new Color(1f, 1f, 1f, Mathf.PingPong(age * 4f, 1f) * 0.7f + 0.3f);
        }
        if (age >= lifetime)
        {
            Release();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (picked || !collision.CompareTag("Player") || !GameManager.instance.isLive)
            return;

        picked = true;
        CoinWallet.Add(1);
        SpriteFx.GoldSpark(transform.position, 0.55f);                    // 금빛 반짝임

        AudioClip clip = RunConfig.Instance.coinSound;
        if (clip != null)
        {
            AudioManager.instance.PlayClip(clip);
        }
        else
        {
            AudioManager.instance.PlaySfx(AudioManager.Sfx.Exe_Get);
        }
    }

    private void Release()
    {
        gameObject.SetActive(false);
        pool.Push(this);
    }
}
