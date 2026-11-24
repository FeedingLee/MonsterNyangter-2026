using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 필살기 공통 설정
[System.Serializable]
public class UltimateBase
{
    [Tooltip("GameManager.GameStart에 전달되는 캐릭터 번호")]
    public int playerId;
    public Sprite icon;
    [Tooltip("사용 횟수. 0보다 작으면 무한")]
    public int uses = 3;
    public AudioClip useSound;
}

// 월드에 소환되는 오브젝트의 외형 설정
[System.Serializable]
public class WorldObjectVisual
{
    public Sprite sprite;
    public RuntimeAnimatorController animator;
    public GameObject spawnEffect;
    [Min(0.01f)] public float scale = 1f;
    public int sortingOrder = 3;
    [Min(0.01f)] public float colliderRadius = 0.5f;
}

[System.Serializable]
public class HoneyBugUltimate : UltimateBase
{
    public WorldObjectVisual visual = new WorldObjectVisual();
    [Min(0f)] public float spawnRadius = 4f;            // 플레이어 주위 설치 반경
    [Min(0f)] public float minDistanceFromPlayer = 1.5f; // 바로 먹히지 않도록 띄울 최소 거리
    [Min(0f)] public float minSpacing = 1.5f;           // 다른 꿀벌레와의 최소 거리
    [Range(0f, 100f)] public float healPercent = 30f;   // 최대 체력 대비 회복량(%)
    public AudioClip healSound;
    public GameObject pickupEffect;
}

[System.Serializable]
public class MiniDemonUltimate : UltimateBase
{
    [Min(0f)] public float duration = 8f;               // 버프 지속 시간(초)
    [Min(0f)] public float cooldown = 12f;              // 다음 사용까지의 쿨타임(초, 사용 시점부터 계산)
    [Min(0.01f)] public float statMultiplier = 1.5f;    // 이동속도, 스테미나, 쿨타임 배율
    public bool includeDamage = false;                  // 공격력에도 배율 적용
    public GameObject effect;
    public Color playerTint = Color.white;
}

[System.Serializable]
public class DecoyUltimate : UltimateBase
{
    public WorldObjectVisual visual = new WorldObjectVisual();
    [Min(0f)] public float duration = 6f;               // 미끼 지속 시간(초)
    [Min(0f)] public float cooldown = 10f;              // 다음 소환까지의 쿨타임(초, 소환 시점부터 계산)
    [Min(0f)] public float distance = 3f;               // 플레이어 앞 소환 거리
    [Range(0f, 1f)] public float bossRatio = 0.3f;      // 보스에게 적용되는 지속시간 비율
}

[System.Serializable]
public class SniperUltimate : UltimateBase
{
    [Min(0f)] public float toggleCooldown = 3f;         // 저격모드 해제 후 다시 켜기까지의 쿨타임(초)
    [Min(0.01f)] public float damageMultiplier = 3f;    // 저격모드 공격력 배율
    public Sprite activeIcon;                           // 저격모드 중 아이콘 (비우면 기본 아이콘)
    public GameObject effect;
    public Color playerTint = Color.white;
}

// 필살기 버튼 UI와 캐릭터별 효과 관리
// Canvas 아래의 빈 오브젝트에 붙이면 버튼을 실행 시 자동으로 생성한다.
public class UltimateManager : MonoBehaviour
{
    public const string ButtonObjectName = "Ultimate Button";

    private const int SpawnAttempts = 30;

    [Header("# Airu")]
    public HoneyBugUltimate airu = new HoneyBugUltimate { playerId = 0 };

    [Header("# Gajabu")]
    public MiniDemonUltimate gajabu = new MiniDemonUltimate { playerId = 1 };

    [Header("# BowaBowa")]
    public DecoyUltimate bowabowa = new DecoyUltimate { playerId = 2 };

    [Header("# Wiggler")]
    public SniperUltimate wiggler = new SniperUltimate { playerId = 3, uses = -1 };

