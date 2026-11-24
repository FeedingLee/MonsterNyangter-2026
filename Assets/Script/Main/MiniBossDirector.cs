using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 중간보스 단계 관리 (기존 몬스터 웨이브, 메인 보스 흐름은 그대로 두고 추가)
// - 게임 시간이 stageTimes(2·4·6·8분)에 닿으면 1~4단계 중간보스 무리 등장 (메인 보스 등장 후에는 없음)
// - 등장: 소환 지점에 이펙트 spawnFxTime초 → 등장 (그동안 피해 주고받지 않음) + 화면 위 "우두머리 등장!!" + 초상화
// - 같은 단계 무리를 한 그룹으로 관리, 전원 처치 시 마지막 개체 발밑에 보상 상자
// - 화면 밖 중간보스는 화면 테두리에 해골 원형 아이콘으로 표시 (말벌 제외)
public class MiniBossDirector : MonoBehaviour
{
    private static MiniBossDirector instance;

    // 같은 단계에서 나온 무리
    public class Group
    {
        public MiniBossConfig.Species species;
        public int stage;
        public int total;
        public int dead;
        public readonly List<MiniBoss> members = new List<MiniBoss>();

        // 말벌 원 대형
        public Vector2 waspCenter;
        public float waspRadius;
        public float waspAngle;
        public bool waspChasing;                                 // 원을 다 조인 뒤 각자 추적

        public Vector2 WaspSlot(int index)
        {
            float a = (waspAngle + 360f * index / Mathf.Max(1, total)) * Mathf.Deg2Rad;
            return waspCenter + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * waspRadius;
        }

        public void OnMemberDied(MiniBoss boss)
        {
            dead++;
            if (dead >= total)
            {
                // 아침 선택지 우두머리 현상금: 상자 코인 배율, 무리 전멸 시 레벨업
                float bounty = RunStats.Get(RunStat.MiniBossBounty);
                Vector2Int coins = species.rewardCoins;
                if (bounty > 1f) coins = new Vector2Int(Mathf.RoundToInt(coins.x * bounty), Mathf.RoundToInt(coins.y * bounty));
                RewardBox.Spawn(boss.transform.position, coins);   // 마지막으로 처치된 개체 발밑
                if (bounty > 1f && DayNightCycle.Instance != null) DayNightCycle.Instance.QueueLevelUps(RunConfig.Instance.morningCards.bountyLevels);
                if (instance != null)
                {
                    instance.groups.Remove(this);
                }
            }
        }
    }

    private readonly List<Group> groups = new List<Group>();
    private int nextStage;
    private MiniBossConfig config;

    // UI
    private Canvas canvas;
    private readonly List<Image> indicators = new List<Image>();
    private readonly List<Image> indicatorIcons = new List<Image>();
    private readonly List<Image> indicatorArrows = new List<Image>();
    private Sprite circleSprite;

    // 새 판 시작 시 (GameManager.GameStart)
    public static void Begin()
    {
        MiniBoss.Alive.Clear();
        OffscreenSpawn.ResetRun();
        if (instance != null)
        {
            Destroy(instance.gameObject);
        }
        instance = new GameObject("Mini Boss Director").AddComponent<MiniBossDirector>();
    }

    // 테스트 모드: 단계 강제 시작 (0~3)
    public static void ForceStage(int stage)
    {
        if (instance == null)
        {
            Begin();
        }
        instance.StartStage(stage);
        instance.nextStage = Mathf.Max(instance.nextStage, stage + 1);
    }

    public static int NextStage => instance != null ? instance.nextStage : 0;

    private void Awake()
    {
        config = MiniBossConfig.Instance;
        canvas = RunUI.CreateCanvas("Mini Boss UI", 15, false);   // 스킬·필살기 버튼(0)보다 위, 상점(20)보다 아래. 게임 정지 중(레벨업 등)에는 숨김
        canvas.transform.SetParent(transform, false);
        circleSprite = UiCircleSprite.Create();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
        UiCircleSprite.Release(circleSprite);
    }

