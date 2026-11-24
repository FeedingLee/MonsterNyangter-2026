using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 스테이지 선택 화면: 왼쪽 스테이지 목록, 오른쪽 선택한 스테이지 정보
// 잠긴 스테이지는 검게 + 자물쇠만 (이름·정보 숨김, 선택 불가). 데이터가 없는 항목은 비워 둠. 별점·평가 표시는 없음
public partial class TitleMenu
{
    private class StageRow
    {
        public Image frame;
        public Text name;
        public int index;
    }

    private readonly List<StageRow> stageRows = new List<StageRow>();
    private int stageIndex = -1;

    private Text stageName, stageDescription, stageLevel, stageReward, stageCharacter;
    private Image stageImage, stageCharacterIcon;
    private AspectRatioFitter stageImageFit;
    private RectTransform stageMonsters, stageRewardIcons;
    private Button departButton;

    private CanvasGroup BuildStageSelect()
    {
        RectTransform panel = NewPanel("Stage Select");
        Header(panel, "스테이지 선택");
        BuildStageList(panel);
        BuildStageDetail(panel);

        // 선택한 캐릭터 (아래 가운데)
        stageCharacterIcon = Icon("Character Icon", panel, null, new Vector2(0.5f, 0f), new Vector2(-150f, cat.bottomButtonMargin.y), new Vector2(64f, 72f));
        stageCharacter = Label("Character", panel, "", cat.stageBodyFontSize, cat.lightTextColor, TextAnchor.MiddleLeft,
            new Vector2(0.5f, 0f), new Vector2(60f, cat.bottomButtonMargin.y), new Vector2(360f, 50f), true);

        BottomButton(panel, "돌아가기", false, false, cat.iconBack, () => Show(characterPanel));
        departButton = BottomButton(panel, "출발", true, true, cat.iconPlay, OnDepart);
        return panel.GetComponent<CanvasGroup>();
    }

    private void BuildStageList(RectTransform panel)
    {
        Image box = Frame("Stage List", panel, cat.ornatePanel, Vector4.zero, Color.white, Center, cat.stageListPosition, cat.stageListSize);

        // 스크롤 목록 (스테이지가 늘어나면 아래로 이어짐)
        RectTransform view = Stretch(NewRect("Viewport", box.rectTransform), 34f);
        view.gameObject.AddComponent<RectMask2D>();
        Image hit = view.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);

