using UnityEngine;

// 중간보스 무리 보상 상자 (Reward_box 시트 애니메이션)
// 반짝이며 기다리다가 플레이어가 닿으면 열리고, 능력치 성장용 코인을 주변에 흩뿌린 뒤 사라짐
public class RewardBox : MonoBehaviour
{
    private SpriteRenderer sr;
    private MiniBossConfig config;
    private Vector2Int coinRange;
    private bool opened;
    private float time;
    private float openTime;
    private bool coinsDropped;

    public static void Spawn(Vector3 position, Vector2Int coins)
    {
        MiniBossConfig c = MiniBossConfig.Instance;
        GameObject go = new GameObject("Reward Box");
        position = CampZone.FreeSpot(position, 0.8f);           // 텐트·동굴 등에 끼지 않게
        go.transform.position = position;
        RewardBox box = go.AddComponent<RewardBox>();
        box.config = c;
        box.coinRange = coins;
        box.sr = go.AddComponent<SpriteRenderer>();
        box.sr.sortingLayerName = "Player";                     // 플레이어보다 뒤
        box.sr.sortingOrder = -10;
        Sprite first = c.boxIdle != null && c.boxIdle.Length > 0 ? c.boxIdle[0] : null;
        box.sr.sprite = first;
        if (first != null)
        {
            go.transform.localScale = Vector3.one * (c.boxSize / Mathf.Max(0.01f, first.bounds.size.x));
        }
        RunConfig rc = RunConfig.Instance;
        GroundShadow.Attach(go, rc.rewardBoxShadowSize, rc.rewardBoxShadowOffsetY);   // 발밑 그림자
        SpriteFx.StarBurst(position + Vector3.up * 0.5f, 2.2f);
        SpriteFx.GoldSpark(position + Vector3.up * 0.6f, 1.2f);
    }

    private void Update()
    {
        time += Time.deltaTime;
        GameManager gm = GameManager.instance;
        if (gm == null)
            return;

        if (!opened)
        {
            Loop(config.boxIdle, 6f);
            // 플레이어가 닿으면 열림
            if (gm.isLive && Vector2.Distance(gm.player.transform.position, transform.position + Vector3.up * 0.4f) < 1.1f)
            {
                opened = true;
                openTime = time;
                AudioManager.instance.PlaySfx(AudioManager.Sfx.Player_LevelUp);
            }
            return;
        }

        float t = time - openTime;
        int openFrames = config.boxOpen != null ? config.boxOpen.Length : 0;
        float openDuration = openFrames / 14f;
        if (t < openDuration && openFrames > 0)
        {
            sr.sprite = config.boxOpen[Mathf.Min(openFrames - 1, Mathf.FloorToInt(t * 14f))];
            return;
        }

        if (!coinsDropped)
        {
            coinsDropped = true;
            DropCoins();
        }

        // 터지는 프레임 → 마지막 프레임 유지하며 사라짐
        int burstFrames = config.boxBurst != null ? config.boxBurst.Length : 0;
        float bt = t - openDuration;
        if (burstFrames > 0)
        {
            sr.sprite = config.boxBurst[Mathf.Min(burstFrames - 1, Mathf.FloorToInt(bt * 12f))];
        }
        float fade = Mathf.Clamp01((bt - 1.2f) / 0.6f);
        sr.color = new Color(1f, 1f, 1f, 1f - fade);
        if (fade >= 1f)
        {
            Destroy(gameObject);
        }
    }

    private void Loop(Sprite[] frames, float fps)
    {
        if (frames == null || frames.Length == 0)
            return;
        sr.sprite = frames[Mathf.FloorToInt(time * fps) % frames.Length];
    }

    // 코인을 상자 주변에 흩뿌림 (기존 코인처럼 직접 줍거나 흡수 범위에 끌려옴)
    private void DropCoins()
    {
        int count = Random.Range(coinRange.x, Mathf.Max(coinRange.x, coinRange.y) + 1);
        for (int i = 0; i < count; i++)
        {
            Vector2 offset = Random.insideUnitCircle * config.coinScatterRadius;
            Coin.Drop(transform.position + Vector3.up * 0.3f + (Vector3)offset, 1);
        }
        SpriteFx.StarBurst(transform.position + Vector3.up * 0.6f, 2.6f);
        CameraShake.Play(0.08f, 0.15f);
    }
}