    private void Update()
    {
        GameManager gm = GameManager.instance;
        if (gm == null || config == null)
            return;

        // 단계: n일 차 낮에 n단계 (1일 차 낮 시작 + miniBossDelayFirst, 2~4일 차 낮 시작 + miniBossDelay)
        // 귀환 준비 동안은 게임 시간이 멈추므로 낮 시작 기준 게임 시간으로 셈. 메인 보스 등장 이후에는 없음
        DayNightCycle dn = DayNightCycle.Instance;
        if (gm.isLive && !gm.isBossSpawn && dn != null && dn.CurrentPhase == DayNightCycle.Phase.Day && config.species != null)
        {
            if (nextStage < dn.DayCount - 1) nextStage = dn.DayCount - 1;   // 끝없는 밤 등으로 낮을 건너뛰었으면 그날 단계부터
            RunConfig rc = RunConfig.Instance;
            float delay = dn.DayCount <= 1 ? rc.miniBossDelayFirst : rc.miniBossDelay;
            if (nextStage == dn.DayCount - 1 && nextStage < config.species.Length && gm.gameTime - dn.DayStartGameTime >= delay)
            {
                StartStage(nextStage);
                nextStage++;
            }
        }

        if (gm.isLive)
        {
            UpdateWaspGroups();
        }
        UpdateIndicators();
    }

    // ---------------------------------------------------------------
    // 단계 시작
    // ---------------------------------------------------------------
    private void StartStage(int stage)
    {
        if (config.species == null || stage < 0 || stage >= config.species.Length)
            return;
        GameManager gm = GameManager.instance;
        if (gm.isBossSpawn)
            return;

        MiniBossConfig.Species s = config.species[stage];
        Group group = new Group { species = s, stage = stage, total = s.count };
        groups.Add(group);

        // 능력치 기준: 지금 일반 몬스터
        Spawner spawner = gm.player.GetComponentInChildren<Spawner>();
        float hp = 10f, spd = 1.5f;
        if (spawner != null && spawner.spawnData != null && spawner.spawnData.Length > 0)
        {
            int level = spawner.CurrentLevel;                // 그날 일반 몬스터 기준 (일차)
            hp = spawner.spawnData[level].health * (1f + DayNightCycle.MonsterBuff)
                 * RunStats.Get(RunStat.MiniBossHealthToday);    // 아침 선택지 우두머리 현상금: 오늘 새로 나오는 중간보스만
            spd = spawner.spawnData[level].speed;
        }

        List<Vector2> points = SpawnPoints(s, group);
        for (int i = 0; i < points.Count; i++)
        {
            StartCoroutine(SpawnAfterFx(s, group, points[i], hp, spd, i));
        }

        Announce(s);
    }