    [Header("# UI Layout")]
    public Vector2 offsetFromCorner = new Vector2(-280f, 170f); // 화면 오른쪽 아래 모서리 기준 위치
    [Min(1f)] public float circleSize = 120f;                   // 버튼 지름
    [Range(0.2f, 2f)] public float buttonScale = 0.7f;           // 버튼 전체 배율 (아이콘·글자 정렬을 유지한 채 함께 축소)
    public Vector2 ultCountPosition = new Vector2(0.24f, 0.24f);  // 남은 횟수 글자 중심 (버튼 중심 기준, 버튼 크기 비율)
    [Range(0.1f, 1f)] public float iconScale = 0.7f;            // 버튼 대비 아이콘 크기
    [Min(1)] public int labelFontSize = 28;                     // 남은 횟수 글자 크기
    [Min(0)] public int ultCountFontSize = 0;                   // 버튼 모서리 남은 횟수 글자 크기 (0이면 자동: 버튼 크기 기준, 이전의 1.5배)
    public Vector2 labelOffset = new Vector2(0f, -88f);         // 버튼 중앙 기준 글자 위치
    [Min(0f)] public float labelOutlineWidth = 0f;              // 글자 테두리 두께 (0이면 없음)
    public Color labelOutlineColor = Color.black;               // 글자 테두리 색
    public Font labelFont;                                      // 비워두면 Unity 기본 폰트 사용
    public string infiniteText = "∞";                           // 무한 횟수 표시

    [Header("# Key")]
    public KeyCode ultimateKey = KeyCode.E;                     // 필살기 사용 키 (클릭도 가능, 무기 전용 스킬은 R)
    public bool showKeyHint = true;                             // 남은 횟수 옆에 사용 키 표시

    [Header("# UI Button Shape")]
    public Sprite buttonSprite;                                 // 버튼 모양 (비우면 원)

    [Header("# UI Color")]
    public Color backgroundColor = new Color(0.1f, 0.1f, 0.15f, 0.8f);
    public Color overlayColor = new Color(0f, 0f, 0f, 0.65f);   // 사용 중, 쿨타임 표시
    public Color labelColor = Color.white;

    [Header("# Effect")]
    [Min(0f)] public float effectLifetime = 2f;                 // 일회성 이펙트 유지 시간(초)

    private RectTransform rect;
    private GameObject visualRoot;
    private Image background;
    private Image iconImage;
    private Image overlay;
    private Text labelText;
    private Outline labelOutline;
    private GameObject buffAura;     // 가쟈부 / 흔들흔들 버프 오라
    private Image countBox;          // 버튼 안 오른쪽 아래 남은 횟수 칸
    private Text countText;
    private Sprite circleSprite;

    private Player player;
    private SpriteRenderer playerSprite;
    private UltimateBase current;
    private int remainingUses;
    private float busyEndTime;
    private float busyDuration;
    private bool isSniperMode;
    private GameObject activeEffect;
    private Coroutine miniDemonCoroutine;

    private bool HasNoUsesLeft => remainingUses == 0;

    // 필살기 봉인(아침 선택지) 중에는 사용 불가
    private static bool IsSealed => RunStats.Get(RunStat.UltEnabled) < 0.5f;

    // 필살기 위력 배율 (상점 강화)
    private static float Power => RunStats.Get(RunStat.UltPower);

    public static UltimateManager Instance { get; private set; }

    // 횟수제 필살기의 사용 횟수 추가 (무한인 필살기는 변화 없음)
    public void AddUses(int count)
    {
        if (remainingUses >= 0)
        {
            remainingUses += count;
            RefreshDisplay();
        }
    }

    private void Awake()
    {
        Instance = this;
        PlayerBuff.Reset();

        rect = transform as RectTransform;
        if (rect == null || GetComponentInParent<Canvas>() == null)
        {
            Debug.LogError("UltimateManager는 Canvas 아래의 UI 오브젝트에 붙여야 합니다.", this);
            enabled = false;
            return;
        }

        circleSprite = UiCircleSprite.Create();
        BuildVisual();
        ApplyLayout();
        visualRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        PlayerBuff.Reset();
        UiCircleSprite.Release(circleSprite);
    }

