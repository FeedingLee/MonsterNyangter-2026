using UnityEngine;

// 상점 NPC (Shopper): 낮 동안 텐트 입구 옆에 서서 상점 역할을 대신함
// - 낮이 되면 달려와 등장, 밤이 되면 달려 나가며 퇴장
// - 플레이어가 가까이 오면 손을 흔들어 인사, 멀어지면 다시 가만히 서 있음
// 그림은 RunConfig의 shopper 프레임 (Shopper.png: 대기 / 인사 / 등장 / 퇴장)
public class ShopKeeper : MonoBehaviour
{
    public static ShopKeeper Instance { get; private set; }

    private enum State { Away, Entering, Idle, Greeting, Leaving }

    private RunConfig rc;
    private SpriteRenderer sr;
    private Vector3 home;                                       // 서 있는 자리
    private State state = State.Away;
    private float frame;
    private bool greeted;                                       // 이번에 다가왔을 때 이미 인사했는지

    // 상점 이용 판정·표지판 위치 (보이는 동안만)
    public bool Present => state == State.Idle || state == State.Greeting || state == State.Entering;
    public Bounds Bounds => sr.bounds;

    public static void Create(Vector3 position)
    {
        if (Instance != null) Destroy(Instance.gameObject);
        GameObject go = new GameObject("Shopper");
        ShopKeeper k = go.AddComponent<ShopKeeper>();
        k.home = position;
        go.transform.position = position;
        k.Build();
    }

    private void Build()
    {
        Instance = this;
        rc = RunConfig.Instance;
        sr = gameObject.AddComponent<SpriteRenderer>();
        SpriteRenderer playerSprite = GameManager.instance.player.GetComponent<SpriteRenderer>();
        FitToPlayer();
        sr.sharedMaterial = playerSprite.sharedMaterial;
        sr.sortingLayerID = playerSprite.sortingLayerID;
        sr.sortingOrder = playerSprite.sortingOrder - 1;
        sr.sprite = First(rc.shopperIdleFrames);
        sr.enabled = false;
        // 상인 그림에 그림자가 이미 그려져 있어 따로 발밑 그림자를 붙이지 않음 (겹쳐 보이던 문제)
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 플레이어 캐릭터와 같은 키 (그림 여백 비율이 비슷하므로 스프라이트 높이로 맞춤). 게임 중 캐릭터 그림 기준
    private void FitToPlayer()
    {
        // 게임 시작 직전에는 캐릭터 그림이 대기실용일 수 있어, 게임 중 캐릭터 그림 높이(설정값)를 기준으로
        Sprite idle = First(rc.shopperIdleFrames);
        float playerHeight = rc.shopperPlayerHeight;
        float scale = idle != null ? playerHeight / Mathf.Max(0.01f, idle.bounds.size.y) : 1f;
        transform.localScale = Vector3.one * scale * rc.shopperHeightRatio;
    }

    private static Sprite First(Sprite[] frames) => frames != null && frames.Length > 0 ? frames[0] : null;

    private void SetShadow(bool on)
    {
        Transform s = transform.Find("Ground Shadow");
        if (s != null) s.gameObject.SetActive(on);
    }

    private void Update()
    {
        GameManager gm = GameManager.instance;
        if (gm == null) return;
        bool day = DayNightCycle.ShopAvailable;
        float dt = Time.deltaTime;

        switch (state)
        {
            case State.Away:
                if (day)
                {
                    FitToPlayer();                               // 캐릭터 그림이 바뀌어도 같은 키로
                    Begin(State.Entering);
                }
                break;

            case State.Entering:
            {
                // 왼쪽에서 달려와 자리에 섬
                Sprite[] f = rc.shopperEnterFrames;
                float dur = Length(f);
                float k = Mathf.Clamp01(frame / Mathf.Max(0.01f, dur));
                transform.position = home + Vector3.left * rc.shopperRunDistance * (1f - EaseOut(k));
                if (Play(f, false, dt)) Begin(State.Idle);
                if (!day) Begin(State.Leaving);
                break;
            }

            case State.Idle:
            case State.Greeting:
            {
                transform.position = home;
                if (!day) { Begin(State.Leaving); break; }
                float d = Vector2.Distance(gm.player.transform.position, home);
                // 가까이 오면 한 번 인사, 멀어졌다가 다시 오면 또 한 번 (경계에서 깜빡이지 않게 여유를 둠)
                if (d > rc.shopperGreetRadius + 0.6f) greeted = false;
                if (state == State.Idle && !greeted && d <= rc.shopperGreetRadius)
                {
                    greeted = true;
                    Begin(State.Greeting);
                }
                if (state == State.Greeting)
                {
                    // 손 흔들기를 shopperGreetLoops번 반복한 뒤 가만히 섬
                    Play(rc.shopperGreetFrames, true, dt);
                    if (frame >= Length(rc.shopperGreetFrames) * Mathf.Max(1, rc.shopperGreetLoops)) Begin(State.Idle);
                }
                else
                {
                    Play(rc.shopperIdleFrames, true, dt);
                }
                break;
            }

            case State.Leaving:
            {
                // 돌아서서 오른쪽으로 달려 나가며 사라짐
                Sprite[] f = rc.shopperLeaveFrames;
                float dur = Length(f);
                float k = Mathf.Clamp01(frame / Mathf.Max(0.01f, dur));
                float run = Mathf.Clamp01((k - 0.45f) / 0.55f);          // 앞쪽 프레임은 제자리에서 돌아섬
                transform.position = home + Vector3.right * rc.shopperRunDistance * run * run;
                sr.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((k - 0.75f) / 0.25f));
                if (Play(f, false, dt))
                {
                    state = State.Away;
                    sr.enabled = false;
                    SetShadow(false);
                }
                break;
            }
        }

        // 플레이어보다 아래(앞)에 있으면 앞에, 위(뒤)에 있으면 뒤에 그림
        SpriteRenderer playerSprite = gm.player.GetComponent<SpriteRenderer>();
        sr.sortingOrder = playerSprite.sortingOrder + (transform.position.y < gm.player.transform.position.y ? 1 : -1);
    }

    private void Begin(State next)
    {
        state = next;
        frame = 0f;
        sr.enabled = true;
        sr.color = Color.white;
        SetShadow(true);
    }

    private float Length(Sprite[] f) => (f != null ? f.Length : 1) / Mathf.Max(1f, rc.shopperFps);

    // 프레임 재생. loop가 아니면 끝났을 때 true
    private bool Play(Sprite[] frames, bool loop, float dt)
    {
        if (frames == null || frames.Length == 0) return true;
        frame += dt;
        int i = Mathf.FloorToInt(frame * rc.shopperFps);
        if (loop) i %= frames.Length;
        else if (i >= frames.Length) { sr.sprite = frames[frames.Length - 1]; return true; }
        sr.sprite = frames[i];
        return false;
    }

    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
}
