using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public JoystickController joystickController;

    [Header("# Game Control")]
    public bool isLive;
    // 실제 흐르는 게임 시간
    public float gameTime;
    // 최대 게임 시간 
    public float maxGameTime = 2 * 10f;
    // 보스 스폰시 추가 생성을 막기위한 변수
    public bool isBossSpawn = false;
    // 경험치 보너스 코루틴 확인용 변수
    public static int expbonuscheck = 0;
    // 슈퍼 그물망 코루틴 확인용 변수
    public static int supermagnetcheck = 0;
    // 보와보와 업적용 변수
    public int wincheck = 0; 
    private IEnumerator expcoroutine;
    private IEnumerator magnetcoroutine;

    [Header("# Player Info")]
    public int playerId;
    public int weaponcode;
    public float health;
    public float maxHealth = 100;
    public int level;
    public int kill;
    public int exp;
    public int[] nextExp = {};

    [Header("# Damage Number")]
    public DamageNumberSettings damageNumber = new DamageNumberSettings();

    [Header("# Aim Laser")]
    public AimLaserSettings aimLaser = new AimLaserSettings();

    [Header("# Great Sword")]
    [Min(1f)] public float greatSwordReturnSpeed = 1080f;   // 회전 후 조작 방향으로 돌아가는 속도(도/초)
    [Min(0f)] public float lanceRehitInterval = 0.4f;       // 보조무기 랜스: 같은 적을 다시 때릴 수 있는 간격(초, 좌우 연타 누적 방지)

    [Header("# Main Weapon")]
    public MainWeaponSettings mainWeapon = new MainWeaponSettings();

    [Header("# GameObject")]
    public PoolManager pool;
    public Player player;
    public LevelUp uiLevelUp;
    public Result uiResult;
    public GameObject uiJoyStick;
    public GameObject enemyCleaner;
    public BossPattern boss;

    private void Awake()
    {
        expcoroutine = CouponTime();
        magnetcoroutine = SuperMagnetTime();

        instance = this;
        Application.targetFrameRate = 60;
        DamageNumber.Suppressed = false;   // 이전 판에서 클리어 후 꺼둔 데미지 숫자 표시를 다시 켬

        // 낮/밤, 코인, 상점, 아침 선택지 진행 (씬 배치 없이 실행 중에 추가)
        gameObject.AddComponent<DayNightCycle>();
    }

    private void Update()
    {
        if (expbonuscheck == 0 && Item.Exp_Bonus == 1)
        {
            Exp_Fever();
        }       
        
        if (supermagnetcheck == 0 && Item.Super_Magnet == 1)
        {
            Magnet_Fever();
        }   

        if (!isLive)
            return;

        if (!DayNightCycle.TimerPaused)                      // 귀환 준비 동안은 전체 타이머 정지
        {
            gameTime += Time.deltaTime;
        }

        if (gameTime > maxGameTime && !isBossSpawn)
        {
            isBossSpawn = true;
            gameTime = maxGameTime;
            // 보스 스폰
            player.GetComponentInChildren<Spawner>().BossSpawn();
            //GameVictory();
        }
    }

    public void GameStart(int id)
    {
        playerId = id;
        health = maxHealth;
        WeaponSynergy.ResetRun();                            // 시너지 단계 초기화
        MapDecorGenerator.NewRun();                          // 맵 식생·바닥 장식을 새 시드로 배치
        MiniBossDirector.Begin();                            // 2분마다 중간보스 단계
        MapLeafFall.Begin();                                 // 이번 판에 흩날릴 나뭇잎 종류를 무작위로

        switch (playerId)
        {
            case 0:
                weaponcode = 0;
                break;
            case 1:
                weaponcode = 0;
                break;
            case 2:
                weaponcode = 0;
                break;
            case 3:
                weaponcode = 2;
                break;
        }

        player.gameObject.SetActive(true);
        uiJoyStick.SetActive(true);
        if (weaponcode == 0 && RunConfig.Instance.woodStickSprite != null)
        {
            ItemData dual = null;
            foreach (Item item in uiLevelUp.Items)
            {
                if (item.data.itemType == ItemData.ItemType.DualBlades) dual = item.data;
            }
            Weapon.CreateStarter(dual, RunConfig.Instance.woodStickSprite);   // 쌍검 1레벨 대신 나무 막대기
        }
        else
        {
            uiLevelUp.Select(weaponcode);
        }
        Resume();

        AudioManager.instance.PlayBgm(true);
        RunUI.ClickSound();   // 게임 시작 소리 (기존 선택음은 옵션/상점 전용)
    }
    
    public void GameOver()
    {
        StartCoroutine(GameOverRoutine());
    }

    private IEnumerator GameOverRoutine()
    {
        // 획득했던 스텟 값 초기화
        Gear.rate_stat = 0.0f;
        Gear.damage_stat = 0.0f;
        Gear.stamina_stat = 0.0f;
        Gear.upgrade_stat = 0;
        // 무기 레벨 초기화
        Weapon.DB_level = 0;
        Weapon.HB_level = 0;
        Weapon.SH_level = 0;
        Weapon.GS_level = 0;
        Weapon.LC_level = 0;
        // 경험치 보너스 상태 초기화
        Item.Exp_Bonus = 0;
        expbonuscheck = 0;
        // 자석 보너스 상태 초기화
        Item.Super_Magnet = 0;
        supermagnetcheck = 0;

        isLive = false;

        yield return new WaitForSeconds(1f);

        uiResult.gameObject.SetActive(true);
        uiResult.Lose();
        GameResultView.Show(false);                          // 젬 보상 + 새 결과 화면 (기존 결과 화면은 보이지 않게)
        Stop();

        AudioManager.instance.PlayBgm(false);
        AudioManager.instance.PlaySfx(AudioManager.Sfx.Game_Lose);
    }

    public void GameVictory()
    {
        StartCoroutine(GameVictoryRoutine());
        wincheck = 1;
        DamageNumber.HideAll();
        DamageNumber.Suppressed = true;    // 클리어 후 남은 몬스터 정리 피해는 숫자로 표시하지 않음
    }

    private IEnumerator GameVictoryRoutine()
    {
        // 이전 게임에서 얻었던 값 초기화
        Gear.rate_stat = 0.0f;
        Gear.damage_stat = 0.0f;
        Gear.stamina_stat = 0.0f;
        Gear.upgrade_stat = 0;
        // 무기 레벨 초기화
        Weapon.DB_level = 0;
        Weapon.HB_level = 0;
        Weapon.SH_level = 0;
        Weapon.GS_level = 0;
        Weapon.LC_level = 0;
        // 경험치 보너스 상태 초기화
        Item.Exp_Bonus = 0;
        expbonuscheck = 0;
        // 자석 보너스 상태 초기화
        Item.Super_Magnet = 0;
        supermagnetcheck = 0;

        isLive = false;
        enemyCleaner.SetActive(true);

        yield return new WaitForSeconds(0.8f);

        uiResult.gameObject.SetActive(true);
        uiResult.Win();
        GameResultView.Show(true);
        Stop();

        AudioManager.instance.PlayBgm(false);
        AudioManager.instance.PlaySfx(AudioManager.Sfx.Game_Win);
    }

    public void GameRetry()
    {
        SceneManager.LoadScene(1);
    }

    public void GameQuit()
    {
        Application.Quit();
    }
 
    public void Stop()
    {
        isLive = false;
        Time.timeScale = 0;
        uiJoyStick.SetActive(false);
    }

    public void Resume()
    {
        isLive = true;
        Time.timeScale = 1;
        uiJoyStick.SetActive(true);

        // 게임 Resume 시 조이스틱 초기화
        JoystickController joystickHandle = uiJoyStick.GetComponent<JoystickController>();
        if (joystickHandle != null)
        {
            joystickHandle.ResetJoystick();
        }
    }

    public void GetExp()
    {
        if (!isLive)
            return;

        // 경험치 획득량 배율 (몰이사냥 1.5배 등). 소수점은 모아 두었다가 1이 되면 반영
        expBank += RunStats.Get(RunStat.ExpGain);

        while (expBank >= 1f)
        {
            expBank -= 1f;
            if (AddOneExp())
                break;                      // 레벨업 창이 열렸으면 남은 경험치는 다음 획득 때 반영
        }
    }

    private float expBank;

    // 경험치 1 획득. 레벨업했으면 true
    private bool AddOneExp()
    {
        int need = nextExp[Mathf.Min(level, nextExp.Length - 1)];

        // 테스트 모드 '레벨업 불가'가 켜져 있으면 경험치는 레벨업 직전까지만 쌓임
        if (TestMode.NoLevelUp)
        {
            exp = Mathf.Min(exp + 1, Mathf.Max(0, need - 1));
            return false;
        }

        exp++;

        if (exp >= need)
        {
            level++;
            exp = 0;
            SpriteFx.LevelUp(player.transform);              // 금빛 기둥 + 머리 위 반짝임
            uiLevelUp.Show();
            return true;
        }
        return false;
    }

    // 체력 회복 (회복 배율 적용: 단식 수행이면 0, 배수진이면 절반)
    public void Heal(float amount)
    {
        if (health <= 0f || amount <= 0f)
            return;

        health = Mathf.Min(maxHealth, health + amount * RunStats.Get(RunStat.HealScale));
    }

    /* 경험치 보너스(격운티켓) 관련 */
    public void Exp_Fever()
    {        
        StopCoroutine(expcoroutine);
        expbonuscheck = 1;

        expcoroutine = CouponTime();
        StartCoroutine(expcoroutine);
    }
    // 능력치 창에 남은 시간을 보여 주기 위한 종료 시각
    public static float ExpBonusEndTime { get; private set; }
    public static float SuperMagnetEndTime { get; private set; }

    private IEnumerator CouponTime()
    {
        ExpBonusEndTime = Time.time + 30.0f;
        yield return new WaitForSeconds(30.0f);
        Item.Exp_Bonus = 0;
        expbonuscheck = 0;
        StopCoroutine(expcoroutine);
    }

    /* 자력 강화(슈퍼 그물망) 관련 */
    public void Magnet_Fever()
    {
        StopCoroutine(magnetcoroutine);
        supermagnetcheck = 1;

        magnetcoroutine = SuperMagnetTime();
        StartCoroutine(magnetcoroutine);
    }
    private IEnumerator SuperMagnetTime()
    {
        SuperMagnetEndTime = Time.time + 10.0f;
        yield return new WaitForSeconds(10.0f);
        Item.Super_Magnet = 0;
        supermagnetcheck = 0;
        StopCoroutine(magnetcoroutine);
    }
}