    private void OnValidate()
    {
        if (Application.isPlaying && visualRoot != null)
        {
            ApplyLayout();
            RefreshDisplay();
        }
    }

    private void LateUpdate()
    {
        // 키보드로 필살기 사용
        if (current != null && Input.GetKeyDown(ultimateKey))
        {
            OnClickUltimate();
        }

        if (current == null)
        {
            TrySetup();
            return;
        }

        float remain = busyEndTime - Time.time;
        if (HasNoUsesLeft || IsSealed)
        {
            overlay.fillAmount = 1f;
        }
        else
        {
            overlay.fillAmount = remain > 0f && busyDuration > 0f ? Mathf.Clamp01(remain / busyDuration) : 0f;
        }
    }

    // 캐릭터 선택이 끝나 플레이어가 활성화되면 해당 캐릭터의 필살기를 적용
    private void TrySetup()
    {
        if (GameManager.instance == null || !GameManager.instance.player.gameObject.activeInHierarchy)
            return;

        player = GameManager.instance.player;
        playerSprite = player.GetComponent<SpriteRenderer>();
        current = FindUltimate(GameManager.instance.playerId);

        if (current == null)
        {
            Debug.LogError($"캐릭터 번호 {GameManager.instance.playerId}에 맞는 필살기 설정이 없습니다.", this);
            enabled = false;
            return;
        }

        remainingUses = current.uses;
        RefreshDisplay();
        visualRoot.SetActive(true);
    }

    private UltimateBase FindUltimate(int playerId)
    {
        UltimateBase[] ultimates = { airu, gajabu, bowabowa, wiggler };
        foreach (UltimateBase ultimate in ultimates)
        {
            if (ultimate.playerId == playerId)
            {
                return ultimate;
            }
        }
        return null;
    }

    private void OnClickUltimate()
    {
        if (current == null || !GameManager.instance.isLive || HasNoUsesLeft || IsSealed || Time.time < busyEndTime)
            return;

        if (!Activate())
            return;

        if (remainingUses > 0)
        {
            remainingUses--;
        }

        AudioManager.instance.PlayClip(current.useSound);
        if (!(current is HoneyBugUltimate))
        {
            CameraShake.Weak();                              // 필살기 사용: 약한 화면 흔들림 (아이루 꿀벌레는 제외)
        }
        RefreshDisplay();
    }

    // 필살기를 실행하고, 실제로 발동되었으면 true를 반환
    private bool Activate()
    {
        switch (current)
        {
            case HoneyBugUltimate honeyBug:
                return SpawnHoneyBug(honeyBug);
            case MiniDemonUltimate miniDemon:
                // 쿨타임이 지속시간보다 짧아 이전 버프가 남아 있으면 처음부터 다시 적용
                if (miniDemonCoroutine != null)
                {
                    StopCoroutine(miniDemonCoroutine);
                    EndPlayerEffect();
                }
                miniDemonCoroutine = StartCoroutine(MiniDemonRoutine(miniDemon));
                return true;
            case DecoyUltimate decoy:
                return SpawnDecoy(decoy);
            case SniperUltimate sniper:
                ToggleSniperMode(sniper);
                return true;
            default:
                return false;
        }
    }

    private bool SpawnHoneyBug(HoneyBugUltimate settings)
    {
        Vector2 origin = player.transform.position;
        float minDistance = Mathf.Min(settings.minDistanceFromPlayer, settings.spawnRadius);

        for (int attempt = 0; attempt < SpawnAttempts; attempt++)
        {
            Vector2 direction = Random.insideUnitCircle.normalized;
            Vector2 position = origin + direction * Random.Range(minDistance, settings.spawnRadius);

            if (HoneyBug.IsNear(position, settings.minSpacing) ||
                IsBlockedByWall(origin, position, settings.visual.colliderRadius) ||
                CampZone.Blocked(position, settings.visual.colliderRadius))   // 텐트·동굴 등 장애물 위 제외
            {
                continue;
            }

            GameObject bug = CreateWorldObject("Honey Bug", position, settings.visual, true);
            GroundShadow.Attach(bug, RunConfig.Instance.honeyBugShadowSize, RunConfig.Instance.honeyBugShadowOffsetY);   // 발밑 그림자
            SpriteFx.HoneyBugSpawn(position);                   // 반짝임과 나비가 모이며 나타남
            bug.AddComponent<HoneyBug>().Init(settings.healPercent * Power, settings.healSound, settings.pickupEffect, effectLifetime);
            return true;
        }

        return false;
    }

