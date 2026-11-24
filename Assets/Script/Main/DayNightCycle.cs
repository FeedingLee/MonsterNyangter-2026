using UnityEngine;
using UnityEngine.UI;

// 낮/밤 진행 관리 (GameManager가 실행 중에 붙임)
// 낮: 평범한 사냥 + 맵 중앙 캠프에서 상점 이용
// 밤: 몬스터 강화(보스 제외) + 코인 추가 드롭, 화면이 어두워지고 캐릭터 주변만 보임
// 아침: 밤이 끝나면 성장/도전/지옥 선택지 중 하나를 고름
// 보스가 등장하면 낮/밤이 멈추고 남은 도전 리스크가 풀린다.
public class DayNightCycle : MonoBehaviour
{
    public enum Phase { Day, Night, ReturnPrep }   // ReturnPrep: 낮 → 밤 사이 귀환 준비 (전체 타이머·낮 스폰 정지)

    // 포탑 발판 등 연결 지점 (다음 작업): 귀환 준비 시작 / 밤 끝(아침)
    public static event System.Action ReturnPrepStarted;
    public static event System.Action NightEnded;
    public static event System.Action BossStarted;           // 포탑·발판 철거

    public bool InReturnPrep => phase == Phase.ReturnPrep && !bossStarted;
    // 전체 타이머(GameManager.gameTime) 정지 여부
    public static bool TimerPaused => Instance != null && Instance.InReturnPrep;
    // 기존 낮 몬스터 스폰 정지 (귀환 준비 ~ 밤)
    public static bool DaySpawnPaused => Instance != null && Instance.started && !Instance.bossStarted && Instance.phase != Phase.Day;
    // 이번 낮이 시작된 게임 시각 (중간보스 등장 기준)
    public float DayStartGameTime { get; private set; }
    // 마지막 날(이 날 낮이 끝나면 보스) = (총 시간 - 마지막 낮) / (낮 + 밤) + 1
    public int LastDay => Mathf.FloorToInt((config.totalTime - config.lastDayTime) / Mathf.Max(1f, config.dayDuration + config.nightDuration) + 0.001f) + 1;
    private float DayLength => dayCount >= LastDay ? config.lastDayTime : config.dayDuration;
    public float PhaseRemain => Mathf.Max(0f, CurrentPhaseLength - phaseTime);
    private float CurrentPhaseLength => phase == Phase.Day ? DayLength : phase == Phase.ReturnPrep ? config.returnPrepTime : config.nightDuration;

    public static DayNightCycle Instance { get; private set; }

    // 지금 밤인지 (보스전 중에는 항상 낮 취급)
    public static bool IsNight => Instance != null && Instance.phase == Phase.Night && !Instance.bossStarted;

    // 지금 스폰되는 몬스터에 적용할 밤 강화량 (0.3 = +30%)
    public static float MonsterBuff
    {
        get
        {
            if (!IsNight)
                return 0f;

            // 1120: 야간 강화 제거 (밤 웨이브는 전용 체력 표 nightHp 사용, 색 변화 없음)
            return 0f;
        }
    }

    // 상점을 열 수 있는지 (낮, 보스전 전)
    public static bool ShopAvailable => Instance != null && Instance.started && Instance.phase == Phase.Day && !Instance.bossStarted;

    public Phase CurrentPhase => phase;

    // 시계, 코인, 능력치 창을 보여 줄지 (캐릭터 선택 후 게임 중)
    public bool HudVisible { get; private set; }
    public int DayCount => dayCount;
    public int NightsSurvived => nightsSurvived;       // 밤을 버티고 아침을 맞은 횟수 (보스 등장 뒤에는 늘지 않음)
    public Vector3 MapCenter => mapCenter;
    public bool QuestActive => questActive;

    private RunConfig config;
    private GameManager gm;
    private Phase phase = Phase.Day;
    private float phaseTime;
    private int dayCount = 1;
    private int nightIndex;
    private int nightsSurvived;
    private bool started;
    private bool bossStarted;
    private Vector3 mapCenter;
    private float baseMaxHealth;
    private int pendingLevels;
    private int nextMorningPicks = 1;
    private float darkness;