        RectTransform content = NewRect("Content", view);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = content.offsetMax = Vector2.zero;
        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = cat.stageRowGap;
        layout.padding = new RectOffset(4, 4, 6, 6);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = view;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        for (int i = 0; i < cat.stages.Count; i++)
        {
            stageRows.Add(MakeStageRow(content, i));
        }
    }

    private StageRow MakeStageRow(RectTransform content, int index)
    {
        MenuCatalog.StageEntry s = cat.stages[index];
        StageRow row = new StageRow { index = index };
        row.frame = Frame("Stage " + (index + 1), content, s.playable ? cat.rowNormal : cat.rowLocked, Vector4.zero, Color.white,
            Center, Vector2.zero, cat.stageRowSize);
        Vector2 size = cat.stageRowSize;

        if (!s.playable)
        {
            Icon("Lock", row.frame.transform, LockSprite, Center, Vector2.zero, new Vector2(52f, 58f));
            return row;
        }

        row.name = Label("Name", row.frame.transform, (index + 1) + ". " + s.displayName, cat.stageBodyFontSize + 4, cat.darkTextColor,
            TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(size.x * 0.5f + 34f, 0f), new Vector2(size.x - 40f, size.y), false);

        row.frame.raycastTarget = true;
        Button button = row.frame.gameObject.AddComponent<Button>();
        button.targetGraphic = row.frame;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        colors.normalColor = cat.buttonNormalTint;
        colors.highlightedColor = Color.white;
        colors.pressedColor = cat.buttonPressedTint;
        colors.selectedColor = cat.buttonNormalTint;
        button.colors = colors;
        button.onClick.AddListener(PlayClick);
        button.onClick.AddListener(() => SelectStage(index));
        MenuButtonFx fx = row.frame.gameObject.AddComponent<MenuButtonFx>();
        fx.target = button;
        return row;
    }

    private void BuildStageDetail(RectTransform panel)
    {
        Vector2 size = cat.stageDetailSize;
        Image card = Frame("Stage Detail", panel, cat.parchment, Vector4.zero, Color.white, Center, cat.stageDetailPosition, size);
        RectTransform r = card.rectTransform;
        float pad = 40f, inner = size.x - pad * 2f;
        int body = cat.stageBodyFontSize;

        stageName = Label("Name", r, "", cat.stageNameFontSize, cat.darkTextColor, TextAnchor.MiddleLeft, Center, Vector2.zero, Vector2.zero, false);
        Place(stageName.rectTransform, pad, pad - 4f, new Vector2(inner, 56f));

        // 대표 이미지 칸 (데이터가 없으면 빈 칸)
        Vector2 imageSize = new Vector2(Mathf.Min(cat.stageImageSize.x, inner), cat.stageImageSize.y);
        Image imageBox = Icon("Image Box", r, null, Center, Vector2.zero, Vector2.zero);
        imageBox.enabled = true;
        imageBox.preserveAspect = false;
        imageBox.color = cat.emptyAreaColor;
        Place(imageBox.rectTransform, pad + (inner - imageSize.x) * 0.5f, pad + 62f, imageSize);
        Outline edge = imageBox.gameObject.AddComponent<Outline>();
        edge.effectColor = cat.darkTextColor;
        edge.effectDistance = new Vector2(3f, -3f);
        imageBox.gameObject.AddComponent<RectMask2D>();
        stageImage = Icon("Image", imageBox.rectTransform, null, Center, Vector2.zero, imageSize);
        stageImage.preserveAspect = false;
        stageImageFit = stageImage.gameObject.AddComponent<AspectRatioFitter>();
        stageImageFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;   // 칸을 꽉 채우고 넘치는 부분은 잘림 (비율 유지)

        float top = pad + 62f + imageSize.y + 14f;
        stageDescription = Label("Description", r, "", body, cat.darkTextColor, TextAnchor.UpperLeft, Center, Vector2.zero, Vector2.zero, false);
        Place(stageDescription.rectTransform, pad, top, new Vector2(inner, 56f));

        top += 64f;
        Divider(r, pad, top, inner);
        SectionTitle(r, "스테이지 정보", pad, top + 12f, inner);
        top += 60f;

        float labelWidth = 260f, valueLeft = pad + labelWidth;
        string[] labels = { "권장 레벨", "주요 등장 몬스터", "최초 클리어 보상" };
        for (int i = 0; i < labels.Length; i++)
        {
            Text label = Label("Label " + i, r, labels[i], body, cat.darkTextColor, TextAnchor.MiddleLeft, Center, Vector2.zero, Vector2.zero, false);
            Place(label.rectTransform, pad + 12f, top + i * (cat.stageIconSize.y + 12f), new Vector2(labelWidth - 12f, cat.stageIconSize.y));
        }
        stageLevel = Label("Level", r, "", body, cat.darkTextColor, TextAnchor.MiddleLeft, Center, Vector2.zero, Vector2.zero, false);
        Place(stageLevel.rectTransform, valueLeft, top, new Vector2(inner - labelWidth, cat.stageIconSize.y));
        stageMonsters = IconRow("Monsters", r, valueLeft, top + (cat.stageIconSize.y + 12f), inner - labelWidth);
        stageRewardIcons = IconRow("Rewards", r, valueLeft, top + (cat.stageIconSize.y + 12f) * 2f, inner - labelWidth);
        stageReward = Label("Reward Text", r, "", body - 2, cat.darkTextColor, TextAnchor.MiddleLeft, Center, Vector2.zero, Vector2.zero, false);
        Place(stageReward.rectTransform, valueLeft, top + (cat.stageIconSize.y + 12f) * 2f, new Vector2(inner - labelWidth, cat.stageIconSize.y));
    }

    private RectTransform IconRow(string name, Transform parent, float left, float top, float width)
    {
        RectTransform row = NewRect(name, parent);
        Place(row, left, top, new Vector2(width, cat.stageIconSize.y));
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        return row;
    }

    private void FillIcons(RectTransform row, Sprite[] icons)
    {
        for (int i = row.childCount - 1; i >= 0; i--)
        {
            Destroy(row.GetChild(i).gameObject);
        }
        if (icons == null)
            return;
        foreach (Sprite icon in icons)
        {
            if (icon == null) continue;
            Image slotImage = Frame("Slot", row, cat.slot, Vector4.zero, Color.white, Center, Vector2.zero, cat.stageIconSize);
            Icon("Icon", slotImage.transform, icon, Center, Vector2.zero, cat.stageIconSize * 0.78f);
        }
    }

    // ---------------- 동작 ----------------
    private void OnStageOpen()
    {
        if (stageIndex < 0 || stageIndex >= cat.stages.Count || !cat.stages[stageIndex].playable)
        {
            stageIndex = cat.stages.FindIndex(s => s.playable);
        }
        RefreshStage();

        MenuCatalog.CharacterEntry c = SelectedCharacter;
        stageCharacterIcon.sprite = c.Portrait;
        stageCharacterIcon.enabled = c.Portrait != null;
        stageCharacter.text = "선택한 캐릭터  " + c.displayName;
    }

    private void SelectStage(int index)
    {
        if (index < 0 || index >= cat.stages.Count || !cat.stages[index].playable)
            return;
        stageIndex = index;
        RefreshStage();
    }

    private void RefreshStage()
    {
        foreach (StageRow row in stageRows)
        {
            if (!cat.stages[row.index].playable)
                continue;
            bool selected = row.index == stageIndex;
            row.frame.sprite = Sliced(selected ? cat.rowSelected : cat.rowNormal, Vector4.zero);
        }

        bool has = stageIndex >= 0;
        departButton.interactable = has;
        MenuCatalog.StageEntry s = has ? cat.stages[stageIndex] : null;
        stageName.text = has ? s.displayName : "";
        stageImage.sprite = has ? s.image : null;
        stageImage.enabled = has && s.image != null;
        if (stageImage.enabled) stageImageFit.aspectRatio = s.image.rect.width / s.image.rect.height;
        stageDescription.text = has ? s.description : "";
        stageLevel.text = has ? s.recommendedLevel : "";
        FillIcons(stageMonsters, has ? s.monsterIcons : null);
        FillIcons(stageRewardIcons, has ? s.rewardIcons : null);
        // 보상 글자는 아이콘 뒤에 이어 씀
        int rewardCount = 0;
        if (has && s.rewardIcons != null) foreach (Sprite icon in s.rewardIcons) if (icon != null) rewardCount++;
        stageReward.rectTransform.anchoredPosition = new Vector2(
            stageRewardIcons.anchoredPosition.x + rewardCount * (cat.stageIconSize.x + 12f), stageReward.rectTransform.anchoredPosition.y);
        stageReward.text = has ? s.rewardText : "";
    }

    private void OnDepart()
    {
        if (busy || launching || stageIndex < 0 || cat.characters.Count == 0 || !SelectedCharacter.IsUnlocked)
            return;
        MenuCatalog.StageEntry s = cat.stages[stageIndex];
        string scene = string.IsNullOrEmpty(s.sceneName) ? "Main" : s.sceneName;
        StartCoroutine(LaunchRoutine(SelectedCharacter.playerId, stageIndex, scene));
    }
}
