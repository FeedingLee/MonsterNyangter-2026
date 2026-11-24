using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    [Tooltip("화면(메인 카메라) 테두리 바깥 소환 거리 최소~최대 (이 사이 무작위)")]
    public Vector2 offscreenMargin = new Vector2(2.25f, 7.5f);
    public SpawnData[] spawnData;
    public BossSpawnData[] bossSpawnData;
    public GameManager GM;
    public float levelTime;
    public Color StrongMonsterColor;

    int level;
    float timer;

    private void Awake()
    {
        levelTime = GameManager.instance.maxGameTime / spawnData.Length;
    }

    private void Update()
    {
        if (!GameManager.instance.isLive)
            return;
        if (DayNightCycle.DaySpawnPaused)                    // 귀환 준비 ~ 밤: 낮 몬스터 새로 소환하지 않음 (밤은 NightDefense가 경로로 소환)
            return;

        // 타이머의 시간에 맞춰 소환
        timer += Time.deltaTime;
        level = CurrentLevel;                                // 일차 기준 (경과 시간 기준이면 2일 차 낮 중간에 3일 차 몬스터가 나옴)

        // 스폰데이터의 레벨에 따라 소환
        // 스폰 속도 배율 (몰이사냥 등)
        if (timer > spawnData[level].spawnTime / RunStats.Get(RunStat.SpawnRate))
        {
            timer = 0;
            Spawn();
            // 보스 스폰시 잡몹 소환 X
            /*if (GM.isBossSpawn == false)
            {           
                Spawn();
            }*/
        }
    }
    private void Spawn()
    {
        GameObject enemy = GameManager.instance.pool.Get(0);
        // 고정 소환 지점 대신 화면 밖 360도 무작위 (OffscreenSpawn)
        float margin = Random.Range(offscreenMargin.x, Mathf.Max(offscreenMargin.x, offscreenMargin.y));
        if (CampLoiterPenalty.Punished)
        {
            Vector2 m = RunConfig.Instance.penaltySpawnMargin;   // 캠프 비비기 처벌: 화면 바로 밖에서 소환
            margin = Random.Range(m.x, Mathf.Max(m.x, m.y));
        }
        enemy.transform.position = OffscreenSpawn.RandomPoint(GameManager.instance.player.transform.position, margin);

        int randomIndex = Random.Range(0, 100);

        // chance : 몬스터의 체력이 2배가 될 확률
        // 엘리트 등장 확률 배율 (엘리트 토벌, 엘리트의 행진)
        if (spawnData[level].chance * RunStats.Get(RunStat.EliteChance) >= randomIndex)
        {
            // 원래 체력 임시 저장
            int tmp = spawnData[level].health;

            // 체력을 임의의 배율만큼 증가
            float newHp = spawnData[level].health * spawnData[level].stronger;

            // 체력을 2배로
            spawnData[level].health = (int)newHp;

            // 강화 몬스터 표시는 색 대신 발밑 오라 (Enemy.Init 에서 IsElite 로 처리)

            // 몬스터 스탯 초기화
            enemy.GetComponent<Enemy>().Init(spawnData[level], spawnData[level].expIndex);

            // 체력 변수를 원래대로
            spawnData[level].health = tmp;
        }
        else
        {
            enemy.GetComponent<Enemy>().Init(spawnData[level], 1);
        }
        CampLoiterPenalty.ApplyToSpawn(enemy.GetComponent<Enemy>());   // 캠프 비비기 처벌 스택만큼 체력 증가

    }

    // 지금 낮 스폰 단계의 데이터 (군락: 둥지 체력·호위 종류)
    public SpawnData CurrentData => spawnData[CurrentLevel];

    // 스폰 표 단계 = 일차 - 1 (1일 차 Mosswine, 2일 차 Kestodon, 3일 차 Vespoid, 4일 차 Hercudrome, 5일 차 Jagras: 밤 웨이브 표와 같은 기준)
    public int CurrentLevel
    {
        get
        {
            DayNightCycle dn = DayNightCycle.Instance;
            if (dn == null)                                  // 낮/밤이 없으면 예전처럼 경과 시간 기준
                return Mathf.Clamp(Mathf.FloorToInt(GameManager.instance.gameTime / Mathf.Max(0.01f, levelTime)), 0, spawnData.Length - 1);
            return Mathf.Clamp(dn.DayCount - 1, 0, spawnData.Length - 1);
        }
    }

    // 지정 위치에 그날 낮 일반 몬스터 하나 (엘리트 확률은 Spawn과 같은 규칙, 군락 호위용)
    public Enemy SpawnDayMonsterAt(Vector2 position)
    {
        GameObject go = GameManager.instance.pool.Get(0);
        go.transform.position = position;
        Enemy enemy = go.GetComponent<Enemy>();
        SpawnData d = CurrentData;
        if (d.chance * RunStats.Get(RunStat.EliteChance) >= Random.Range(0, 100))
        {
            int tmp = d.health;
            d.health = (int)(d.health * d.stronger);
            enemy.Init(d, d.expIndex);
            d.health = tmp;
        }
        else
        {
            enemy.Init(d, 1);
        }
        return enemy;
    }

    public void BossSpawn()
    {
        GameObject Boss = GameManager.instance.pool.GetEnemy(1);
    }
}

    //소환데이터 설정 클래스 [ 스프라이트 타입, 소환시간, 체력 등 ]
    [System.Serializable]
    public class SpawnData
    {   
        public float spawnTime;
        public int spriteType;
        public int health;
        public float speed;
        public int chance;
        public float stronger;                  
        public int expIndex;                    // 추가 경험치 구슬 갯수
    }   

    [System.Serializable]
    public class BossSpawnData
    {
        public float speed;
        public float health;
    }