    // 종별 등장 위치
    private List<Vector2> SpawnPoints(MiniBossConfig.Species s, Group group)
    {
        List<Vector2> list = new List<Vector2>();
        Vector2 playerPos = GameManager.instance.player.transform.position;
        switch (s.kind)
        {
            case MiniBossConfig.Kind.Moss:
            {
                // 화면 밖 한 방향에서 무리로 함께
                float baseAngle = PickAngle(playerPos);
                for (int i = 0; i < s.count; i++)
                {
                    // 같은 쪽에서 오되 화면 가장자리를 따라 조금씩 벌어져 등장
                    float a = (baseAngle + Random.Range(-25f, 25f)) * Mathf.Deg2Rad;
                    Vector2 p = OffScreen(playerPos, new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
                    list.Add(ClampToMap(p + Random.insideUnitCircle * 1.2f));
                }
                break;
            }
            case MiniBossConfig.Kind.Kestodon:
            {
                // 화면 밖 서로 다른 방향에서 1마리씩
                float start = PickAngle(playerPos);
                for (int i = 0; i < s.count; i++)
                {
                    float a = (start + 360f * i / s.count + Random.Range(-30f, 30f)) * Mathf.Deg2Rad;
                    list.Add(ClampToMap(OffScreen(playerPos, new Vector2(Mathf.Cos(a), Mathf.Sin(a)))));
                }
                break;
            }
            case MiniBossConfig.Kind.Wasp:
            {
                // 플레이어 중심 원 위에 같은 간격으로
                group.waspCenter = playerPos;
                group.waspRadius = config.waspStartRadius;
                group.waspAngle = Random.Range(0f, 360f);
                for (int i = 0; i < s.count; i++)
                {
                    list.Add(group.WaspSlot(i));
                }
                break;
            }
            default:
            {
                float a = PickAngle(playerPos) * Mathf.Deg2Rad;
                list.Add(ClampToMap(OffScreen(playerPos, new Vector2(Mathf.Cos(a), Mathf.Sin(a)))));
                break;
            }
        }
        return list;
    }

    // 화면 밖 등장 위치·방향은 OffscreenSpawn 공용 (일반 몬스터와 같은 방식)
    // 화면 밖 거리: 최소 여유 + 무작위 추가 (너무 가깝지도 멀지도 않게)
    private static float Margin => MiniBossConfig.Instance != null
        ? MiniBossConfig.Instance.spawnMargin + Random.Range(0f, MiniBossConfig.Instance.spawnMarginRandom) : 1.5f;

    private static float PickAngle(Vector2 playerPos)
    {
        MiniBossConfig c = MiniBossConfig.Instance;
        return OffscreenSpawn.PickAngle(playerPos, Margin, c != null ? c.spawnAngleGap : 90f);
    }

    private static Vector2 OffScreen(Vector2 playerPos, Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.01f) dir = Vector2.right;
        return OffscreenSpawn.Edge(playerPos, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, Margin);
    }

    private static Vector2 ClampToMap(Vector2 p) => OffscreenSpawn.ClampToMap(p);

    // 소환 이펙트 후 등장 (이펙트 동안에는 아직 몸이 없어 피해를 주고받지 않음)
    private IEnumerator SpawnAfterFx(MiniBossConfig.Species s, Group group, Vector2 point, float hp, float spd, int index)
    {
        // 연기 소용돌이 + 불씨 파티클
        float fxRadius = config.spawnFxRadius * Mathf.Max(0.6f, s.size * config.sizeBoost * 0.25f);
        MiniBossFx.SpawnPortal(point, fxRadius, config.spawnFxTime);
        yield return new WaitForSeconds(config.spawnFxTime);

        if (GameManager.instance == null)
            yield break;

        if (s.kind == MiniBossConfig.Kind.Wasp)
        {
            point = group.WaspSlot(index);
        }
        MiniBoss boss = MiniBoss.Create(s, group, point, hp, spd);
        boss.FormationIndex = index;
        group.members.Add(boss);
        MiniBossFx.AppearBurst(point, fxRadius);
    }

    // 말벌 무리: 등장 때 플레이어 위치를 중심으로 원이 점점 좁혀 옴 (따라가기·회전은 설정값, 기본 0)
    private void UpdateWaspGroups()
    {
        Vector2 playerPos = GameManager.instance.player.transform.position;
        foreach (Group g in groups)
        {
            if (g.species.kind != MiniBossConfig.Kind.Wasp)
                continue;
            g.waspCenter = Vector2.MoveTowards(g.waspCenter, playerPos, config.waspCenterFollowSpeed * Time.deltaTime);   // 0이면 등장 위치 고정
            g.waspRadius = Mathf.Max(config.waspMinRadius, g.waspRadius - config.waspShrinkSpeed * Time.deltaTime);
            if (!g.waspChasing && g.waspRadius <= config.waspChaseRadius)
            {
                g.waspChasing = true;                            // 거의 다 조였으면 원 대형을 풀고 각자 추적 (서로 겹치지 않게)
            }
            g.waspAngle += config.waspSpinSpeed * Time.deltaTime;
        }
    }

