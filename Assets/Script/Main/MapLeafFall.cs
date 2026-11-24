using UnityEngine;

// 게임 맵에 나뭇잎·꽃잎이 바람에 살랑살랑 떨어짐
// - 게임을 시작할 때 RunConfig.mapLeafSets 중 한 종류를 무작위로 골라 그 판 내내 그 종류만 떨어짐
//   (leaf_effect 시트: 분홍 벚꽃잎 / 흰 벚꽃잎 / 초록 나뭇잎 / 노란 잎 / 꽃송이)
// - 카메라 화면 위쪽·왼쪽 바깥에서 들어와 바람 방향으로 흘러가고, 화면 밖으로 나가면 다시 들어옴 (수는 일정)
// 수치는 RunConfig의 "맵 나뭇잎" 항목
public class MapLeafFall : MonoBehaviour
{
    private class Leaf
    {
        public Transform t;
        public SpriteRenderer r;
        public Vector2 pos;
        public Vector2 vel;
        public float frame, fps;
        public float swayPhase, swaySpeed;
        public float spin;
        public bool resting;                                 // 밤: 화면 밖으로 나간 뒤 새로 들어오지 않고 쉬는 중
        public float wakeAt;                                 // 아침: 이 시각에 다시 들어옴 (한꺼번에 쏟아지지 않게)
    }

    private static MapLeafFall instance;
    private RunConfig rc;
    private Sprite[] frames;
    private Leaf[] leaves;

    public static string CurrentSet { get; private set; }

    // 새 판 시작 (GameManager.GameStart)
    public static void Begin()
    {
        if (instance != null) Destroy(instance.gameObject);
        RunConfig rc = RunConfig.Instance;
        if (rc == null || rc.mapLeafSets == null || rc.mapLeafSets.Length == 0) return;

        // 프레임이 있는 종류 중에서 무작위
        int tries = 0, pick;
        do
        {
            pick = Random.Range(0, rc.mapLeafSets.Length);
        } while ((rc.mapLeafSets[pick].frames == null || rc.mapLeafSets[pick].frames.Length == 0) && ++tries < 20);
        RunConfig.LeafSet set = rc.mapLeafSets[pick];
        if (set.frames == null || set.frames.Length == 0) return;

        instance = new GameObject("Map Leaf Fall").AddComponent<MapLeafFall>();
        instance.rc = rc;
        instance.frames = set.frames;
        CurrentSet = set.name;
        instance.Build();
    }

    private void Build()
    {
        leaves = new Leaf[Mathf.Max(0, rc.mapLeafCount)];
        for (int i = 0; i < leaves.Length; i++)
        {
            Leaf leaf = new Leaf();
            leaf.r = new GameObject("Leaf").AddComponent<SpriteRenderer>();
            leaf.t = leaf.r.transform;
            leaf.t.SetParent(transform, false);
            leaf.r.sortingLayerName = rc.mapLeafSortingLayer;
            leaf.r.sortingOrder = rc.mapLeafSortingOrder;
            leaves[i] = leaf;
            Respawn(leaf, true);
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // 카메라 화면 범위 (월드)
    private static Rect View(float margin)
    {
        Camera cam = Camera.main;
        if (cam == null) return new Rect(-10f, -6f, 20f, 12f);
        float h = cam.orthographicSize, w = h * cam.aspect;
        Vector2 c = cam.transform.position;
        return Rect.MinMaxRect(c.x - w - margin, c.y - h - margin, c.x + w + margin, c.y + h + margin);
    }

    private void Respawn(Leaf leaf, bool anywhere)
    {
        float size = Random.Range(rc.mapLeafSize.x, rc.mapLeafSize.y);
        Sprite first = frames[0];
        float spriteW = first != null ? Mathf.Max(0.01f, first.bounds.size.x) : 1f;
        leaf.t.localScale = Vector3.one * (size / spriteW);
        leaf.vel = rc.mapLeafWind * Random.Range(0.7f, 1.3f);
        Rect v = View(size);
        if (anywhere)
        {
            leaf.pos = new Vector2(Random.Range(v.xMin, v.xMax), Random.Range(v.yMin, v.yMax));   // 처음에는 화면 곳곳에
        }
        else
        {
            // 바람이 불어오는 쪽(위·왼쪽) 화면 바깥에서 들어옴
            bool fromSide = Random.value < Mathf.Abs(rc.mapLeafWind.x) / (Mathf.Abs(rc.mapLeafWind.x) + Mathf.Abs(rc.mapLeafWind.y) + 0.001f);
            float sideX = rc.mapLeafWind.x >= 0f ? v.xMin : v.xMax;
            float sideY = rc.mapLeafWind.y <= 0f ? v.yMax : v.yMin;
            leaf.pos = fromSide ? new Vector2(sideX, Random.Range(v.yMin, v.yMax)) : new Vector2(Random.Range(v.xMin, v.xMax), sideY);
        }
        leaf.fps = Random.Range(rc.mapLeafFps * 0.7f, rc.mapLeafFps * 1.3f);
        leaf.frame = Random.Range(0f, frames.Length);
        leaf.swayPhase = Random.Range(0f, 6.28f);
        leaf.swaySpeed = Random.Range(1.1f, 2f);
        leaf.spin = Random.Range(-1f, 1f);
        leaf.r.color = new Color(1f, 1f, 1f, rc.mapLeafAlpha);
    }

    private void LateUpdate()
    {
        if (leaves == null) return;
        float dt = Time.deltaTime;                             // 레벨업 등으로 멈추면 같이 멈춤
        Rect v = View(1.5f);
        bool night = DayNightCycle.IsNight;                    // 밤: 떠 있는 잎은 그대로 흘러가고 새 잎은 들어오지 않음, 아침이 되면 다시
        foreach (Leaf leaf in leaves)
        {
            if (leaf.resting)
            {
                if (night) continue;
                if (leaf.wakeAt <= 0f) leaf.wakeAt = Time.time + Random.Range(0f, rc.mapLeafResumeSpread);
                if (Time.time < leaf.wakeAt) continue;
                leaf.resting = false;
                leaf.wakeAt = 0f;
                leaf.r.enabled = true;
                Respawn(leaf, false);
            }
            leaf.swayPhase += leaf.swaySpeed * dt;
            // 바람 방향 + 좌우로 살랑살랑
            Vector2 sway = new Vector2(Mathf.Sin(leaf.swayPhase) * rc.mapLeafSway, Mathf.Cos(leaf.swayPhase * 0.8f) * rc.mapLeafSway * 0.35f);
            leaf.pos += (leaf.vel + sway) * dt;
            leaf.frame += leaf.fps * dt;
            leaf.r.sprite = frames[(int)leaf.frame % frames.Length];
            leaf.t.position = new Vector3(leaf.pos.x, leaf.pos.y, 0f);
            leaf.t.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(leaf.swayPhase * 0.7f) * 25f + leaf.spin * 10f);
            if (!v.Contains(leaf.pos))
            {
                if (night)
                {
                    leaf.resting = true;                       // 밤에는 다시 들어오지 않음
                    leaf.r.enabled = false;
                }
                else Respawn(leaf, false);                     // 화면 밖으로 나가면(카메라가 움직여도) 다시 들어옴
            }
        }
    }
}
