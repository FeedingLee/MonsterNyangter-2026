using UnityEngine;
using UnityEngine.UI;

// 설정 화면: 왼쪽 탭, 오른쪽 옵션
// 실제로 있는 옵션(배경음·효과음 볼륨, 기존 AudioSettings와 같은 저장 키)만 사운드 탭에 둠. 옵션이 없는 탭은 비워 둠
// 슬라이더를 움직이면 바로 들리고, [적용]을 눌러야 저장. [기본값]은 MenuCatalog의 기본 볼륨으로 되돌림. 돌아가기(ESC)는 저장하지 않은 변경을 취소
public partial class TitleMenu
{
    private Image[] tabImages;
    private Text[] tabLabels;
    private GameObject[] tabPages;
    private int tabIndex;
    private Slider bgmSlider, sfxSlider;
    private Text bgmValue, sfxValue, toast;
    private float toastUntil;

    private CanvasGroup BuildSettings()
    {
        RectTransform panel = NewPanel("Settings");
        Header(panel, "설정");

        int count = cat.settingTabs.Length;
        tabImages = new Image[count];
        tabLabels = new Text[count];
        tabPages = new GameObject[count];

        Image box = Frame("Options", panel, cat.ornatePanel, Vector4.zero, Color.white, Center, cat.settingsPanelPosition, cat.settingsPanelSize);

        for (int i = 0; i < count; i++)
        {
            int index = i;
            Sprite tabIcon = cat.settingTabIcons != null && i < cat.settingTabIcons.Length ? cat.settingTabIcons[i] : null;
            Button tab = MakeButton("Tab " + cat.settingTabs[i], panel, cat.settingTabs[i], cat.settingFontSize, false,
                Center, cat.tabFirstPosition + new Vector2(0f, -cat.tabSpacing * i), cat.tabButtonSize, () => SelectTab(index), tabIcon);
            tabImages[i] = (Image)tab.targetGraphic;
            tabLabels[i] = tab.GetComponentInChildren<Text>();
            tabPages[i] = Stretch(NewRect("Page " + cat.settingTabs[i], box.rectTransform), 50f).gameObject;
        }
        if (cat.soundTabIndex >= 0 && cat.soundTabIndex < count)
        {
            BuildSoundPage(tabPages[cat.soundTabIndex].transform);
        }

        BottomButton(panel, "돌아가기", false, false, cat.iconBack, CancelSettings);
        BottomButton(panel, "적용", true, true, cat.iconConfirm, ApplySettings);
        Vector2 resetPos = new Vector2(-cat.bottomButtonMargin.x - cat.bottomButtonSize.x - 40f, cat.bottomButtonMargin.y);
        MakeButton("기본값", panel, "기본값", cat.bottomButtonFontSize, false, new Vector2(1f, 0f), resetPos, cat.bottomButtonSize, ResetSettings, cat.iconReset);

        toast = Label("Toast", panel, "적용되었습니다", cat.settingFontSize - 4, cat.goldColor, TextAnchor.MiddleCenter,
            new Vector2(1f, 0f), new Vector2(-cat.bottomButtonMargin.x, cat.bottomButtonMargin.y + cat.bottomButtonSize.y * 0.5f + 34f), new Vector2(360f, 44f), true);
        toast.gameObject.SetActive(false);
        return panel.GetComponent<CanvasGroup>();
    }

    private void BuildSoundPage(Transform page)
    {
        bgmSlider = SoundRow(page, 0, cat.bgmIcon, "배경음", out bgmValue);
        sfxSlider = SoundRow(page, 1, cat.sfxIcon, "효과음", out sfxValue);
    }

    private Slider SoundRow(Transform page, int row, Sprite icon, string title, out Text valueText)
    {
        float top = 20f + row * cat.settingRowHeight;
        float h = cat.settingRowHeight * 0.6f;
        Image iconImage = Icon("Icon", page, icon, Center, Vector2.zero, Vector2.zero);
        iconImage.color = cat.lightTextColor;
        Place(iconImage.rectTransform, 20f, top + (h - 48f) * 0.5f, new Vector2(48f, 48f));
        Text label = Label(title, page, title, cat.settingFontSize, cat.lightTextColor, TextAnchor.MiddleLeft, Center, Vector2.zero, Vector2.zero, true);
        Place(label.rectTransform, 90f, top, new Vector2(240f, h));

        Slider slider = MakeSlider(page, 340f, top + (h - cat.sliderSize.y) * 0.5f);
        valueText = Label("Value", page, "", cat.settingFontSize, cat.goldColor, TextAnchor.MiddleRight, Center, Vector2.zero, Vector2.zero, true);
        Place(valueText.rectTransform, 340f + cat.sliderSize.x + 20f, top, new Vector2(120f, h));


        slider.onValueChanged.AddListener(_ => OnVolumeChanged());
        return slider;
    }