    // 낮 하늘 연출용 (DaySky): 1 = 한낮 밝기, 0 = 밤 / 노을 진행 0~1
    public static float Daylight { get; private set; } = 1f;
    public static float SunsetProgress { get; private set; }
    public static float DayProgress { get; private set; } = 0.5f;          // 낮 진행 0~1 (0.5 = 정오)
    private const float BossNightDarkness = 0.6f;   // 보스전 밤 어둠 정도 (평소 밤 = 1)

    private bool questActive;
    private int questStartKills;

    // UI
    private RectTransform hudCanvasRect;
    private GameObject hudRoot;
    private RectTransform overlayCanvasRect;
    private RawImage nightOverlay;
    private Texture2D vignetteTexture;
    private Image clockImage;
    private RectTransform hourHand;
    private Image dayZone;                                   // 시계판 낮 구간 (부채꼴)
    private RectTransform minuteHand;
    private Text clockLabel;
    private Text coinLabel;
    private Text questLabel;
    private Text banner;
    private Canvas bannerCanvas;                             // 알림 문구 전용 하위 캔버스 (정렬 최상단)
    private float bannerTime = -10f;
    private AudioLowPassFilter bgmFilter;
    private float builtInnerDarkness;         // 지금 비네트 텍스처의 시야 안 어둠 정도

    private const float VignetteHole = 0.25f;     // 비네트 텍스처에서 구멍 반경 (텍스처 크기 대비)

    private void Awake()
    {
        Instance = this;
        config = RunConfig.Instance;
        gm = GetComponent<GameManager>();

        RunStats.Reset();
        CoinWallet.Reset();
        MorningChoice.ResetRun();

        BuildOverlay();
        DaySky.Create();
        BuildHud();

        // 화면 왼쪽 능력치 현황 + 보관 슬롯
        gameObject.AddComponent<StatPanel>();
        gameObject.AddComponent<NightDefense>();             // 밤 웨이브·기지 체력·귀환 준비 표시
        gameObject.AddComponent<TurretSystem>();             // 포탑 발판·건설
        gameObject.AddComponent<ColonySystem>();             // 낮 외곽 군락 (2~4일 차)
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        if (vignetteTexture != null)
        {
            Destroy(vignetteTexture);
        }

        // 배경음 관리자는 씬이 바뀌어도 남으므로 밤 필터를 꺼 둠
        if (bgmFilter != null)
        {
            bgmFilter.enabled = false;
        }
    }

    // 아침 선택지, 토벌 의뢰 등에서 레벨업을 여러 번 줄 때 순서대로 레벨업 창을 띄움
    public void QueueLevelUps(int count)
    {
        pendingLevels += Mathf.Max(0, count);
    }

    // 다음 아침에 고를 선택지 장수 (끝없는 밤)
    public void SetNextMorningPicks(int picks)
    {
        nextMorningPicks = Mathf.Max(nextMorningPicks, picks);
    }

    // 끝없는 밤: 다가오는 낮을 건너뛰고 바로 밤 시작
    public void StartEndlessNight()
    {
        BeginNight(true);
    }

    // 테스트용: 지금 낮/밤을 바로 끝냄
    public void SkipPhase()
    {
        phaseTime = CurrentPhaseLength;
    }

    // 테스트용: 지금 바로 귀환 준비부터 시작 (→ 밤)
    public void StartReturnPrepNow()
    {
        if (!started || bossStarted) return;
        BeginReturnPrep();
    }

    // 토벌 의뢰 시작 (다음 아침까지 목표 처치 수 달성)
    public void StartKillQuest()
    {
        questActive = true;
        questStartKills = gm.kill;
    }

    private void Update()
    {
        if (!started)
        {
            if (gm.player.gameObject.activeInHierarchy && gm.isLive)
            {
                StartRun();
            }
            else
            {
                UpdateVisuals();
                return;
            }
        }

        ApplyMaxHealth();

        if (!bossStarted && gm.isBossSpawn)
        {
            OnBossAppeared();
        }

        if (gm.isLive)
        {
            Regenerate();

            if (!bossStarted)
            {
                phaseTime += Time.deltaTime;
                if (phase == Phase.Day && phaseTime >= DayLength && dayCount < LastDay)
                {
                    BeginReturnPrep();                       // 낮 → 귀환 준비 → 밤 (마지막 날 낮이 끝나면 보스)
                }
                else if (phase == Phase.ReturnPrep && phaseTime >= config.returnPrepTime)
                {
                    BeginNight(false);
                }
                else if (phase == Phase.Night && phaseTime >= config.nightDuration)
                {
                    BeginMorning();
                }
            }

            ProcessLevelQueue();
        }

        UpdateVisuals();
    }