    // ---------------------------------------------------------------
    // 등장 문구 + 초상화
    // ---------------------------------------------------------------
    private void Announce(MiniBossConfig.Species s)
    {
        StartCoroutine(AnnounceRoutine(s));
        CameraShake.Play(0.12f, 0.25f);
        AudioManager.instance.PlaySfx(AudioManager.Sfx.Anj_Landing);
    }

    private IEnumerator AnnounceRoutine(MiniBossConfig.Species s)
    {
        Canvas c = RunUI.CreateCanvas("Mini Boss Announce", 35, false);
        RectTransform root = RunUI.Rect("Announce", c.transform, new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(620f, 150f));

        // 뒤로 흩날리는 불씨 파티클
        MiniBossFx.UiEmbers(root, new Vector2(640f, 180f), Mathf.Max(0.5f, config.announceTime));

        Image portrait = RunUI.Icon("Portrait", root, s.portrait, new Vector2(0.5f, 0.5f), new Vector2(-230f, 10f), new Vector2(150f, 150f));
        portrait.useSpriteMesh = true;                           // 직사각형이 아닌 그림 모양대로 → 옆 프레임 조각이 안 보임
        portrait.preserveAspect = true;
        portrait.color = s.tint;
        Text title = RunUI.Label("Title", root, "우두머리 등장!!", 44, new Color(1f, 0.82f, 0.3f), TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(60f, 22f), new Vector2(460f, 60f), 3f);
        Text name = RunUI.Label("Name", root, (s.count > 1 ? s.displayName + " " + s.count + "마리" : s.displayName), 28, Color.white, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(60f, -30f), new Vector2(460f, 40f), 2f);

        float time = 0f;
        float total = Mathf.Max(0.5f, config.announceTime);
        while (time < total)
        {
            time += Time.unscaledDeltaTime;
            float pop = Mathf.Clamp01(time / 0.2f);
            float fadeOut = Mathf.Clamp01((time - (total - 0.4f)) / 0.4f);
            root.localScale = Vector3.one * (pop < 1f ? Mathf.Lerp(1.6f, 1f, pop) : 1f + 0.03f * Mathf.Sin(time * 10f));
            float alpha = 1f - fadeOut;
            title.color = new Color(title.color.r, title.color.g, title.color.b, alpha);
            name.color = new Color(1f, 1f, 1f, alpha);
            if (portrait.enabled) portrait.color = new Color(s.tint.r, s.tint.g, s.tint.b, alpha);
            yield return null;
        }
        Destroy(c.gameObject);
    }

