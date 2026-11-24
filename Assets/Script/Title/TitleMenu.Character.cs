using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 캐릭터 선택 화면: 가운데 선택 캐릭터, 좌우 미리보기(어둡게), 오른쪽 정보 카드
// 잠긴 캐릭터는 검은 실루엣 + 이름만 표시하고 능력치·필살기 대신 해금 조건을 보여 주며 [다음]을 막음
public partial class TitleMenu
{
    private class CardView
    {
        public RectTransform rect;
        public Image frame, art, lockIcon;
        public Text name, quote;
        public int nameSize, quoteSize;
        public Vector2 artCenter, artArea;       // 캐릭터 그림 영역 (카드 중심 기준)
        public float artScale;                   // 프레임마다 같은 배율 (프레임 크기가 달라도 꿀렁이지 않게)
        public Vector2 artPin;                   // 스프라이트 피벗이 놓일 자리 (게임 속 애니메이션과 같은 정렬)
    }

    private CardView centerCard, leftCard, rightCard;
    private int characterIndex;
    private float idleTime;
    private float punchTime = 99f;

    private Image infoPortrait, ultimateIcon;
    private Text infoName, infoQuote, infoDescription, ultimateName, ultimateDescription, unlockText;
    private GameObject statSection, lockSection;
    private Image[][] statCells;
    private Image[] pageDots;
    private Button nextButton;

    private MenuCatalog.CharacterEntry SelectedCharacter => cat.characters[characterIndex];

    private CanvasGroup BuildCharacterSelect()
    {
        RectTransform panel = NewPanel("Character Select");
        Header(panel, "캐릭터 선택");

        Vector2 c = cat.carouselCenter;
        leftCard = MakeCard("Card Prev", panel, c + new Vector2(-cat.sideCardOffset, 0f), cat.sideCardSize, () => StepCharacter(-1));
        rightCard = MakeCard("Card Next", panel, c + new Vector2(cat.sideCardOffset, 0f), cat.sideCardSize, () => StepCharacter(1));
        centerCard = MakeCard("Card Selected", panel, c, cat.centerCardSize, null);
        ArrowButton("Arrow Prev", panel, cat.arrowLeft, c + new Vector2(-cat.arrowOffset, 0f), () => StepCharacter(-1));
        ArrowButton("Arrow Next", panel, cat.arrowRight, c + new Vector2(cat.arrowOffset, 0f), () => StepCharacter(1));

        int count = cat.characters.Count;
        pageDots = new Image[count];
        float dotGap = 30f;
        for (int i = 0; i < count; i++)
        {
            pageDots[i] = Icon("Dot " + i, panel, cat.statEmpty, Center,
                c + new Vector2((i - (count - 1) * 0.5f) * dotGap, cat.pageDotsY), new Vector2(18f, 18f));
            pageDots[i].preserveAspect = true;
        }

        BuildInfoCard(panel);
        BottomButton(panel, "돌아가기", false, false, cat.iconBack, () => Show(mainPanel));
        nextButton = BottomButton(panel, "다음", true, true, cat.iconConfirm, OnCharacterNext);
        return panel.GetComponent<CanvasGroup>();
    }

