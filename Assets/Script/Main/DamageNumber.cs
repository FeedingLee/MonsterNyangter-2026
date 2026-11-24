using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 데미지 숫자 표시 설정 (GameManager 인스펙터에서 조정)
[System.Serializable]
public class DamageNumberSettings
{
    [Tooltip("이 시간 안에 다시 맞으면 공격이 이어진 것으로 보고 숫자를 합산")]
    [Min(0f)] public float comboWindow = 1f;            // 공격이 이어지는 시간(초)
    [Min(0f)] public float fadeTime = 0.5f;             // 사라지는 데 걸리는 시간(초)
    [Min(0f)] public float floatSpeed = 1.5f;           // 사라지는 동안 위로 떠오르는 속도
    public float heightOffset = 0.3f;                   // 몬스터 머리 위 간격
    [Min(0.1f)] public float fontSize = 8f;
    public Color color = new Color(1f, 0.9f, 0.2f, 1f);
    public Color outlineColor = Color.black;
    [Range(0f, 1f)] public float outlineWidth = 0.25f;
    public int sortingOrder = 20;
    public Font font;                                   // neodgm 같은 일반 폰트 지정 가능, 비워두면 기본 폰트 사용
}

// 몬스터 머리 위에 합산 데미지를 표시하는 숫자
// 몬스터가 죽어도 끝까지 사라질 수 있도록 몬스터와 별개의 오브젝트로 동작한다.
public class DamageNumber : MonoBehaviour
{
    private static readonly Dictionary<SpriteRenderer, DamageNumber> activeNumbers = new Dictionary<SpriteRenderer, DamageNumber>();
    private static readonly Stack<DamageNumber> pool = new Stack<DamageNumber>();
    private static Font cachedSourceFont;
    private static TMP_FontAsset cachedFontAsset;

    private TextMeshPro text;
    private DamageNumberSettings settings;
    private SpriteRenderer target;
    private float total;
    private float idleTime;
    private float fadeElapsed;
    private bool isFading;

    // true이면 데미지 숫자를 표시하지 않음 (보스 처치로 클리어가 확정된 뒤)
    public static bool Suppressed { get; set; }

    // 화면에 떠 있는 숫자를 모두 즉시 숨김
    public static void HideAll()
    {
        foreach (DamageNumber number in FindObjectsOfType<DamageNumber>())
        {
            number.RemoveFromActive();
            number.gameObject.SetActive(false);
            pool.Push(number);
        }
        activeNumbers.Clear();
    }

    // 몬스터가 입은 데미지를 표시 (공격이 이어지는 중이면 기존 숫자에 합산)
    public static void Add(SpriteRenderer target, float amount)
    {
        if (Suppressed)
            return;

        if (!activeNumbers.TryGetValue(target, out DamageNumber number) || number == null)
        {
            number = Spawn();
            number.Begin(target);
            activeNumbers[target] = number;
        }

        number.AddDamage(amount);
    }

    private static DamageNumber Spawn()
    {
        while (pool.Count > 0)
        {
            DamageNumber pooled = pool.Pop();
            if (pooled != null)
            {
                pooled.gameObject.SetActive(true);
                return pooled;
            }
        }

        GameObject numberObject = new GameObject("Damage Number");
        DamageNumber number = numberObject.AddComponent<DamageNumber>();
        number.text = numberObject.AddComponent<TextMeshPro>();
        number.text.alignment = TextAlignmentOptions.Center;
        number.text.enableWordWrapping = false;
        return number;
    }

    private void Begin(SpriteRenderer target)
    {
        this.target = target;
        settings = GameManager.instance.damageNumber;
        total = 0f;
        idleTime = 0f;
        fadeElapsed = 0f;
        isFading = false;

        TMP_FontAsset fontAsset = GetFontAsset(settings.font);
        if (fontAsset != null)
        {
            text.font = fontAsset;
        }
        text.fontSize = settings.fontSize;
        text.color = settings.color;
        text.alpha = 1f;
        ApplyOutline();

        // 몬스터와 같은 정렬 레이어의 앞쪽에 표시
        Renderer textRenderer = text.GetComponent<Renderer>();
        textRenderer.sortingLayerID = target.sortingLayerID;
        textRenderer.sortingOrder = settings.sortingOrder;

        FollowTarget();
    }

    // 일반 폰트(Font)를 TextMesh Pro에서 쓸 수 있는 폰트 에셋으로 변환
    private static TMP_FontAsset GetFontAsset(Font source)
    {
        if (source == null)
        {
            return TMP_Settings.defaultFontAsset;
        }

        if (cachedFontAsset == null || cachedSourceFont != source)
        {
            cachedSourceFont = source;
            cachedFontAsset = TMP_FontAsset.CreateFontAsset(source);
        }

        return cachedFontAsset;
    }

    // 기본 폰트의 모바일 셰이더는 테두리가 꺼져 있으므로, 테두리를 지원하는 셰이더로 바꿔 직접 적용
    private void ApplyOutline()
    {
        Material material = text.fontMaterial;

        Shader outlineShader = Shader.Find("TextMeshPro/Distance Field");
        if (outlineShader != null && material.shader != outlineShader)
        {
            material.shader = outlineShader;
        }

        material.EnableKeyword("OUTLINE_ON");
        material.SetColor("_OutlineColor", settings.outlineColor);
        material.SetFloat("_OutlineWidth", settings.outlineWidth);
        text.UpdateMeshPadding();
    }

    private void AddDamage(float amount)
    {
        total += amount;
        idleTime = 0f;
        text.text = Mathf.RoundToInt(total).ToString();
    }

    private void Update()
    {
        if (isFading)
        {
            UpdateFade();
            return;
        }

        if (target == null || !target.gameObject.activeInHierarchy)
        {
            BeginFade();
            return;
        }

        FollowTarget();

        idleTime += Time.deltaTime;
        if (idleTime >= settings.comboWindow)
        {
            BeginFade();
        }
    }

    private void FollowTarget()
    {
        Bounds bounds = target.bounds;
        transform.position = new Vector3(bounds.center.x, bounds.max.y + settings.heightOffset, 0f);
    }

    // 사라지기 시작하면 공격이 끊긴 것이므로, 이후의 타격은 새 숫자로 표시됨
    private void BeginFade()
    {
        isFading = true;
        RemoveFromActive();
    }

    private void UpdateFade()
    {
        transform.position += Vector3.up * (settings.floatSpeed * Time.deltaTime);
        fadeElapsed += Time.deltaTime;

        float progress = settings.fadeTime > 0f ? fadeElapsed / settings.fadeTime : 1f;
        text.alpha = 1f - Mathf.Clamp01(progress);

        if (progress >= 1f)
        {
            gameObject.SetActive(false);
            pool.Push(this);
        }
    }

    private void RemoveFromActive()
    {
        if (target != null && activeNumbers.TryGetValue(target, out DamageNumber number) && number == this)
        {
            activeNumbers.Remove(target);
        }
    }

    private void OnDestroy()
    {
        RemoveFromActive();
    }
}