    // ---------------------------------------------------------------
    // 화면 밖 위치 표시 (말벌 제외, 가까운 순 최대 indicatorMax)
    // ---------------------------------------------------------------
    private void UpdateIndicators()
    {
        Camera cam = Camera.main;
        GameManager gm = GameManager.instance;
        List<MiniBoss> targets = new List<MiniBoss>();
        if (cam != null && gm != null && gm.player != null && gm.isLive && Time.timeScale > 0f)   // 레벨업·팝업·일시정지 중에는 숨김
        {
            Vector2 playerPos = gm.player.transform.position;
            foreach (MiniBoss b in MiniBoss.Alive)
            {
                if (b == null || !b.IsLive || b.Data.kind == MiniBossConfig.Kind.Wasp)
                    continue;
                Vector3 vp = cam.WorldToViewportPoint(b.transform.position);
                if (vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f)
                    continue;                                    // 화면 안이면 숨김
                targets.Add(b);
            }
            targets.Sort((p, q) => ((Vector2)p.transform.position - playerPos).sqrMagnitude.CompareTo(((Vector2)q.transform.position - playerPos).sqrMagnitude));
        }

        int count = Mathf.Min(targets.Count, config.indicatorMax);
        while (indicators.Count < count)
        {
            // 나침반 고리 + 안쪽 보스 초상화 + 바깥 방향 화살표
            float sz = config.indicatorBaseSize;
            Image circle = RunUI.Icon("Mini Boss Indicator", canvas.transform, config.indicatorFrame != null ? config.indicatorFrame : circleSprite,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * sz);
            circle.color = config.indicatorFrame != null ? Color.white : config.indicatorColor;
            Image back = RunUI.Icon("Back", circle.transform, circleSprite, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * sz * 0.78f);
            back.color = config.indicatorFrame != null ? new Color(0f, 0f, 0f, 0f) : config.indicatorColor;   // 테두리 그림이 있으면 둥근 바탕은 숨김
            Image face = RunUI.Icon("Face", circle.transform, config.indicatorIcon, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * sz * 0.66f);
            face.useSpriteMesh = true;
            face.preserveAspect = true;
            Image arrow = RunUI.Icon("Arrow", circle.transform, config.indicatorArrow, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * sz * 0.42f);
            arrow.preserveAspect = true;
            arrow.color = new Color(1f, 0.35f, 0.25f, 1f);
            arrow.gameObject.SetActive(config.indicatorArrow != null);
            circle.gameObject.AddComponent<CanvasGroup>().alpha = config.indicatorAlpha;   // 반투명
            indicators.Add(circle);
            indicatorIcons.Add(face);
            indicatorArrows.Add(arrow);
        }

        RectTransform area = (RectTransform)canvas.transform;
        Vector2 half = area.rect.size * 0.5f;
        for (int i = 0; i < indicators.Count; i++)
        {
            bool show = i < count;
            indicators[i].gameObject.SetActive(show);
            if (!show)
                continue;

            MiniBoss b = targets[i];
            Vector2 playerPos = gm.player.transform.position;
            Vector3 vp = cam.WorldToViewportPoint(b.transform.position);
            Vector2 dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;

            // 화면 가운데에서 보스 쪽으로 뻗은 선이 테두리와 만나는 곳
            float size = config.indicatorBaseSize;
            Vector2 inner = half - Vector2.one * (size * 0.8f);
            float tx = Mathf.Abs(dir.x) > 0.0001f ? inner.x / Mathf.Abs(dir.x * area.rect.width) : float.MaxValue;
            float ty = Mathf.Abs(dir.y) > 0.0001f ? inner.y / Mathf.Abs(dir.y * area.rect.height) : float.MaxValue;
            float t = Mathf.Min(tx, ty);
            Vector2 pos = new Vector2(dir.x * area.rect.width, dir.y * area.rect.height) * t;
            indicators[i].rectTransform.anchoredPosition = pos;

            // 가까울수록 크게
            float distance = Vector2.Distance(playerPos, b.transform.position);
            float k = Mathf.InverseLerp(config.indicatorDistNear, config.indicatorDistFar, distance);
            float scale = Mathf.Lerp(config.indicatorScaleNear, config.indicatorScaleFar, k);
            indicators[i].rectTransform.localScale = Vector3.one * scale;
            // 안쪽 그림 = 그 보스 초상화, 화살표 = 보스 쪽으로 (고리 바깥에서 살짝 들썩임)
            Sprite face = b.Data.portrait != null ? b.Data.portrait : config.indicatorIcon;
            indicatorIcons[i].sprite = face;
            indicatorIcons[i].color = b.Data.portrait != null ? b.Data.tint : Color.white;
            if (b.Data.portrait != null && b.Data.facesRight == (dir.x < 0f))
                indicatorIcons[i].rectTransform.localScale = new Vector3(-1f, 1f, 1f);   // 보스 쪽을 바라보게
            else
                indicatorIcons[i].rectTransform.localScale = Vector3.one;
            Vector2 nd = new Vector2(dir.x * area.rect.width, dir.y * area.rect.height).normalized;
            float bob = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 8f);
            indicatorArrows[i].rectTransform.anchoredPosition = nd * size * 0.62f * bob;
            indicatorArrows[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(nd.y, nd.x) * Mathf.Rad2Deg - 90f);
        }
    }
}