    private CardView MakeCard(string name, Transform parent, Vector2 pos, Vector2 size, UnityAction onClick)
    {
        CardView v = new CardView();
        v.frame = Frame(name, parent, cat.cardNormal, Vector4.zero, Color.white, Center, pos, size);
        v.rect = v.frame.rectTransform;
        float scale = size.x / Mathf.Max(1f, cat.centerCardSize.x);
        v.nameSize = Mathf.Max(12, Mathf.RoundToInt(cat.cardNameFontSize * scale));
        v.quoteSize = Mathf.Max(10, Mathf.RoundToInt(cat.cardQuoteFontSize * scale));

        v.artCenter = new Vector2(0f, size.y * 0.1f);
        v.artArea = new Vector2(size.x * 0.72f, size.y * 0.56f);
        v.art = Icon("Art", v.rect, null, Center, v.artCenter, v.artArea);
        v.art.preserveAspect = false;
        v.lockIcon = Icon("Lock", v.rect, LockSprite, Center, new Vector2(0f, size.y * 0.1f), new Vector2(size.x * 0.16f, size.x * 0.19f));
        v.name = Label("Name", v.rect, "", v.nameSize, cat.goldColor, TextAnchor.MiddleCenter,
            Center, new Vector2(0f, -size.y * 0.29f), new Vector2(size.x * 0.92f, v.nameSize * 1.5f), true);
        v.quote = Label("Quote", v.rect, "", v.quoteSize, cat.lightTextColor, TextAnchor.MiddleCenter,
            Center, new Vector2(0f, -size.y * 0.39f), new Vector2(size.x * 0.9f, v.quoteSize * 2.6f), true);

        if (onClick != null)
        {
            v.frame.raycastTarget = true;
            Button button = v.frame.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(PlayClick);
            button.onClick.AddListener(onClick);
            v.frame.gameObject.AddComponent<MenuButtonFx>().target = button;
        }
        return v;
    }

    private void ArrowButton(string name, Transform parent, Sprite sprite, Vector2 pos, UnityAction onClick)
    {
        Image image = Icon(name, parent, sprite, Center, pos, cat.arrowSize);
        image.preserveAspect = false;
        image.color = cat.goldColor;
        image.raycastTarget = true;
        image.enabled = true;
        if (sprite == null)
        {
            image.color = new Color(0f, 0f, 0f, 0f);
            Label("Glyph", image.transform, name.EndsWith("Prev") ? "◀" : "▶", 48, cat.goldColor, TextAnchor.MiddleCenter,
                Center, Vector2.zero, cat.arrowSize * 1.5f, true);
        }
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        colors.normalColor = cat.buttonNormalTint;
        colors.highlightedColor = Color.white;
        colors.pressedColor = cat.buttonPressedTint;
        colors.selectedColor = cat.buttonNormalTint;
        button.colors = colors;
        button.onClick.AddListener(PlayClick);
        button.onClick.AddListener(onClick);
        image.gameObject.AddComponent<MenuButtonFx>().target = button;
    }