    private IEnumerator MiniDemonRoutine(MiniDemonUltimate settings)
    {
        SetBusy(settings.cooldown);

        // 필살기 위력: 버프 증가분을 배율만큼 키움 (1.5배 x 위력 1.2 → 1.6배)
        float multiplier = 1f + (settings.statMultiplier - 1f) * Power;
        PlayerBuff.MoveSpeed = multiplier;
        PlayerBuff.Stamina = multiplier;
        PlayerBuff.RateScale = 1f / multiplier;
        if (settings.includeDamage)
        {
            PlayerBuff.Damage = multiplier;
        }
        BeginPlayerEffect(settings.effect, settings.playerTint);
        SpriteFx.StopAura(buffAura);
        buffAura = SpriteFx.DemonAura(player.transform);    // 버프 동안 발밑 붉은 마법진 + 양옆 불꽃 (캐릭터 뒤)

        yield return new WaitForSeconds(settings.duration);

        PlayerBuff.Reset();
        EndPlayerEffect();
        SpriteFx.StopAura(buffAura);
        buffAura = null;
    }

    private bool SpawnDecoy(DecoyUltimate settings)
    {
        Vector2 origin = player.transform.position;

        // 앞에 벽이 있으면 벽에 막히지 않는 위치까지 소환 거리를 줄임
        for (float ratio = 1f; ratio > 0f; ratio -= 0.25f)
        {
            Vector2 position = origin + player.LastMoveDir * (settings.distance * ratio);

            if (IsBlockedByWall(origin, position, settings.visual.colliderRadius) ||
                CampZone.Blocked(position, settings.visual.colliderRadius))   // 텐트·동굴 등 장애물 위 제외
                continue;

            GameObject decoyObject = CreateWorldObject("Decoy", position, settings.visual, false);
            GroundShadow.Attach(decoyObject, RunConfig.Instance.decoyShadowSize, RunConfig.Instance.decoyShadowOffsetY);   // 발밑 그림자
            SpriteFx.DecoySpawn(position);                      // 흙먼지가 피어오르고 꽃잎이 흩날림
            Collider2D decoyCollider = decoyObject.GetComponent<Collider2D>();

            // 플레이어와 보스의 이동을 미끼가 막지 않도록 함
            foreach (Collider2D playerCollider in player.GetComponentsInChildren<Collider2D>(true))
            {
                Physics2D.IgnoreCollision(decoyCollider, playerCollider, true);
            }
            if (BossPattern.Current != null)
            {
                BossPattern.Current.IgnoreCollisionWith(decoyCollider);
            }

            decoyObject.AddComponent<Decoy>().Init(settings.duration * Power, settings.bossRatio);   // 필살기 위력만큼 오래 유지
            SetBusy(settings.cooldown);
            return true;
        }

        return false;
    }

    private void ToggleSniperMode(SniperUltimate settings)
    {
        isSniperMode = !isSniperMode;
        PlayerBuff.Damage = isSniperMode ? settings.damageMultiplier * Power : 1f;
        player.SetRooted(isSniperMode);

        if (isSniperMode)
        {
            BeginPlayerEffect(settings.effect, settings.playerTint);
            SpriteFx.StopAura(buffAura);
            buffAura = SpriteFx.SniperAura(player.transform);   // 저격 모드 동안 발밑 보라 마법진
        }
        else
        {
            EndPlayerEffect();
            SpriteFx.StopAura(buffAura);
            buffAura = null;
            SetBusy(settings.toggleCooldown);   // 저격모드는 바로 해제할 수 있고, 해제한 순간부터 쿨타임 적용
        }
    }