    private void StartRun()
    {
        started = true;
        mapCenter = gm.player.transform.position;
        baseMaxHealth = gm.maxHealth;
        gm.maxGameTime = config.totalTime;                   // 한 판 카운트다운 (귀환 준비 동안은 정지)
        DayStartGameTime = gm.gameTime;
        if (NightDefense.Instance != null) NightDefense.Instance.ResetRun();
        DayMonsterStash.Clear();
        if (TurretSystem.Instance != null) TurretSystem.Instance.ResetRun();   // 발판을 풀로 (낮), 포탑 없음
        UpgradeShop.Create(mapCenter + (Vector3)config.campOffset);
        CampZone.Create();                                   // 텐트 울타리 + 상점 NPC
        GemSave.ApplyToRun();                                // 장착한 젬을 '젬' 출처로 적용 (시작 리롤·코인 지급)
        int bonusPicks = Mathf.RoundToInt(RunStats.Get(RunStat.BonusPicks));
        if (bonusPicks > 0 && LevelUp.instance != null) LevelUp.instance.ShowBonus(bonusPicks);   // 보너스 선택 젬: 시작 전에 카드 선택
        GameResultView.BeginRun();                           // 생존 시간 기록 시작
        ShowBanner("1일차 낮");
        DaySky.NewDay();
    }

    // 귀환 준비: 전체 타이머·낮 스폰 정지, 경고 문구·경고음, 기지 쪽 이동 버프 (표시는 NightDefense)
    private void BeginReturnPrep()
    {
        phase = Phase.ReturnPrep;
        phaseTime = 0f;
        DayMonsterStash.HideAll();                           // 낮 일반 몬스터·중간보스 땅속으로 (다음 날 낮에 다시 등장)
        if (config.returnPrepSound != null && AudioManager.instance != null) AudioManager.instance.PlayClip(config.returnPrepSound);
        ReturnPrepStarted?.Invoke();
    }

    private void BeginNight(bool endless)
    {
        if (phase != Phase.ReturnPrep) DayMonsterStash.HideAll();   // 끝없는 밤처럼 귀환 준비 없이 밤이 되면 지금 숨김
        phase = Phase.Night;
        phaseTime = 0f;
        nightIndex++;
        ShowBanner(endless ? "끝없는 밤이 이어진다냥..." : dayCount + "일차 밤이 찾아왔다냥!");
    }

    private void BeginMorning()
    {
        phase = Phase.Day;
        phaseTime = 0f;
        dayCount++;
        if (!bossStarted) nightsSurvived++;           // 젬 실패 보상 기준
        DayStartGameTime = gm.gameTime;
        if (NightDefense.Instance != null) NightDefense.Instance.ClearNight();   // 길 위 밤 몬스터 즉시 소멸 (보상·기지 피해 없음)
        if (!bossStarted) DayMonsterStash.RestoreAll();       // 숨겨 둔 낮 몬스터·중간보스가 카메라 밖에서 다시 등장
        NightEnded?.Invoke();
        DaySky.NewDay();                              // 아침마다 해 방향이 바뀜

        RunStats.Expire(ModifierLife.UntilMorning);   // 도전 선택지의 효과는 아침까지
        ResolveQuest();
        // "아침이 밝았다냥!" 배너는 아침 선택지 제목과 겹쳐서 띄우지 않음

        int picks = nextMorningPicks;
        nextMorningPicks = 1;
        MorningChoice.Open(picks);
    }

    private void OnBossAppeared()
    {
        bossStarted = true;
        DayMonsterStash.Clear();                             // 보스전: 숨겨 둔 낮 몬스터는 다시 나오지 않음
        BossStarted?.Invoke();
        phase = Phase.Day;
        questActive = false;                         // 보스가 나오면 남은 도전 리스크는 즉시 해제
        RunStats.Expire(ModifierLife.UntilMorning);
    }

    private void ResolveQuest()
    {
        if (!questActive)
            return;

        questActive = false;
        int kills = gm.kill - questStartKills;
        if (kills >= config.questKillGoal)
        {
            QueueLevelUps(2);
            MorningChoice.LevelUpBestWeapon();
            ShowBanner("토벌 의뢰 성공! 레벨 +2, 무기 레벨 +1");
        }
        else
        {
            CoinWallet.Add(-30);
            ShowBanner("토벌 의뢰 실패... 코인 -30");
        }
    }