    // ---------------- 정보 카드 ----------------
    private void BuildInfoCard(RectTransform panel)
    {
        Vector2 size = cat.infoCardSize;
        Image card = Frame("Info Card", panel, cat.parchment, Vector4.zero, Color.white, Center, cat.infoCardPosition, size);
        RectTransform r = card.rectTransform;
        float pad = 46f;
        float inner = size.x - pad * 2f;
        int body = cat.infoBodyFontSize;

        Image slot = Frame("Portrait Slot", r, cat.slot, Vector4.zero, Color.white, Center, Vector2.zero, new Vector2(124f, 124f));
        Place(slot.rectTransform, pad, pad, new Vector2(124f, 124f));
        infoPortrait = Icon("Portrait", slot.rectTransform, null, Center, Vector2.zero, new Vector2(100f, 104f));
        float textLeft = pad + 144f;
        infoName = Label("Name", r, "", cat.infoNameFontSize, cat.darkTextColor, TextAnchor.MiddleLeft, Center, Vector2.zero, Vector2.zero, false);
        Place(infoName.rectTransform, textLeft, pad + 6f, new Vector2(size.x - textLeft - pad, 56f));
        infoQuote = Label("Quote", r, "", body, cat.headerTextColor, TextAnchor.UpperLeft, Center, Vector2.zero, Vector2.zero, false);
        Place(infoQuote.rectTransform, textLeft, pad + 70f, new Vector2(size.x - textLeft - pad, 54f));
        infoDescription = Label("Description", r, "", body, cat.darkTextColor, TextAnchor.UpperLeft, Center, Vector2.zero, Vector2.zero, false);
        Place(infoDescription.rectTransform, pad, pad + 150f, new Vector2(inner, 84f));

        // 능력치 + 필살기 (해금된 캐릭터만)
        statSection = Stretch(NewRect("Stats", r)).gameObject;
        Transform s = statSection.transform;
        Divider(s, pad, pad + 240f, inner);
        SectionTitle(s, "능력치", pad, pad + 258f, inner);
        int rows = Mathf.Min(4, cat.statLabels.Length);
        statCells = new Image[rows][];
        float rowTop = pad + 308f, rowStep = 44f;
        for (int i = 0; i < rows; i++)
        {
            Text label = Label("Stat " + i, s, cat.statLabels[i], body, cat.darkTextColor, TextAnchor.MiddleLeft, Center, Vector2.zero, Vector2.zero, false);
            Place(label.rectTransform, pad + 12f, rowTop + rowStep * i, new Vector2(200f, cat.statCellSize.y + 8f));
            statCells[i] = new Image[cat.statMax];
            for (int j = 0; j < cat.statMax; j++)
            {
                Image cell = Icon("Cell " + j, s, cat.statEmpty, Center, Vector2.zero, cat.statCellSize);
                cell.preserveAspect = false;
                Place(cell.rectTransform, pad + 230f + j * (cat.statCellSize.x + cat.statCellGap), rowTop + rowStep * i + (cat.statCellSize.y + 8f - cat.statCellSize.y) * 0.5f, cat.statCellSize);
                statCells[i][j] = cell;
            }
        }
        float ultTop = rowTop + rowStep * rows + 14f;
        Divider(s, pad, ultTop, inner);
        SectionTitle(s, "필살기", pad, ultTop + 18f, inner);
        Image ultSlot = Frame("Ultimate Slot", s, cat.slot, Vector4.zero, Color.white, Center, Vector2.zero, Vector2.zero);
        Place(ultSlot.rectTransform, pad, ultTop + 68f, new Vector2(100f, 100f));
        ultimateIcon = Icon("Ultimate Icon", ultSlot.rectTransform, null, Center, Vector2.zero, new Vector2(76f, 76f));
        ultimateName = Label("Ultimate Name", s, "", cat.infoHeaderFontSize, cat.darkTextColor, TextAnchor.MiddleLeft, Center, Vector2.zero, Vector2.zero, false);
        Place(ultimateName.rectTransform, pad + 120f, ultTop + 66f, new Vector2(inner - 120f, 40f));
        ultimateDescription = Label("Ultimate Description", s, "", body - 2, cat.darkTextColor, TextAnchor.UpperLeft, Center, Vector2.zero, Vector2.zero, false);
        Place(ultimateDescription.rectTransform, pad + 120f, ultTop + 112f, new Vector2(inner - 120f, 90f));

        // 해금 조건 (잠긴 캐릭터만)
        lockSection = Stretch(NewRect("Locked", r)).gameObject;
        Transform l = lockSection.transform;
        Divider(l, pad, pad + 240f, inner);
        Image lockImage = Icon("Lock", l, LockSprite, Center, Vector2.zero, Vector2.zero);
        Place(lockImage.rectTransform, size.x * 0.5f - 40f, pad + 290f, new Vector2(80f, 96f));
        Text lockTitle = Label("Unlock Title", l, "해금 조건", cat.infoHeaderFontSize, cat.headerTextColor, TextAnchor.MiddleCenter, Center, Vector2.zero, Vector2.zero, false);
        Place(lockTitle.rectTransform, pad, pad + 410f, new Vector2(inner, 40f));
        unlockText = Label("Unlock Condition", l, "", body + 2, cat.darkTextColor, TextAnchor.UpperCenter, Center, Vector2.zero, Vector2.zero, false);
        Place(unlockText.rectTransform, pad, pad + 464f, new Vector2(inner, 120f));
    }

