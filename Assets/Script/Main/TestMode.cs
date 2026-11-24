using UnityEngine;
using UnityEngine.SceneManagement;

// 테스트 모드 (개발 확인용)
// 게임을 실행할 때마다 무조건 꺼진 상태로 시작하며, 인스펙터에서 컴포넌트 체크박스를 직접 켜야 동작한다.
// 켜지면 화면 왼쪽 위에 테스트 패널이 나타나고, 끄면 모든 효과가 원래대로 돌아간다.
// 에디터에서는 Main 씬을 실행하면 "Test Mode" 오브젝트가 자동으로 만들어진다 (씬에 직접 붙여도 됨).
//
// 능력치 막대는 지금 캐릭터의 실제 능력치(레벨업으로 얻은 값)를 보여 주며, 막대를 움직이면 그 값이 되도록 보정한다.
// 원래 능력치를 직접 바꾸지 않으므로 테스트 모드를 끄면 원래 값으로 돌아간다. (필살기 버프는 표시에서 제외)
public class TestMode : MonoBehaviour
{
    [Header("# 피격 / 리롤 / 스킬")]
    public bool noHit;                                       // 켜면 플레이어가 피해를 받지 않음
    public bool infiniteReroll;                              // 켜면 레벨업 리롤 횟수 무한
    public bool noSkillCooldown;                             // 켜면 메인무기 전용 스킬 쿨타임 없음
    public bool noLevelUp;                                   // 켜면 경험치를 먹어도 레벨업하지 않음 ('레벨업 발동' 버튼은 동작)

    [Header("# 패널")]
    public bool showPanel = true;                            // 화면 테스트 패널 표시
    [Min(0.5f)] public float panelScale = 1.5f;              // 패널 크기
    public bool collapsed;                                   // 패널 접기 (제목 줄만 표시)
    [Min(240f)] public float panelWidth = 470f;
    public Color panelBackColor = new Color(0.05f, 0.05f, 0.07f, 0.88f);   // 패널 뒤 어두운 바탕 (뒤 게임 화면에 묻히지 않게)
    private static Texture2D backTex;              // 펼쳤을 때 패널 가로 폭 (패널 크기 배율 적용 전)
    private string seedInput = "";                           // 맵 시드 입력칸

    // 실제 능력치에 곱하는 보정값 (패널 막대로 조절)
    private float damageMultiplier = 1f;
    private float cooldownMultiplier = 1f;
    private float moveSpeedMultiplier = 1f;
    private float attackSpeedMultiplier = 1f;

    private static TestMode instance;
    private Rect windowRect = new Rect(10f, 10f, 340f, 0f);
    private Vector2 scroll;                                  // 패널 스크롤 위치

    // 패널 크기: 설정값을 기준 화면 높이(1280)에 맞춰 조절 (가로 1080 화면에서 너무 커져 잘리지 않게)
    private float PanelScale => Mathf.Max(0.5f, panelScale * Mathf.Min(1f, Screen.height / 1280f));

    // 다른 스크립트에서 읽는 값 (테스트 모드가 꺼져 있으면 항상 기본값)
    public static bool IsActive => instance != null && instance.isActiveAndEnabled;
    public static bool NoHit => IsActive && instance.noHit;
    public static bool InfiniteReroll => IsActive && instance.infiniteReroll;
    public static bool NoSkillCooldown => IsActive && instance.noSkillCooldown;
    public static bool NoLevelUp => IsActive && instance.noLevelUp;
    public static float DamageScale => IsActive ? instance.damageMultiplier : 1f;
    public static float CooldownScale => IsActive ? instance.cooldownMultiplier : 1f;
    public static float MoveSpeedScale => IsActive ? instance.moveSpeedMultiplier : 1f;
    public static float AttackSpeedScale => IsActive ? instance.attackSpeedMultiplier : 1f;