    private void ProcessLevelQueue()
    {
        if (pendingLevels <= 0 || MorningChoice.IsOpen || UpgradeShop.IsOpen)
            return;
        if (LevelUp.instance == null || !LevelUp.instance.LevelUpFinish)
            return;

        pendingLevels--;
        gm.level++;
        gm.uiLevelUp.Show();
    }

    // 최대 체력 반영: (기준 + 고정값) x 배율 (늘어난 만큼은 현재 체력도 늘리고, 줄어들면 최대치에 맞춤)
    private void ApplyMaxHealth()
    {
        float target = (baseMaxHealth + RunStats.Get(RunStat.MaxHealthFlat)) * RunStats.Get(RunStat.MaxHealth) * GimmickCards.MaxHealthScale;   // 생명의 일격은 다른 체력 효과 뒤에 곱함   // 고정값을 먼저 더한 뒤 % 효과
        if (Mathf.Abs(target - gm.maxHealth) < 0.01f)
            return;

        float delta = target - gm.maxHealth;
        gm.maxHealth = target;
        if (delta > 0f && gm.health > 0f)
        {
            gm.health += delta;
        }
        gm.health = Mathf.Min(gm.health, gm.maxHealth);
    }

    private void Regenerate()
    {
        float regen = RunStats.Get(RunStat.Regen);
        if (regen > 0f)
        {
            gm.Heal(regen * Time.deltaTime);
        }
    }

    // ---------------------------------------------------------------
    // 화면 표시
    // ---------------------------------------------------------------
    private void BuildOverlay()
    {
        // 밤 어둠: 게임 화면 위, 기존 UI 아래 (정렬 순서 -10)
        Canvas canvas = RunUI.CreateCanvas("Night Overlay", -10, false);
        overlayCanvasRect = (RectTransform)canvas.transform;

        vignetteTexture = CreateVignette(256, config.nightInnerDarkness);
        builtInnerDarkness = config.nightInnerDarkness;
        RectTransform rect = RunUI.Rect("Darkness", canvas.transform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 2000f);
        nightOverlay = rect.gameObject.AddComponent<RawImage>();
        nightOverlay.texture = vignetteTexture;
        nightOverlay.raycastTarget = false;
        nightOverlay.color = new Color(0f, 0f, 0f, 0f);
    }