    private void SectionTitle(Transform parent, string title, float left, float top, float width)
    {
        Image pawIcon = Icon("Paw", parent, cat.paw, Center, Vector2.zero, Vector2.zero);
        Place(pawIcon.rectTransform, left, top + 6f, new Vector2(26f, 26f));
        Text text = Label(title, parent, title, cat.infoHeaderFontSize, cat.headerTextColor, TextAnchor.MiddleLeft, Center, Vector2.zero, Vector2.zero, false);
        Place(text.rectTransform, left + 36f, top, new Vector2(width - 36f, 38f));
    }

    private void Divider(Transform parent, float left, float top, float width)
    {
        Image line = Icon("Divider", parent, cat.divider, Center, Vector2.zero, Vector2.zero);
        line.preserveAspect = false;
        if (cat.divider == null)
        {
            line.enabled = true;
            line.color = new Color(cat.darkTextColor.r, cat.darkTextColor.g, cat.darkTextColor.b, 0.4f);
        }
        Place(line.rectTransform, left, top, new Vector2(width, 4f));
    }

    // 부모의 왼쪽 위 기준으로 배치
    private static void Place(RectTransform rect, float left, float top, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(left, -top);
        rect.sizeDelta = size;
    }

    // ---------------- 동작 ----------------
    private void OnCharacterOpen()
    {
        characterIndex = Mathf.Clamp(characterIndex, 0, Mathf.Max(0, cat.characters.Count - 1));
        RefreshCharacter();
    }

    private void StepCharacter(int dir)
    {
        int count = cat.characters.Count;
        if (count <= 1 || busy || launching)
            return;
        characterIndex = (characterIndex + dir + count) % count;
        punchTime = 0f;
        idleTime = 0f;
        RefreshCharacter();
    }

    private void OnCharacterNext()
    {
        if (cat.characters.Count == 0 || !SelectedCharacter.IsUnlocked)
            return;
        Show(stagePanel);
    }