    private void SetBusy(float duration)
    {
        duration *= RunStats.Get(RunStat.UltCooldown);   // 필살기 쿨타임 배율 (상점 강화, 필살기 봉인 보상)
        busyDuration = duration;
        busyEndTime = Time.time + duration;
    }

    private void BeginPlayerEffect(GameObject effect, Color tint)
    {
        // 캐릭터 색은 바꾸지 않음 (버프는 오라 이펙트로 표현)

        if (effect != null)
        {
            activeEffect = Instantiate(effect, player.transform);
        }
    }

    private void EndPlayerEffect()
    {
        playerSprite.color = Color.white;

        if (activeEffect != null)
        {
            Destroy(activeEffect);
        }
    }

    // 지정한 위치가 울타리와 겹치거나, 플레이어와의 사이에 울타리가 있는지 확인
    private static bool IsBlockedByWall(Vector2 origin, Vector2 position, float radius)
    {
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(position, radius))
        {
            if (hit.CompareTag("Wall"))
                return true;
        }

        foreach (RaycastHit2D hit in Physics2D.LinecastAll(origin, position))
        {
            if (hit.collider.CompareTag("Wall"))
                return true;
        }

        return false;
    }

    private GameObject CreateWorldObject(string objectName, Vector2 position, WorldObjectVisual visual, bool isTrigger)
    {
        if (visual.sprite == null)
        {
            Debug.LogWarning($"{objectName}의 Sprite가 비어 있어 보이지 않습니다.", this);
        }

        GameObject worldObject = new GameObject(objectName);
        worldObject.transform.position = position;
        worldObject.transform.localScale = Vector3.one * visual.scale;

        SpriteRenderer spriteRenderer = worldObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = visual.sprite;

        // 코드로 만든 렌더러는 Default 레이어에 놓여 땅 뒤로 숨을 수 있으므로, 화면에 보이는 플레이어의 렌더링 설정을 따름
        spriteRenderer.sharedMaterial = playerSprite.sharedMaterial;
        spriteRenderer.sortingLayerID = playerSprite.sortingLayerID;
        spriteRenderer.sortingOrder = Mathf.Max(visual.sortingOrder, playerSprite.sortingOrder);

        if (visual.animator != null)
        {
            worldObject.AddComponent<Animator>().runtimeAnimatorController = visual.animator;
        }

        CircleCollider2D worldCollider = worldObject.AddComponent<CircleCollider2D>();
        worldCollider.radius = visual.colliderRadius;
        worldCollider.isTrigger = isTrigger;

        if (visual.spawnEffect != null)
        {
            Destroy(Instantiate(visual.spawnEffect, position, Quaternion.identity), effectLifetime);
        }

        return worldObject;
    }

    private void BuildVisual()
    {
        RectTransform root = CreateRect("Visual", transform);
        Stretch(root);
        visualRoot = root.gameObject;

        RectTransform backgroundRect = CreateRect(ButtonObjectName, root);
        Stretch(backgroundRect);
        background = backgroundRect.gameObject.AddComponent<Image>();

        Button button = backgroundRect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(OnClickUltimate);

        RectTransform iconRect = CreateRect("Icon", root);
        iconImage = iconRect.gameObject.AddComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        RectTransform overlayRect = CreateRect("Overlay", root);
        Stretch(overlayRect);
        overlay = overlayRect.gameObject.AddComponent<Image>();
        overlay.raycastTarget = false;
        overlay.type = Image.Type.Filled;
        overlay.fillMethod = Image.FillMethod.Radial360;
        overlay.fillOrigin = (int)Image.Origin360.Top;
        overlay.fillClockwise = false;
        overlay.fillAmount = 0f;

        RectTransform labelRect = CreateRect("Remaining Count", root);
        labelText = labelRect.gameObject.AddComponent<Text>();
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
        labelText.verticalOverflow = VerticalWrapMode.Overflow;
        labelText.raycastTarget = false;
        labelOutline = labelRect.gameObject.AddComponent<Outline>();

        // 남은 횟수: 버튼 안 오른쪽 아래 작은 어두운 칸
        RectTransform boxRect = CreateRect("Count Box", root);
        countBox = boxRect.gameObject.AddComponent<Image>();
        countBox.raycastTarget = false;
        RectTransform countRect = CreateRect("Count", boxRect);
        Stretch(countRect);
        countText = countRect.gameObject.AddComponent<Text>();
        countText.alignment = TextAnchor.MiddleCenter;
        countText.horizontalOverflow = HorizontalWrapMode.Overflow;
        countText.verticalOverflow = VerticalWrapMode.Overflow;
        countText.raycastTarget = false;
        countText.fontStyle = FontStyle.Bold;
        countText.alignment = TextAnchor.MiddleCenter;
    }

    private void ApplyLayout()
    {
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = offsetFromCorner;
        rect.sizeDelta = new Vector2(circleSize, circleSize);
        rect.localScale = Vector3.one * buttonScale;

        Sprite shape = buttonSprite != null ? buttonSprite : circleSprite;
        background.sprite = shape;
        overlay.sprite = shape;

        background.color = backgroundColor;
        overlay.color = overlayColor;
        iconImage.rectTransform.sizeDelta = Vector2.one * (circleSize * iconScale);

        labelText.font = labelFont != null ? labelFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontSize = labelFontSize;
        labelText.color = labelColor;
        labelText.rectTransform.sizeDelta = new Vector2(circleSize, labelFontSize * 1.5f);
        labelText.rectTransform.anchoredPosition = labelOffset;

        labelOutline.enabled = labelOutlineWidth > 0f;
        labelOutline.effectColor = labelOutlineColor;
        labelOutline.effectDistance = new Vector2(labelOutlineWidth, -labelOutlineWidth);

        // 남은 횟수: 아이콘과 겹치지 않게 버튼 오른쪽 아래 모서리 바깥쪽에 흰 글자 + 검은 외곽선
        // 남은 횟수: 버튼 안 오른쪽 아래 모서리에 흰 글자 (외곽선 없이)
        int countSize = ultCountFontSize > 0 ? ultCountFontSize : Mathf.RoundToInt(circleSize * 0.27f);
        RectTransform boxRect = countBox.rectTransform;
        boxRect.sizeDelta = new Vector2(circleSize * 0.5f, countSize * 1.3f);
        // 남은 횟수: 버튼 안 오른쪽 위(2시 방향) 빈칸
        boxRect.anchoredPosition = new Vector2(circleSize * ultCountPosition.x, circleSize * ultCountPosition.y);
        countBox.color = Color.clear;                                   // 칸 배경 없이 글자만
        countText.font = labelText.font;
        countText.fontSize = Mathf.Max(1, countSize);
        countText.color = Color.white;
        Outline old = countText.GetComponent<Outline>();
        if (old != null) Destroy(old);                                  // 외곽선은 글자가 깨져 보여 쓰지 않음
    }

    // 아이콘과 남은 횟수 표시 갱신
    private void RefreshDisplay()
    {
        if (current == null)
            return;

        Sprite icon = current.icon;
        if (isSniperMode && current is SniperUltimate sniper && sniper.activeIcon != null)
        {
            icon = sniper.activeIcon;
        }

        iconImage.sprite = icon;
        iconImage.enabled = icon != null;
        string count = remainingUses < 0 ? infiniteText : remainingUses.ToString();
        countText.text = count;                                         // 버튼 안 오른쪽 아래
        // 미끼(보와보와) 아이콘은 밝아 흰 숫자가 묻히므로 숫자 뒤에만 어두운 둥근 바탕
        countBox.color = Color.clear;                                   // 숫자 뒤 바탕 없음
        labelText.text = showKeyHint ? "[" + ultimateKey + "]" : "";    // 버튼 아래: 사용 키
    }

    public static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child.GetComponent<RectTransform>();
    }

    public static void Stretch(RectTransform target)
    {
        target.anchorMin = Vector2.zero;
        target.anchorMax = Vector2.one;
        target.offsetMin = Vector2.zero;
        target.offsetMax = Vector2.zero;
    }
}