    private Slider MakeSlider(Transform parent, float left, float top)
    {
        RectTransform r = NewRect("Slider", parent);
        Place(r, left, top, cat.sliderSize);
        Slider slider = r.gameObject.AddComponent<Slider>();

        // 홈(어두운 막대) 위에 초록 채움. 둘 다 Panel_1 슬라이더 그림
        Image back = Frame("Track", r, cat.sliderBar, Vector4.zero, Color.white, Center, Vector2.zero, Vector2.zero);
        Stretch(back.rectTransform);
        back.raycastTarget = true;
        if (cat.sliderBar == null) back.color = cat.emptyAreaColor;

        RectTransform fillArea = Stretch(NewRect("Fill Area", r), 0f);
        Image fill = Frame("Fill", fillArea, cat.sliderFill, Vector4.zero, Color.white, Center, Vector2.zero, Vector2.zero);
        if (cat.sliderFill == null) fill.color = cat.sliderFillColor;
        fill.rectTransform.sizeDelta = Vector2.zero;

        RectTransform handleArea = Stretch(NewRect("Handle Area", r));
        handleArea.offsetMin = new Vector2(cat.sliderHandleSize.x * 0.5f, 0f);
        handleArea.offsetMax = new Vector2(-cat.sliderHandleSize.x * 0.5f, 0f);
        Image handle = NewRect("Handle", handleArea).gameObject.AddComponent<Image>();
        handle.sprite = cat.sliderHandle;
        handle.color = cat.sliderHandle != null ? Color.white : cat.goldColor;
        handle.rectTransform.sizeDelta = new Vector2(cat.sliderHandleSize.x, cat.sliderHandleSize.y - cat.sliderSize.y);

        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = slider.colors;
        colors.normalColor = cat.buttonNormalTint;
        colors.highlightedColor = Color.white;
        colors.pressedColor = cat.buttonPressedTint;
        colors.selectedColor = cat.buttonNormalTint;
        slider.colors = colors;
        return slider;
    }

    // ---------------- 동작 ----------------
    private void OnSettingsOpen()
    {
        if (bgmSlider != null)
        {
            bgmSlider.SetValueWithoutNotify(MenuCatalog.SavedBgmVolume);
            sfxSlider.SetValueWithoutNotify(MenuCatalog.SavedSfxVolume);
            OnVolumeChanged();
        }
        toast.gameObject.SetActive(false);
        SelectTab(Mathf.Clamp(cat.startTabIndex, 0, cat.settingTabs.Length - 1));
    }

    private void SelectTab(int index)
    {
        tabIndex = index;
        for (int i = 0; i < tabImages.Length; i++)
        {
            bool on = i == index;
            Sprite s = Sliced(on ? cat.buttonGreen : cat.buttonBrown, cat.objectPanelBorder);
            if (s != null) tabImages[i].sprite = s;
            tabLabels[i].color = on ? Color.white : cat.lightTextColor;
            tabPages[i].SetActive(on);
        }
    }

    private void OnVolumeChanged()
    {
        bgmValue.text = Mathf.RoundToInt(bgmSlider.value * 100f) + "%";
        sfxValue.text = Mathf.RoundToInt(sfxSlider.value * 100f) + "%";
        PreviewVolume(bgmSlider.value, sfxSlider.value);
    }

    private void ResetSettings()
    {
        if (bgmSlider == null)
            return;
        bgmSlider.value = cat.defaultBgmVolume;
        sfxSlider.value = cat.defaultSfxVolume;
    }

    private void ApplySettings()
    {
        if (bgmSlider != null)
        {
            PlayerPrefs.SetFloat(MenuCatalog.BgmKey, bgmSlider.value);
            PlayerPrefs.SetFloat(MenuCatalog.SfxKey, sfxSlider.value);
            PlayerPrefs.Save();
        }
        toast.gameObject.SetActive(true);
        toastUntil = Time.unscaledTime + cat.toastTime;
    }

    private void CancelSettings()
    {
        PreviewVolume(MenuCatalog.SavedBgmVolume, MenuCatalog.SavedSfxVolume);
        Show(mainPanel);
    }

    private void UpdateToast()
    {
        if (toast != null && toast.gameObject.activeSelf && Time.unscaledTime >= toastUntil)
        {
            toast.gameObject.SetActive(false);
        }
    }
}
