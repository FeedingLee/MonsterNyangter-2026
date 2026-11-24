using System.Collections.Generic;
using UnityEngine;

// 낮 잔존 몬스터 은신 보관 (코어루프 개선기획서 v2.0 2.3, 2.4)
// - 귀환 준비 시작(끝없는 밤처럼 준비 없이 밤이 되면 밤 시작) 때 필드의 낮 일반 몬스터·중간보스가 땅속으로 사라짐
// - 다음 날 낮(아침)에 카메라 밖에서 저장한 상태 그대로 다시 나와 플레이어를 추격
// - 보스전이 시작되면 보관 목록을 비움 (다시 나오지 않음)
public static class DayMonsterStash
{
    private static readonly List<Enemy.DayState> monsters = new List<Enemy.DayState>();
    private static readonly List<MiniBoss> bosses = new List<MiniBoss>();

    public static int StoredCount => monsters.Count + bosses.Count;

    // 필드의 낮 몬스터를 숨기고 보관 (밤 웨이브·군락 호위·이미 숨는 중인 몬스터 제외)
    public static void HideAll()
    {
        RunConfig c = RunConfig.Instance;
        foreach (Enemy e in Object.FindObjectsOfType<Enemy>())
        {
            if (!e.IsAlive || e.NightWave || e.ColonyGuard || e.Hiding)
                continue;
            monsters.Add(e.CaptureDayState());
            e.HideForNight(c.hideFxTime, c.hideSinkDistance);
        }
        foreach (MiniBoss b in new List<MiniBoss>(MiniBoss.Alive))
        {
            if (b == null || !b.IsLive)
                continue;
            if (!bosses.Contains(b)) bosses.Add(b);
            b.HideForNight(c.hideFxTime, c.hideSinkDistance);
        }
    }

    // 다음 날 낮: 카메라 밖에서 같은 상태로 다시 소환
    public static void RestoreAll()
    {
        GameManager gm = GameManager.instance;
        if (gm == null || gm.player == null)
            return;
        Spawner spawner = gm.player.GetComponentInChildren<Spawner>(true);
        Vector2 margin = spawner != null ? spawner.offscreenMargin : new Vector2(2.25f, 7.5f);
        Vector3 playerPos = gm.player.transform.position;

        foreach (Enemy.DayState s in monsters)
        {
            Enemy e = gm.pool.Get(0).GetComponent<Enemy>();
            e.transform.position = OffscreenSpawn.RandomPoint(playerPos, Random.Range(margin.x, Mathf.Max(margin.x, margin.y)));
            SpawnData data = new SpawnData { spriteType = s.spriteType, health = Mathf.Max(1, Mathf.RoundToInt(s.maxHealth)), speed = s.speed, expIndex = s.expIndex, stronger = 1f };
            e.Init(data, s.expIndex);                          // 엘리트 오라 등 기본 외형
            e.RestoreDayState(s);                              // 체력·처벌 외형·몸 색 그대로
        }
        monsters.Clear();

        foreach (MiniBoss b in bosses)
        {
            if (b == null) continue;
            b.Reappear(OffscreenSpawn.RandomPoint(playerPos, Random.Range(margin.x, Mathf.Max(margin.x, margin.y)) + 2f));
        }
        bosses.Clear();
    }

    // 보스전 시작·새 판: 보관 목록 비움 (숨어 있던 중간보스 오브젝트는 정리)
    public static void Clear()
    {
        monsters.Clear();
        foreach (MiniBoss b in bosses)
        {
            if (b != null) Object.Destroy(b.gameObject);
        }
        bosses.Clear();
    }
}