    private void UpdateCharacterKeys()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            PlayClick();
            StepCharacter(-1);
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            PlayClick();
            StepCharacter(1);
        }
        else if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && nextButton.interactable)
        {
            nextButton.onClick.Invoke();
        }
    }

    private void RefreshCharacter()
    {
        int count = cat.characters.Count;
        if (count == 0)
            return;

        MenuCatalog.CharacterEntry e = SelectedCharacter;
        SetCard(centerCard, e, true);
        leftCard.rect.gameObject.SetActive(count > 1);
        rightCard.rect.gameObject.SetActive(count > 2);
        if (count > 1) SetCard(leftCard, cat.characters[(characterIndex - 1 + count) % count], false);
        if (count > 2) SetCard(rightCard, cat.characters[(characterIndex + 1) % count], false);

        for (int i = 0; i < pageDots.Length; i++)
        {
            pageDots[i].sprite = i == characterIndex ? cat.statFilled : cat.statEmpty;
        }

        bool unlocked = e.IsUnlocked;
        infoPortrait.sprite = e.Portrait;
        infoPortrait.enabled = e.Portrait != null;
        infoPortrait.color = unlocked ? Color.white : cat.silhouetteColor;
        infoName.text = e.displayName;
        infoQuote.text = unlocked ? e.quote : "";
        infoQuote.fontStyle = e.quoteSmallItalic ? FontStyle.Italic : FontStyle.Normal;
        infoQuote.fontSize = e.quoteSmallItalic ? cat.infoBodyFontSize - 3 : cat.infoBodyFontSize;
        infoDescription.text = unlocked ? e.description : "";

        statSection.SetActive(unlocked);
        lockSection.SetActive(!unlocked);
        int[] values = { e.attack, e.cooldown, e.stamina, e.speed };
        for (int i = 0; i < statCells.Length; i++)
            for (int j = 0; j < statCells[i].Length; j++)
                statCells[i][j].sprite = j < values[i] ? cat.statFilled : cat.statEmpty;

        ultimateIcon.sprite = e.ultimateIcon;
        ultimateIcon.enabled = e.ultimateIcon != null;
        ultimateName.text = e.ultimateName;
        ultimateDescription.text = e.ultimateDescription;
        unlockText.text = string.IsNullOrEmpty(e.unlockCondition) ? cat.unlockConditionFallback : e.unlockCondition;

        nextButton.interactable = unlocked;
    }

    private void SetCard(CardView v, MenuCatalog.CharacterEntry e, bool selected)
    {
        bool unlocked = e.IsUnlocked;
        v.frame.sprite = Sliced(!unlocked ? cat.cardLocked : selected ? cat.cardSelected : cat.cardNormal, Vector4.zero);
        v.frame.type = v.frame.sprite != null && v.frame.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        v.frame.color = !unlocked ? (selected ? Color.Lerp(cat.lockedTint, Color.white, 0.25f) : cat.lockedTint) : selected ? Color.white : cat.sideCardTint;

        PrepareArt(v, e);
        SetArtFrame(v, e.Portrait);
        v.art.color = !unlocked ? cat.silhouetteColor : selected ? Color.white : cat.sideCardTint;
        v.lockIcon.enabled = !unlocked;

        v.name.text = e.displayName;
        v.name.color = selected && unlocked ? cat.goldColor : cat.mutedTextColor;
        v.quote.text = unlocked ? e.quote : "잠김";
        v.quote.fontStyle = unlocked && e.quoteSmallItalic ? FontStyle.Italic : FontStyle.Normal;
        v.quote.fontSize = unlocked && e.quoteSmallItalic ? v.quoteSize - 3 : v.quoteSize;
        v.quote.color = selected ? cat.lightTextColor : cat.mutedTextColor;
    }

    // 캐릭터 그림 배율·자리: 모든 프레임에 같은 배율을 쓰고 발(그림 아래쪽)을 같은 자리에 맞춤
    // (서 있는 애니메이션 중 한 프레임만 위로 몇 픽셀 길어서, 가운데 맞춤이나 칸 맞춤이면 발이 들썩이고 몸이 눌려 보임)
    private static void PrepareArt(CardView v, MenuCatalog.CharacterEntry e)
    {
        Vector2 max = Vector2.zero;
        Sprite[] frames = e.idleFrames;
        if (frames != null)
            foreach (Sprite f in frames)
                if (f != null) max = Vector2.Max(max, f.rect.size);
        Sprite first = e.Portrait;
        if (first == null || max.x <= 0f || max.y <= 0f)
        {
            v.artScale = 0f;
            return;
        }
        v.artScale = Mathf.Min(v.artArea.x / max.x, v.artArea.y / max.y);
        Vector2 size = first.rect.size * v.artScale;
        float pivotX = first.pivot.x / first.rect.width;
        v.artPin = v.artCenter + new Vector2((pivotX - 0.5f) * size.x, -size.y * 0.5f);
    }

    private static void SetArtFrame(CardView v, Sprite frame)
    {
        v.art.sprite = frame;
        v.art.enabled = frame != null && v.artScale > 0f;
        if (!v.art.enabled)
            return;
        RectTransform r = v.art.rectTransform;
        r.pivot = new Vector2(frame.pivot.x / frame.rect.width, 0f);
        r.sizeDelta = frame.rect.size * v.artScale;
        r.anchoredPosition = v.artPin;
    }

    // 가운데 캐릭터 서 있는 애니메이션 + 바뀔 때 살짝 튀어나옴
    private void AnimateCharacter()
    {
        if (cat.characters.Count == 0)
            return;

        float dt = Time.unscaledDeltaTime;
        idleTime += dt;
        Sprite[] frames = SelectedCharacter.idleFrames;
        if (frames != null && frames.Length > 1)
        {
            Sprite frame = frames[(int)(idleTime * cat.idleFps) % frames.Length];
            if (frame != null && frame != centerCard.art.sprite) SetArtFrame(centerCard, frame);
        }

        punchTime += dt;
        float k = Mathf.Clamp01(punchTime / cat.cardPunchTime);
        centerCard.rect.localScale = Vector3.one * Mathf.Lerp(cat.cardPunchScale, 1f, k);
    }
}
