using UnityEngine;

// 맵 환경 장식 설정 (Resources/MapDecorConfig). 분류별 스프라이트와 수치를 인스펙터에서 조정
// 스프라이트는 Forest_object 시트에서 잘라 둔 것 (Forest_object_0 ~ 60)
[CreateAssetMenu(fileName = "MapDecorConfig", menuName = "MonNyang/Map Decor Config")]
public class MapDecorConfig : ScriptableObject
{
    private static MapDecorConfig instance;

    public static MapDecorConfig Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<MapDecorConfig>("MapDecorConfig");
            }
            return instance;
        }
    }

    [Header("# 키 큰 장식 (플레이어·몬스터보다 위에 그림, 드물게 하나씩)")]
    public Sprite[] tree;            // 큰 나무
    public Sprite[] rock;            // 바위
    public Sprite[] log;             // 쓰러진 통나무
    public Sprite[] bush;            // 키 큰 풀·고사리

    [Header("# 바닥 장식 (캐릭터·몬스터·보스가 위로 지나감)")]
    public Sprite[] smallPlant;      // 풀·꽃·큰 잎 풀·클로버 (군락)
    public Sprite[] mushroom;        // 버섯 (풀 군락 사이에 가끔)
    public Sprite[] bones;           // 뼈·해골
    public Sprite[] randomScatter;   // 낙엽·작은 돌 (흩뿌림)

    [Header("# 개수 (매 판 무작위 위치)")]
    [Min(0)] public int treeCount = 3;           // 큰 나무: 가끔 하나씩만
    [Min(0)] public int rockCount = 3;           // 바위: 가끔 하나씩만
    [Min(0)] public int logCount = 2;            // 통나무
    [Min(0)] public int looseBushCount = 8;      // 키 큰 풀·고사리
    [Min(0)] public int mushroomCount = 8;
    [Min(0)] public int boneSpotCount = 2;       // 뼈 자리
    [Min(0)] public int smallPlantCount = 120;   // 풀·꽃 (군락 단위로 나눠 배치)
    [Min(0)] public int tinyCount = 40;          // 낙엽·작은 돌
    [Min(1)] public int maxObjects = 400;
    [Min(1f)] public float lonelySpacing = 12f;  // 나무·바위·통나무끼리 최소 거리 (하나씩 떨어져 있게)

    [Header("# 배치")]
    public Vector2Int clusterSize = new Vector2Int(3, 6);    // 풀·꽃 군락 하나의 개수
    [Min(0.1f)] public float clusterRadius = 1.2f;            // 군락 반경
    [Min(0f)] public float spawnClearRadius = 3f;             // 시작 위치 주변 비우기
    public string campObjectName = "Center";                 // 가운데 캠프 오브젝트 이름 (이 주변은 식생을 놓지 않음)
    [Min(0f)] public float campClearRadius = 10f;             // 캠프 그림 영역 가운데에서 이 반경 안은 비우기
    public Vector2 scaleRange = new Vector2(0.9f, 1.1f);
    [Min(0.001f)] public float noiseScale = 0.1f;             // Perlin 노이즈 크기 (작을수록 큰 숲 덩어리)
    [Range(0f, 1f)] public float denseThreshold = 0.45f;      // 노이즈가 이보다 높은 곳이 숲이 짙은 구역
    [Min(0f)] public float wallMargin = 0.6f;                 // 벽 안쪽 여유

    [Header("# 분류별 최소 간격")]
    [Min(0f)] public float treeSpacing = 3.2f;
    [Min(0f)] public float bushSpacing = 1.6f;
    [Min(0f)] public float propSpacing = 1.3f;
    [Min(0f)] public float smallPlantSpacing = 0.6f;
    [Min(0f)] public float tinySpacing = 0.6f;

    [Header("# 분류별 크기 배율 (scaleRange에 곱함)")]
    [Min(0.1f)] public float treeScale = 2.6f;
    [Min(0.1f)] public float bushScale = 1.8f;
    [Min(0.1f)] public float smallPlantScale = 1.5f;
    [Min(0.1f)] public float propScale = 1.6f;
    [Min(0.1f)] public float tinyScale = 1.4f;

    [Header("# 그리는 순서")]
    public string decorLayer = "Tree 3";         // 키 큰 장식: 플레이어·몬스터보다 위 (기존 씬 나무와 같은 레이어)
    public int decorOrder = 0;                   // 이 값부터 아래(화면 아래쪽)에 있는 장식이 앞에 그려짐
    public string groundLayer = "ForeGround";    // 바닥 장식: 몬스터 레이어보다 아래, 경험치 구슬(0)보다 아래
    public int groundOrder = -1;
}