    // 가운데(시야 안)는 innerDarkness만큼만 어둡고, 바깥으로 갈수록 완전히 어두워지는 원형 텍스처
    private static Texture2D CreateVignette(int size, float innerDarkness)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size - 0.5f;
                float dy = (y + 0.5f) / size - 0.5f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(VignetteHole * 0.6f, VignetteHole * 1.25f, distance));
                float alpha = Mathf.Lerp(innerDarkness, 1f, edge);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void BuildHud()
    {
        // 시계, 코인 등은 기존 UI(레벨업 창, 정렬 0~3)보다 뒤, 밤 어둠(-10)보다 앞
        Canvas canvas = RunUI.CreateCanvas("Day Night HUD", -5, false);
        hudCanvasRect = (RectTransform)canvas.transform;
        hudRoot = canvas.gameObject;
        hudRoot.SetActive(false);                                // 캐릭터 선택 화면에서는 숨김
        Vector2 top = new Vector2(0f, 1f);                       // 화면 왼쪽 위 모서리 기준

        // 시계 (시침: 하루 한 바퀴, 분침: 낮/밤 한 번에 한 바퀴)
        RectTransform clockRect = RunUI.Rect("Clock", canvas.transform, top, config.clockOffset, Vector2.one * config.clockSize);
        clockImage = clockRect.gameObject.AddComponent<Image>();
        clockImage.sprite = config.clockSprite;
        clockImage.preserveAspect = true;
        clockImage.raycastTarget = false;
        clockImage.enabled = config.clockSprite != null;

        // 낮·밤 구간 표시: 시계판 뒤 테두리 바깥으로만 살짝 비치는 빛 고리 (시계 그림은 그대로)
        // 시침이 하루(낮+밤) 한 바퀴 → 위(12시)부터 시계 방향으로 낮 구간(노랑), 나머지 밤 구간(남색)
        Sprite circle = UiCircleSprite.Create();
        RectTransform zones = RunUI.Rect("Clock Zones", canvas.transform, top, config.clockOffset, Vector2.one * config.clockSize);
        zones.SetSiblingIndex(clockRect.GetSiblingIndex());      // 시계판보다 먼저 그림 (가운데는 시계판이 덮음)
        Image nightZone = RunUI.Icon("Night Zone", zones, circle, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * config.clockSize * config.clockZoneSize);
        nightZone.color = config.clockNightZoneColor;
        dayZone = RunUI.Icon("Day Zone", zones, circle, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * config.clockSize * config.clockZoneSize);
        dayZone.color = config.clockDayZoneColor;
        dayZone.type = Image.Type.Filled;
        dayZone.fillMethod = Image.FillMethod.Radial360;
        dayZone.fillOrigin = (int)Image.Origin360.Top;
        dayZone.fillClockwise = true;
        dayZone.fillAmount = config.dayDuration / Mathf.Max(1f, config.dayDuration + config.nightDuration);

        hourHand = CreateHand("Hour Hand", clockRect, config.hourHandSprite, 0.3f);
        minuteHand = CreateHand("Minute Hand", clockRect, config.minuteHandSprite, 0.4f);

        // 낮/밤 표시는 시계 바로 밑 두 줄 ("1일차 낮" / "1:26")
        clockLabel = RunUI.Label("Clock Label", canvas.transform, "", config.clockFontSize, Color.white, TextAnchor.MiddleCenter,
            top, config.clockLabelUnderOffset, new Vector2(200f, config.clockFontSize * 2.6f));
        clockLabel.lineSpacing = 0.95f;
        RunUI.ApplyPanelFont(clockLabel);                        // 능력치 판과 같은 글꼴 (나눔스퀘어라운드 EB)
        Outline clockOutline = clockLabel.GetComponent<Outline>();
        if (clockOutline != null)
        {
            // 부드러운 글꼴에 두꺼운 대각선 외곽선이면 번져 보임 → 얇고 진하게
            clockOutline.effectDistance = new Vector2(config.clockLabelOutline, -config.clockLabelOutline);
            clockOutline.effectColor = new Color(0f, 0f, 0f, 1f);
        }

        // 코인 보유량
        RectTransform coinRect = RunUI.Rect("Coin Counter", canvas.transform, top, config.coinCounterOffset, new Vector2(200f, 50f));
        RunUI.Icon("Icon", coinRect, config.coinIcon, new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(40f, 40f));
        coinLabel = RunUI.Label("Count", coinRect, "0", config.coinFontSize, new Color(1f, 0.9f, 0.4f), TextAnchor.MiddleLeft,
            new Vector2(0f, 0.5f), new Vector2(120f, 0f), new Vector2(150f, 50f), 0f);
        // 킬 수 글자와 같은 외곽선: 불투명 검정 두 겹 (반투명 한 겹이면 흐려 보임)
        for (int i = 0; i < 2; i++)
        {
            Outline o = coinLabel.gameObject.AddComponent<Outline>();
            o.effectColor = Color.black;
            o.effectDistance = new Vector2(config.coinOutline, -config.coinOutline);
        }

        questLabel = RunUI.Label("Quest", canvas.transform, "", 22, new Color(1f, 0.75f, 0.4f), TextAnchor.MiddleLeft,
            top, config.questLabelOffset, new Vector2(230f, 36f));

        // 알림 문구는 화면 가운데 기준 (시계와 같은 캔버스라 함께 숨겨짐)
        RectTransform bannerLayer = RunUI.Rect("Banner Layer", canvas.transform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        bannerLayer.anchorMin = Vector2.zero;
        bannerLayer.anchorMax = Vector2.one;
        bannerCanvas = bannerLayer.gameObject.AddComponent<Canvas>();
        bannerCanvas.overrideSorting = true;
        bannerCanvas.sortingOrder = config.bannerSortingOrder;   // 상점 표지 등 다른 UI보다 위
        banner = RunUI.Label("Banner", bannerLayer, "", 46, Color.white, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(900f, 80f), 3f);   // 화면 위 HUD에 가리지 않게 조금 아래
        banner.color = new Color(1f, 1f, 1f, 0f);
    }

    private static RectTransform CreateHand(string name, RectTransform clock, Sprite sprite, float length)
    {
        RectTransform hand = UltimateManager.CreateRect(name, clock);
        hand.anchorMin = new Vector2(0.5f, 0.5f);
        hand.anchorMax = new Vector2(0.5f, 0.5f);
        hand.pivot = new Vector2(0.5f, 0.1f);                // 바늘 아래쪽을 중심으로 회전
        hand.anchoredPosition = Vector2.zero;
        hand.sizeDelta = new Vector2(clock.sizeDelta.x * 0.12f, clock.sizeDelta.y * length);

        Image image = hand.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.enabled = sprite != null;
        return hand;
    }

    public void ShowBanner(string message)
    {
        if (bannerCanvas != null && bannerCanvas.isActiveAndEnabled)
        {
            bannerCanvas.overrideSorting = true;                 // HUD가 꺼진 채 만들어지면 풀리므로 띄울 때마다 다시 적용
            bannerCanvas.sortingOrder = config.bannerSortingOrder;
        }
        banner.text = message;
        bannerTime = Time.unscaledTime;
    }

    private void UpdateVisuals()
    {
        // 게임이 시작된 뒤(캐릭터 선택 후)에만 시계, 코인 표시
        bool showHud = started && gm.player.gameObject.activeInHierarchy;
        HudVisible = showHud;
        if (hudRoot.activeSelf != showHud)
        {
            hudRoot.SetActive(showHud);
        }

        // 어둠: 밤이면 서서히 어두워지고, 아침이면 서서히 밝아짐 (일시정지 중에도 진행)
        // 보스전은 평소 밤보다 조금 밝은 밤 (배경음은 그대로)
        // 노을 동안 조금씩 어두워져 밤으로 이어짐 (노을 → 갑자기 검게 되지 않게)
        float duskDim = started && !bossStarted && phase == Phase.ReturnPrep ? config.duskDarkness
            : started && !bossStarted && phase == Phase.Day && config.sunsetTime > 0f && dayCount < LastDay
            ? Mathf.Clamp01((phaseTime - (DayLength - config.sunsetTime)) / config.sunsetTime) * config.duskDarkness : 0f;
        float target = !started ? 0f : bossStarted ? BossNightDarkness : phase == Phase.Night ? 1f : duskDim;
        darkness = Mathf.MoveTowards(darkness, target, Time.unscaledDeltaTime / (phase == Phase.Night ? config.transitionTime * 2f : config.transitionTime));
        Daylight = started ? 1f - darkness : 0f;
        DayProgress = started && phase == Phase.Day && !bossStarted ? Mathf.Clamp01(phaseTime / Mathf.Max(1f, DayLength)) : phase == Phase.ReturnPrep ? 1f : 0.5f;
        float dusk01 = started && !bossStarted && phase == Phase.ReturnPrep ? 1f
            : started && !bossStarted && phase == Phase.Day && config.sunsetTime > 0f && dayCount < LastDay
            ? Mathf.Clamp01((phaseTime - (DayLength - config.sunsetTime)) / config.sunsetTime) : 0f;
        // 밤이 되면 어둠이 짙어지는 만큼 노을이 서서히 빠짐
        SunsetProgress = phase == Phase.Night && !bossStarted ? Mathf.Clamp01(1f - darkness) : dusk01;

        UpdateBgmFilter();

        Color night = config.nightColor;
        // 어둠 사냥 등으로 어둠이 추가되면 시야 밖은 완전히 검게, 시야 안도 더 어둡게
        float extra = RunStats.Get(RunStat.NightDarkness);
        float inner = Mathf.Clamp01(config.nightInnerDarkness + extra);
        if (Mathf.Abs(inner - builtInnerDarkness) > 0.01f)
        {
            Destroy(vignetteTexture);
            vignetteTexture = CreateVignette(256, inner);
            nightOverlay.texture = vignetteTexture;
            builtInnerDarkness = inner;
        }
        // 어둠 색: 막 어두워질 때는 노을빛이 섞인 어둠 → 깊어질수록 밤 색
        Color nightTint = Color.Lerp(config.duskNightColor, night, Mathf.SmoothStep(0f, 1f, darkness));
        nightOverlay.color = new Color(nightTint.r, nightTint.g, nightTint.b, Mathf.Clamp01(night.a + extra) * darkness);
        nightOverlay.enabled = darkness > 0.001f;
        if (nightOverlay.enabled)
        {
            PlaceVignette();
        }

        // 시계
        float cycle = config.dayDuration + config.nightDuration;
        float dayFraction = config.dayDuration / cycle;
        float phaseLength = Mathf.Max(0.01f, CurrentPhaseLength);
        float phaseProgress = Mathf.Clamp01(phaseTime / phaseLength);
        float cycleProgress = phase == Phase.Day ? phaseProgress * dayFraction : phase == Phase.ReturnPrep ? dayFraction : dayFraction + phaseProgress * (1f - dayFraction);

        hourHand.localRotation = Quaternion.Euler(0f, 0f, -cycleProgress * 360f);
        if (dayZone != null) dayZone.fillAmount = dayFraction;
        minuteHand.localRotation = Quaternion.Euler(0f, 0f, -phaseProgress * 360f);
        clockImage.color = Color.Lerp(Color.white, config.nightClockTint, darkness);

        if (bossStarted)
        {
            clockLabel.text = "보스전!";
        }
        else
        {
            int remain = Mathf.CeilToInt(Mathf.Max(0f, phaseLength - phaseTime));
            string phaseName = phase == Phase.Day ? "낮" : phase == Phase.ReturnPrep ? "준비" : "밤";
            clockLabel.text = dayCount + "일차 " + phaseName + "\n" + (remain / 60) + ":" + (remain % 60).ToString("00");
        }

        coinLabel.text = "x " + CoinWallet.Coins;

        questLabel.text = questActive
            ? "토벌 의뢰 " + Mathf.Min(gm.kill - questStartKills, config.questKillGoal) + "/" + config.questKillGoal
            : "";

        // 알림 문구: 0.4초 나타나고 2초 유지 후 사라짐
        float age = Time.unscaledTime - bannerTime;
        float alpha = age < 0.4f ? age / 0.4f : 1f - Mathf.Clamp01((age - 2.4f) / 0.8f);
        banner.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        if (alpha > 0f && bannerCanvas != null && bannerCanvas.isActiveAndEnabled && !bannerCanvas.overrideSorting)
        {
            bannerCanvas.overrideSorting = true;                 // 비활성 상태에서 풀린 정렬을 다시 최상단으로
            bannerCanvas.sortingOrder = config.bannerSortingOrder;
        }
    }

    // 밤에는 배경음에 저역 통과 필터를 걸어 먹먹하고 어두운 느낌으로 (어두워지는 정도에 맞춰 서서히)
    private void UpdateBgmFilter()
    {
        if (bgmFilter == null)
        {
            AudioManager audio = AudioManager.instance;
            if (audio == null || audio.bgmPlayer == null)
                return;

            bgmFilter = audio.bgmPlayer.GetComponent<AudioLowPassFilter>();
            if (bgmFilter == null)
            {
                bgmFilter = audio.bgmPlayer.gameObject.AddComponent<AudioLowPassFilter>();
            }
            bgmFilter.lowpassResonanceQ = 1f;
        }

        // 주파수는 비율로 줄여야 귀에 자연스럽게 들림 (22000 → nightBgmCutoff)
        float muffle = bossStarted ? 0f : darkness;           // 보스전 배경음은 먹먹하게 하지 않음
        bgmFilter.cutoffFrequency = 22000f * Mathf.Pow(config.nightBgmCutoff / 22000f, muffle);
        bgmFilter.enabled = muffle > 0.001f;
    }

    // 어둠의 구멍을 캐릭터 위치에 맞추고 크기를 시야 반경에 맞춤
    private void PlaceVignette()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector2 screen = cam.WorldToScreenPoint(gm.player.transform.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayCanvasRect, screen, null, out Vector2 local);

        float radius = overlayCanvasRect.rect.height * config.nightVisionRadius * RunStats.Get(RunStat.NightVision);
        float size = Mathf.Max(radius / VignetteHole, overlayCanvasRect.rect.height * 3f);
        RectTransform rect = nightOverlay.rectTransform;
        rect.anchoredPosition = local;
        rect.sizeDelta = Vector2.one * size;

        // 화면을 다 덮을 만큼 크게 그리되, 구멍이 시야 반경이 되도록 텍스처를 축소해서 표시
        // (텍스처 바깥은 가장자리 색(완전히 어두움)으로 채워짐)
        float zoom = radius / (size * VignetteHole);
        float half = 0.5f / Mathf.Max(0.0001f, zoom);
        nightOverlay.uvRect = new Rect(0.5f - half, 0.5f - half, half * 2f, half * 2f);
    }
}