    // 마우스가 테스트 패널 위에 있는지 (패널 클릭이 메인무기 공격으로 이어지지 않도록)
    public static bool IsPointerOverPanel
    {
        get
        {
            if (!IsActive || !instance.showPanel)
                return false;

            Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) / instance.PanelScale;
            return instance.windowRect.Contains(mouse);
        }
    }

    private void Awake()
    {
        instance = this;
        enabled = false;                                     // 실행할 때마다 무조건 OFF

        // 찾기 쉽도록 하이어라키 맨 위에 배치
        if (transform.parent == null)
        {
            transform.SetAsFirstSibling();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

#if UNITY_EDITOR
    // 에디터에서 GameManager가 있는 씬을 실행하면 꺼진 테스트 모드 오브젝트를 자동으로 만들어 둠
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterAutoCreate()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindObjectOfType<GameManager>() == null || FindObjectOfType<TestMode>(true) != null)
            return;

        new GameObject("Test Mode").AddComponent<TestMode>();
    }
#endif

    // ---------------------------------------------------------------
    // 기능 (패널 버튼, 컴포넌트 우클릭 메뉴에서 실행)
    // ---------------------------------------------------------------
    [ContextMenu("레벨업 발동")]
    public void TriggerLevelUp()
    {
        GameManager gm = GameManager.instance;
        if (!IsActive || gm == null || !gm.isLive || LevelUp.instance == null || !LevelUp.instance.LevelUpFinish)
            return;

        gm.level++;
        gm.exp = 0;
        gm.uiLevelUp.Show();
    }

    [ContextMenu("보스 소환")]
    public void SpawnBoss()
    {
        GameManager gm = GameManager.instance;
        if (!IsActive || gm == null || !gm.isLive)
            return;

        if (gm.isBossSpawn)
        {
            Debug.Log("[TestMode] 보스가 이미 소환되었습니다.");
            return;
        }

        // 시간이 다 됐을 때와 같은 방식으로 소환 (남은 시간 표시도 보스전으로 전환)
        gm.isBossSpawn = true;
        gm.gameTime = gm.maxGameTime;
        gm.player.GetComponentInChildren<Spawner>().BossSpawn();
    }

    // 메인무기를 빼고 그 무기를 처음 상태(0레벨)로 되돌림. 남은 무기가 없으면 나무 막대기로 시작 상태
    [ContextMenu("메인무기 제거")]
    public void RemoveMainWeapon()
    {
        MainWeaponController main = MainWeaponController.Current;
        if (main == null)
            return;
        Weapon weapon = main.GetComponent<Weapon>();
        GameManager gm = GameManager.instance;
        ItemData dual = null;
        foreach (Item item in gm.uiLevelUp.Items)
        {
            if (item.data.itemType == ItemData.ItemType.DualBlades) dual = item.data;
            if (item.weapon == weapon)
            {
                item.weapon = null;
                item.level = 0;                              // 다시 얻을 수 있게
            }
        }
        DestroyImmediate(main);                              // 메인무기 기능 정리 (게이지·스킬 버튼 등)
        Weapon.RemoveForTest(weapon);

        bool anyWeapon = false;
        foreach (Weapon w in FindObjectsOfType<Weapon>())
        {
            if (w != weapon && !w.IsStarter) anyWeapon = true;
        }
        if (!anyWeapon && dual != null && RunConfig.Instance.woodStickSprite != null)
        {
            Weapon.CreateStarter(dual, RunConfig.Instance.woodStickSprite);   // 무기가 하나도 없으면 시작 나무 막대기
        }
        Debug.Log("[TestMode] 메인무기를 제거했습니다.");
    }

    [ContextMenu("체력 회복")]
    public void HealFull()
    {
        GameManager gm = GameManager.instance;
        if (!IsActive || gm == null || !gm.isLive)
            return;

        gm.health = gm.maxHealth;
    }

    // 지정한 무기를 메인무기 레벨까지 올려 바로 메인무기로 만듦 (이미 메인무기가 있으면 동작 안 함)
    public void GiveMainWeapon(ItemData.ItemType type)
    {
        GameManager gm = GameManager.instance;
        if (!IsActive || gm == null || !gm.isLive || LevelUp.instance == null || MainWeaponController.Current != null)
            return;

        foreach (Item item in LevelUp.instance.Items)
        {
            if (item.data.itemType != type)
                continue;

            // 레벨업 카드를 고른 것과 같은 방식으로 레벨을 올림 (메인무기 레벨에 닿으면 자동으로 메인무기가 됨)
            int guard = 0;
            while (MainWeaponController.Current == null && item.level < item.data.damages.Length && guard++ < 20)
            {
                item.OnClick();
            }
            return;
        }
    }

    // 지정한 무기 레벨을 1 올림 (레벨업 카드를 고른 것과 같음, 만렙이면 동작 안 함)
    public void LevelUpWeapon(ItemData.ItemType type)
    {
        GameManager gm = GameManager.instance;
        if (!IsActive || gm == null || !gm.isLive || LevelUp.instance == null)
            return;

        foreach (Item item in LevelUp.instance.Items)
        {
            if (item.data.itemType == type && item.level < item.data.damages.Length && (item.weapon == null || !item.weapon.IsMain))   // 메인무기 제외
            {
                item.OnClick();
                return;
            }
        }
    }

    private static string SynergyStageName()
    {
        switch (WeaponSynergy.Current)
        {
            case WeaponSynergy.Stage.Resonance: return "공명";
            case WeaponSynergy.Stage.Awakening: return "각성";
            default: return "없음";
        }
    }

    [ContextMenu("능력치 원래대로")]
    public void ResetStats()
    {
        damageMultiplier = 1f;
        cooldownMultiplier = 1f;
        moveSpeedMultiplier = 1f;
        attackSpeedMultiplier = 1f;
    }

    // ---------------------------------------------------------------
    // 화면 패널 (게임이 멈춘 레벨업 중에도 동작)
    // ---------------------------------------------------------------
    private void OnGUI()
    {
        if (!showPanel)
            return;

        Matrix4x4 previous = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(PanelScale, PanelScale, 1f));
        windowRect.width = collapsed ? 120f : panelWidth;     // 인스펙터에서 폭을 바꾸면 바로 반영
        if (backTex == null)
        {
            backTex = new Texture2D(1, 1);
            backTex.SetPixel(0, 0, Color.white);
            backTex.Apply();
        }
        Color oldColor = GUI.color;
        GUI.color = panelBackColor;
        GUI.DrawTexture(windowRect, backTex);                  // 창 뒤 어두운 바탕
        GUI.color = oldColor;
        windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, "TEST MODE");
        GUI.matrix = previous;
    }

    private static readonly ItemData.ItemType[] MainWeaponTypes =
    {
        ItemData.ItemType.DualBlades, ItemData.ItemType.HuntingBow, ItemData.ItemType.SniperHBG,
        ItemData.ItemType.GreatSword, ItemData.ItemType.Lance,
    };
    private static readonly string[] MainWeaponNames = { "쌍검", "활", "헤비보우건", "대검", "랜스" };

    // 가로 줄 안에서 버튼 n개마다 다음 줄로 넘김
    [Min(1)] public int buttonsPerRow = 3;
    private void RowBreak(int index)
    {
        if (index > 0 && index % Mathf.Max(1, buttonsPerRow) == 0)
        {
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
        }
    }

    private void DrawWindow(int windowId)
    {
        GameManager gm = GameManager.instance;
        bool live = gm != null && gm.isLive;

        // 접기 / 열기
        if (GUILayout.Button(collapsed ? "▶ 열기" : "▼ 접기"))
        {
            collapsed = !collapsed;
            windowRect.height = 0f;                          // 접으면 창 높이를 다시 계산
        }
        if (collapsed)
        {
            GUI.DragWindow();
            return;
        }

        // 내용이 화면보다 길면 위아래로 스크롤 (마우스 휠 또는 오른쪽 스크롤바)
        // 높이를 고정해야 창이 찌그러지지 않음 (가로 화면에서도 화면 높이의 대부분을 씀)
        float viewHeight = Mathf.Max(220f, Screen.height / Mathf.Max(0.5f, PanelScale) - windowRect.y - 80f);
        scroll = GUILayout.BeginScrollView(scroll, false, true, GUIStyle.none, GUI.skin.verticalScrollbar, GUI.skin.scrollView,
            GUILayout.Width(panelWidth - 16f), GUILayout.Height(viewHeight));   // 세로 스크롤만 (가로 스크롤바 없음, 창 안쪽 여백만큼 좁게)

        GUI.enabled = live;
        if (GUILayout.Button("레벨업 발동"))
        {
            TriggerLevelUp();
        }

        // 지정한 레벨의 선택 규칙으로 레벨업 (17의 배수·5의 배수·짝수·홀수 확인)
        GUILayout.BeginHorizontal();
        GUILayout.Label("레벨", GUILayout.Width(40f));
        levelInput = GUILayout.TextField(levelInput, GUILayout.Width(60f));
        if (GUILayout.Button("이 레벨로 레벨업") && int.TryParse(levelInput.Trim(), out int targetLevel) && gm != null && LevelUp.instance != null && LevelUp.instance.LevelUpFinish)
        {
            gm.level = Mathf.Max(0, targetLevel - 1);
            TriggerLevelUp();
        }
        GUILayout.EndHorizontal();

        // 캐릭터 강화(젬) 저장 데이터
        GUILayout.Label("── 젬 (저장 " + GemSave.Current.clearCount + "회 클리어) ──");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("젬 +5 지급")) GemSave.AddGems(5);
        if (GUILayout.Button("젬 +16 (전종)"))
        {
            for (int i = 0; i < GemCatalog.Instance.gems.Count; i++) GemSave.Current.gemCounts[i]++;
            GemSave.Save();
        }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("클리어 횟수", GUILayout.Width(80f));
        for (int n = 0; n <= 2; n++)
        {
            if (GUILayout.Button(n.ToString(), GUILayout.Width(40f))) { GemSave.Current.clearCount = n; GemSave.Save(); }
        }
        if (GUILayout.Button("저장 초기화")) GemSave.ResetAll();
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("특수 젬 +1 생성")) GemSave.CreateSpecial();
        if (GUILayout.Button("조각 +50")) GemSave.AddShards(50);
        GUILayout.EndHorizontal();
        GUILayout.Label("특수 젬 " + GemSave.Current.specials.Count + "개 · 조각 " + GemSave.Current.shards);

        // 전투 기믹 카드 1단계 올리기
        showGimmicks = GUILayout.Toggle(showGimmicks, " 전투 기믹 카드 올리기");
        if (showGimmicks && LevelUp.instance != null)
        {
            foreach (ItemData.ItemType type in GimmickCatalog.Order)
            {
                Item card = System.Array.Find(LevelUp.instance.Items, x => x != null && x.data != null && x.data.itemType == type);
                if (card == null) continue;
                bool max = card.level >= card.data.damages.Length;
                GUI.enabled = live && !max;
                if (GUILayout.Button(card.data.itemName + " (" + card.level + "/" + card.data.damages.Length + ")"))
                {
                    card.OnClick();
                }
            }
            GUI.enabled = live;
        }

        GUI.enabled = live && !gm.isBossSpawn;
        if (GUILayout.Button(gm != null && gm.isBossSpawn ? "보스 소환됨" : "보스 소환"))
        {
            SpawnBoss();
        }

        GUI.enabled = live;
        if (GUILayout.Button("체력 회복"))
        {
            HealFull();
        }

        GUILayout.BeginHorizontal();
        GUI.enabled = live && CampLoiterPenalty.Instance != null;
        if (GUILayout.Button("캠프 처벌 +10초"))
        {
            CampLoiterPenalty.Instance.AddTestSeconds(10f);   // 처벌 전: 누적 +10초 / 처벌 중: 게이지 +10
        }
        if (GUILayout.Button("버틴 시간 +10초"))
        {
            CampLoiterPenalty.Instance.AddTestOvertime(10f);  // 처벌 중: 스택 +1
        }
        GUILayout.EndHorizontal();
        GUI.enabled = live && MainWeaponController.Current != null;
        if (GUILayout.Button("메인무기 제거"))
        {
            RemoveMainWeapon();
        }
        GUI.enabled = true;
        // [임시 확인용] 캠프 처벌 확인창 (경고 영역·상점 예외 영역·수치) - CampLoiterDebug를 지울 때 같이 지울 것
        if (GUILayout.Button(CampLoiterDebug.Visible ? "캠프 처벌 확인창 닫기" : "캠프 처벌 확인창 열기"))
        {
            CampLoiterDebug.Visible = !CampLoiterDebug.Visible;
        }
        GUI.enabled = live;

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("코인 +100"))
        {
            CoinWallet.Add(100);
        }
        bool canSkip = live && DayNightCycle.Instance != null && !gm.isBossSpawn;
        GUI.enabled = canSkip;
        string phaseName = DayNightCycle.Instance != null && DayNightCycle.Instance.CurrentPhase == DayNightCycle.Phase.Night ? "밤" : "낮";
        if (GUILayout.Button(phaseName + " 넘기기"))
        {
            DayNightCycle.Instance.SkipPhase();
        }
        GUILayout.EndHorizontal();

        // 밤 디펜스: 귀환 준비부터 바로 시작 / 기지 체력 깎기
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("밤 즉시 시작"))
        {
            DayNightCycle.Instance.StartReturnPrepNow();
        }
        GUI.enabled = live && NightDefense.Instance != null;
        if (GUILayout.Button("기지 체력 -10"))
        {
            NightDefense.Instance.Damage(10);
        }
        GUILayout.EndHorizontal();
        GUI.enabled = live && NightDefense.Instance != null && DayNightCycle.IsNight;
        if (GUILayout.Button("대장 몬스터 즉시 소환 (밤)")) NightDefense.Instance.SpawnBossUnitNow();
        // 군락: 즉시 생성(현재 일차 기준) / 파괴(보상 확인) / 낮 즉시 종료(미처치 페널티 확인)
        GUILayout.BeginHorizontal();
        GUI.enabled = live && ColonySystem.Instance != null && DayNightCycle.Instance != null && DayNightCycle.Instance.CurrentPhase == DayNightCycle.Phase.Day;
        if (GUILayout.Button("군락 즉시 생성")) ColonySystem.Instance.SpawnNow();
        GUI.enabled = live && ColonySystem.Instance != null && ColonyNest.Alive.Count > 0;
        if (GUILayout.Button("군락 파괴")) ColonySystem.Instance.BreakAllNow();
        GUI.enabled = live && DayNightCycle.Instance != null && DayNightCycle.Instance.CurrentPhase == DayNightCycle.Phase.Day;
        if (GUILayout.Button("낮 즉시 종료")) DayNightCycle.Instance.StartReturnPrepNow();
        GUILayout.EndHorizontal();
        GUI.enabled = live;
        ColonySystem.ShowZones = GUILayout.Toggle(ColonySystem.ShowZones, " [임시] 군락 생성 제외 구역 표시");

        // 포탑: 전체 건설(코인 소모 없음) / 초기화 / [임시] 사거리 원 표시 (HANDOFF 기록)
        GUILayout.BeginHorizontal();
        GUI.enabled = live && TurretSystem.Instance != null;
        if (GUILayout.Button("포탑 전체 건설")) TurretSystem.Instance.BuildAllFree();
        if (GUILayout.Button("포탑 초기화")) TurretSystem.Instance.ResetTurrets();
        GUILayout.EndHorizontal();
        GUI.enabled = true;
        TurretSystem.ShowRange = GUILayout.Toggle(TurretSystem.ShowRange, " 포탑 사거리 원 표시 (임시)");

        // 아침 선택지: 바로 열기 / 카드를 골라 강제로 제시
        GUI.enabled = live && !MorningChoice.IsOpen;
        if (GUILayout.Button("아침 선택지 열기")) MorningChoice.Open(1);
        GUI.enabled = true;
        showMorningCards = GUILayout.Toggle(showMorningCards, " 아침 카드 골라서 제시 ▼");
        if (showMorningCards)
        {
            foreach (System.Collections.Generic.KeyValuePair<string, string> card in MorningChoice.CardList())
            {
                GUI.enabled = live && !MorningChoice.IsOpen;
                if (GUILayout.Button(card.Value))
                {
                    MorningChoice.ForcedId = card.Key;               // 그 등급 자리에 이 카드
                    MorningChoice.Open(1);
                }
            }
            GUI.enabled = true;
        }
        GUI.enabled = live;
        GUI.enabled = true;

        // 메인무기 바로 얻기
        bool hasMain = MainWeaponController.Current != null;
        GUILayout.Label(hasMain ? "── 메인무기 확정됨 ──" : "── 메인무기로 얻기 ──");
        GUI.enabled = live && !hasMain;
        GUILayout.BeginHorizontal();
        for (int i = 0; i < MainWeaponTypes.Length; i++)
        {
            RowBreak(i);
            if (GUILayout.Button(MainWeaponNames[i]))
            {
                GiveMainWeapon(MainWeaponTypes[i]);
            }
        }
        GUILayout.EndHorizontal();
        GUI.enabled = true;

        // 무기 레벨 +1 (시너지 공명/각성 확인용)
        GUILayout.Label("── 무기 레벨 +1 (시너지: " + SynergyStageName() + ") ──");
        GUI.enabled = live;
        GUILayout.BeginHorizontal();
        for (int i = 0; i < MainWeaponTypes.Length; i++)
        {
            RowBreak(i);
            if (GUILayout.Button(MainWeaponNames[i] + " " + WeaponSynergy.Level(MainWeaponTypes[i])))
            {
                LevelUpWeapon(MainWeaponTypes[i]);
            }
        }
        GUILayout.EndHorizontal();
        GUI.enabled = true;

        GUI.enabled = live && MainWeaponController.Current != null && WeaponSynergy.IsAwakening;
        if (GUILayout.Button("각성 효과 즉시 발동"))
        {
            MainWeaponController.Current.TestAwakening();
        }
        GUI.enabled = true;

        // 중간보스 단계 강제 시작
        GUILayout.Label("── 중간보스 (다음 단계: " + (MiniBossDirector.NextStage + 1) + ") ──");
        GUI.enabled = live && !gm.isBossSpawn;
        GUILayout.BeginHorizontal();
        // 이름은 MiniBossConfig 의 표시 이름 (명세와 같게)
        MiniBossConfig.Species[] species = MiniBossConfig.Instance != null ? MiniBossConfig.Instance.species : null;
        string[] stageNames = new string[species != null ? species.Length : 0];
        for (int s = 0; s < stageNames.Length; s++)
        {
            stageNames[s] = (s + 1) + " " + species[s].displayName;
        }
        for (int i = 0; i < stageNames.Length; i++)
        {
            RowBreak(i);
            if (GUILayout.Button(stageNames[i]))
            {
                MiniBossDirector.ForceStage(i);
            }
        }
        GUILayout.EndHorizontal();
        GUI.enabled = true;

        // 맵 장식 시드 (같은 시드 = 같은 배치)
        GUILayout.Label("── 맵 시드 : " + (MapDecorGenerator.Generated ? MapDecorGenerator.CurrentSeed.ToString() : "없음") + " ──");
        GUILayout.BeginHorizontal();
        seedInput = GUILayout.TextField(seedInput, GUILayout.Width(120f));
        if (GUILayout.Button("이 시드로 생성") && int.TryParse(seedInput.Trim(), out int seed))
        {
            MapDecorGenerator.Generate(seed);
        }
        if (GUILayout.Button("새 시드"))
        {
            MapDecorGenerator.GenerateRandom();
            seedInput = MapDecorGenerator.CurrentSeed.ToString();
        }
        GUILayout.EndHorizontal();

        noHit = GUILayout.Toggle(noHit, " 피격 판정 OFF (무적)");
        infiniteReroll = GUILayout.Toggle(infiniteReroll, " 리롤 횟수 무한");
        noSkillCooldown = GUILayout.Toggle(noSkillCooldown, " 전용 스킬 쿨타임 없음");
        noLevelUp = GUILayout.Toggle(noLevelUp, " 레벨업 불가 (경험치로 레벨업 안 함)");

        if (gm != null && gm.player != null)
        {
            DrawStats(gm.player);
        }

        showStatAdd = GUILayout.Toggle(showStatAdd, " 세부 능력치 더하기 (상점 출처로 적용)");
        if (showStatAdd)
        {
            DrawStatAdd(live);
        }

        showFinalStats = GUILayout.Toggle(showFinalStats, " 최종 능력치 확인 (출처별)");
        if (showFinalStats)
        {
            DrawFinalStats();
        }

        if (GUILayout.Button("능력치 원래대로"))
        {
            ResetStats();
        }

        GUI.enabled = true;
        GUILayout.EndScrollView();
        GUI.DragWindow();
    }

    private bool showMorningCards;

    // 세부 능력치(무기·총알 크기 등) 하나씩 더하거나 빼기. 상점 출처로 들어가 RunStats 규칙 그대로 계산됨
    private bool showStatAdd;
    [Min(0.001f)] public float statStepPercent = 0.05f;      // 퍼센트 능력치 한 번에 (0.05 = 5%)
    [Min(0.1f)] public float statStepFlat = 5f;              // 고정값 능력치 한 번에
    [Min(1f)] public float statStepCount = 1f;               // 횟수 능력치 한 번에

    private void DrawStatAdd(bool live)
    {
        StatCatalog sc = StatCatalog.Instance;
        GUI.enabled = live;
        foreach (StatCatalog.StatDef d in sc.stats)
        {
            if (d == null) continue;
            float v = RunStats.Get(d.stat);
            float shown = d.direction == StatDirection.Additive ? v : v - 1f;
            float step = d.unit == StatUnit.Percent ? statStepPercent : d.unit == StatUnit.Flat ? statStepFlat : statStepCount;
            GUILayout.BeginHorizontal();
            GUILayout.Label(d.name + " : " + sc.FormatDelta(d.stat, shown));
            if (GUILayout.Button("-", GUILayout.Width(36f))) RunStats.AddValue(d.stat, -step, StatSource.Shop);
            if (GUILayout.Button("+", GUILayout.Width(36f))) RunStats.AddValue(d.stat, step, StatSource.Shop);
            GUILayout.EndHorizontal();
        }
        GUI.enabled = true;
    }

    // 캐릭터 전체 능력치(캐릭터 기본 x 레벨업으로 얻은 능력치 x 필살기 버프)를 계산해 표시하고,
    // 막대로 정한 값이 되도록 보정값을 역산
    private void DrawStats(Player player)
    {
        GameManager gm = GameManager.instance;
        GUILayout.Space(6f);
        GUILayout.Label("── 캐릭터 전체 능력치 (막대로 조절) ──");
        GUILayout.Label("체력 : " + Mathf.CeilToInt(gm.health) + " / " + Mathf.CeilToInt(gm.maxHealth));

        // 공격력 (기본 100%)
        float totalDamage = Mathf.Max(0.01f, Character.Damage * (1f + Gear.damage_stat) * PlayerBuff.RawDamage * RunStats.Get(RunStat.Damage));
        float damage = Slider("공격력", totalDamage * damageMultiplier, 0.1f, Mathf.Max(6f, totalDamage * 2f), v => FormatPercent(v), FormatPercent(totalDamage));
        damageMultiplier = damage / totalDamage;

        // 쿨타임 감소 (남는 쿨타임 비율로 계산, 음수면 기본보다 느림)
        float totalRemain = Mathf.Max(0.01f, Character.WeaponRate * Mathf.Max(0.05f, 1f - Gear.rate_stat) * PlayerBuff.RawRateScale * RunStats.Get(RunStat.Cooldown));
        float reduction = Slider("쿨타임 감소", 1f - totalRemain * cooldownMultiplier, -1f, 0.95f, v => FormatPercent(v), FormatPercent(1f - totalRemain));
        cooldownMultiplier = (1f - reduction) / totalRemain;

        // 스테미나 = 근접 무기 공격 속도 (기본 100%)
        float totalStamina = Mathf.Max(0.01f, Character.WeaponSpeed * (1f + Gear.stamina_stat) * PlayerBuff.RawStamina * RunStats.Get(RunStat.AttackSpeed));
        float stamina = Slider("스테미나 (공격 속도)", totalStamina * attackSpeedMultiplier, 0.1f, Mathf.Max(5f, totalStamina * 2f), v => FormatPercent(v), FormatPercent(totalStamina));
        attackSpeedMultiplier = stamina / totalStamina;

        // 이동속도 (실제 값, 메인무기 모드의 증감 포함)
        float totalMove = Mathf.Max(0.01f, player.memoryspeed * PlayerBuff.RawMoveSpeed * MainWeaponController.MoveSpeedScale * RunStats.Get(RunStat.MoveSpeed));
        float move = Slider("이동속도", totalMove * moveSpeedMultiplier, 0.5f, Mathf.Max(15f, totalMove * 2f), v => v.ToString("0.0"), totalMove.ToString("0.0"));
        moveSpeedMultiplier = move / totalMove;

        // 보유 무기의 실제 피해량
        GUILayout.Space(4f);
        GUILayout.Label("── 보유 무기 피해량 ──");
        foreach (Weapon weapon in player.GetComponentsInChildren<Weapon>())
        {
            if (weapon.Data == null)
                continue;

            string main = weapon.IsMain ? " [메인]" : "";
            GUILayout.Label(weapon.Data.itemName.Trim() + main + " : " + (weapon.BaseDamage * PlayerBuff.Damage).ToString("0.#"));
        }
    }

    private bool showFinalStats;
    private bool showGimmicks;
    private string levelInput = "17";

    // 능력치 16종: 출처별 누적값(상점 / 젬 / 특수 젬 / 아침)과 계산 규칙을 거친 최종값
    private void DrawFinalStats()
    {
        StatCatalog cat = StatCatalog.Instance;
        GUILayout.Label("── 최종 능력치 (상점 / 젬 / 특수 젬 / 아침 → 최종) ──");
        GUILayout.Label("상점 구매 " + RunStats.ShopPurchases + "회, 다음 가격 " + cat.Price(RunStats.ShopPurchases) + " 냥냥코인");
        foreach (StatCatalog.StatDef d in cat.stats)
        {
            string sources = "";
            foreach (StatSource s in (StatSource[])System.Enum.GetValues(typeof(StatSource)))
            {
                float v = RunStats.SourceTotal(d.stat, s);
                sources += (sources.Length > 0 ? " / " : "") + (Mathf.Approximately(v, 0f) ? "0" : cat.FormatDelta(d.stat, v));
            }
            float final = RunStats.Get(d.stat);
            string result = d.direction == StatDirection.Additive ? cat.FormatDelta(d.stat, final) : "x" + final.ToString("0.###");
            GUILayout.Label(d.name + " : " + sources + " → " + result);
        }
        GUILayout.Label("최대 체력 (실제) : " + Mathf.RoundToInt(GameManager.instance != null ? GameManager.instance.maxHealth : 0f));
    }

    private static float Slider(string label, float value, float min, float max, System.Func<float, string> format, string original)
    {
        GUILayout.Label(label + " : " + format(value) + "   (원래 " + original + ")");
        return GUILayout.HorizontalSlider(value, min, max);
    }

    private static string FormatPercent(float value)
    {
        return Mathf.RoundToInt(value * 100f) + "%";
    }
}
