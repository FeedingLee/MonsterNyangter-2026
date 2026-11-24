using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 특수 젬 화면: 공통 부품(그림 칸, 두 줄 옵션, 스크롤 목록, 조각 표시, 잠금 버튼) / 수치 재설정 / 분해
// 규칙·저장은 GemSave, 수치·범위는 GemCatalog
public partial class TitleMenu
{
    private CanvasGroup gemRerollPanel, gemDecomposePanel;
    private readonly List<Text> shardTexts = new List<Text>();
    private static readonly Color PaperTint = new Color(0.96f, 0.9f, 0.8f, 1f);
    private static readonly Color SelectedTint = new Color(1f, 0.86f, 0.5f, 1f);

    // ---------------- 공통 ----------------
    private Sprite SpecialSprite(GemSave.SpecialGem g) => gc.SpecialIcon(GemSave.SpecialCategory(g));

    private string UpLine(GemSave.SpecialGem g) => "▲ " + StatCatalog.Instance.Name(g.up) + " " + gc.OptionValue(gc.UpOption(g.up), g.upValue);
    private string DownLine(GemSave.SpecialGem g) => "▼ " + StatCatalog.Instance.Name(g.down) + " " + gc.OptionValue(gc.DownOption(g.down), g.downValue);

    // 두 줄 (증가 초록 / 감소 빨강), 넘치면 글자 크기만 줄임
    private void SpecialLines(Transform parent, GemSave.SpecialGem g, Vector2 pos, float width, int fontSize, float gap)
    {
        Text up = Label("Up", parent, UpLine(g), fontSize, gc.upColor, TextAnchor.MiddleLeft, Center, pos + new Vector2(0f, gap * 0.5f), new Vector2(width, fontSize + 12f), false);
        FitOneLine(up, fontSize, 14, width);
        Text down = Label("Down", parent, DownLine(g), fontSize, gc.downColor, TextAnchor.MiddleLeft, Center, pos - new Vector2(0f, gap * 0.5f), new Vector2(width, fontSize + 12f), false);
        FitOneLine(down, fontSize, 14, width);
    }

    // 오른쪽 위 조각 표시
    private void ShardCounter(RectTransform panel)
    {
        Image box = Frame("Shards", panel, gc.buttonBrown, Vector4.zero, Color.white, new Vector2(1f, 1f), new Vector2(-250f, -82f), new Vector2(300f, 76f));
        Icon("Icon", box.transform, gc.shard, Center, new Vector2(-104f, 2f), new Vector2(54f, 54f));
        Text t = Label("Count", box.transform, "0", 34, Color.white, TextAnchor.MiddleRight, Center, new Vector2(20f, 0f), new Vector2(200f, 60f), true);
        shardTexts.Add(t);
    }

    private void RefreshShards()
    {
        foreach (Text t in shardTexts) t.text = GemSave.Current.shards.ToString("N0");
    }

    // 세로 스크롤 목록 (마우스 휠). 내용은 content 아래에 위에서부터 배치
    private RectTransform ScrollContent(Transform parent, Vector2 pos, Vector2 size)
    {
        RectTransform view = NewRect("Scroll", parent, Center, pos, size);
        view.gameObject.AddComponent<RectMask2D>();
        Image hit = view.gameObject.AddComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f);
        ScrollRect sr = view.gameObject.AddComponent<ScrollRect>();
        RectTransform content = NewRect("Content", view, new Vector2(0.5f, 1f), Vector2.zero, size);
        content.pivot = new Vector2(0.5f, 1f);
        sr.content = content;
        sr.viewport = view;
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.inertia = false;
        sr.scrollSensitivity = 40f;
        return content;
    }

    // 스크롤 목록 안의 칸: 위치를 목록 맨 위 기준으로 (y는 아래로 음수)
    private static void TopAnchor(RectTransform r)
    {
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
    }

    private static void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            GameObject child = t.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    // 목록 높이 맞추기 (스크롤 끝)
    private static void SetContentHeight(RectTransform content, float height)
    {
        float view = ((RectTransform)content.parent).rect.height;
        content.sizeDelta = new Vector2(content.sizeDelta.x, Mathf.Max(view, height));
    }

    private Button Clickable(Image image, UnityAction onClick)
    {
        image.raycastTarget = true;
        Button b = image.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        b.onClick.AddListener(PlayClick);
        if (onClick != null) b.onClick.AddListener(onClick);
        image.gameObject.AddComponent<MenuButtonFx>().target = b;
        return b;
    }

    // 특수 젬 그림 칸 (장착 E, 잠금 자물쇠, 선택 체크)
    private Image SpecialIconCell(Transform parent, GemSave.SpecialGem g, Vector2 pos, float size, bool selected, bool check, UnityAction onClick)
    {
        Image back = GemFrame("Cell " + g.id, parent, gc.slotDark, pos, Vector2.one * size, new Color(0.3f, 0.2f, 0.15f, 1f));
        back.color = selected ? SelectedTint : Color.white;
        if (selected)
        {
            // 선택 표시: 초록 테두리 + 살짝 크게
            Outline o = back.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.45f, 0.85f, 0.3f, 1f);
            o.effectDistance = new Vector2(5f, -5f);
            back.rectTransform.localScale = Vector3.one * 1.06f;
        }
        Icon("Icon", back.transform, SpecialSprite(g), Center, Vector2.zero, Vector2.one * size * 0.98f);
        if (GemSave.IsSpecialEquipped(g.id)) Icon("E", back.transform, gc.equippedBadge, Center, new Vector2(-size * 0.34f, size * 0.34f), Vector2.one * size * 0.28f);
        if (g.locked) Icon("Lock", back.transform, gc.lockBig, Center, new Vector2(size * 0.32f, -size * 0.3f), Vector2.one * size * 0.3f);
        if (check) Icon("Check", back.transform, cat.iconConfirm, Center, new Vector2(size * 0.32f, size * 0.32f), Vector2.one * size * 0.34f);
        Clickable(back, onClick);
        return back;
    }

    // 잠금 버튼 (잠금 = 금색 칸 + 자물쇠, 해제 = 흐린 자물쇠)
    private Button LockToggle(Transform parent, Vector2 pos, float size, bool locked, UnityAction onClick)
    {
        Image back = GemFrame("Lock Toggle", parent, gc.slotDark, pos, Vector2.one * size, new Color(0.3f, 0.2f, 0.15f, 1f));
        back.color = locked ? SelectedTint : Color.white;
        Image icon = Icon("Icon", back.transform, gc.lockBig, Center, Vector2.zero, Vector2.one * size * 0.72f);
        icon.color = locked ? Color.white : new Color(1f, 1f, 1f, 0.4f);
        if (locked)
        {
            Outline o = back.gameObject.AddComponent<Outline>();          // 잠금: 금빛 테두리
            o.effectColor = new Color(1f, 0.78f, 0.25f, 1f);
            o.effectDistance = new Vector2(4f, -4f);
        }
        return Clickable(back, onClick);
    }

    // 보유 특수 젬 그림 목록 (3칸씩)
    private void FillSpecialGrid(RectTransform content, System.Func<GemSave.SpecialGem, bool> selected, System.Func<GemSave.SpecialGem, bool> check, UnityAction<GemSave.SpecialGem> onClick)
    {
        ClearChildren(content);
        float cell = 150f, gap = 22f;
        List<GemSave.SpecialGem> list = GemSave.Current.specials;
        for (int i = 0; i < list.Count; i++)
        {
            GemSave.SpecialGem g = list[i];
            int r = i / 3, c = i % 3;
            Vector2 p = new Vector2((c - 1) * (cell + gap), -cell * 0.5f - 10f - r * (cell + gap));
            TopAnchor(SpecialIconCell(content, g, p, cell, selected(g), check(g), () => onClick(g)).rectTransform);
        }
        // 빈 칸 (발바닥 자리) 으로 최소 3줄 채움
        int rows = Mathf.Max(3, (list.Count + 2) / 3);
        for (int i = list.Count; i < rows * 3; i++)
        {
            int r = i / 3, c = i % 3;
            Image empty = GemFrame("Empty", content, gc.slotDark, new Vector2((c - 1) * (cell + gap), -cell * 0.5f - 10f - r * (cell + gap)), Vector2.one * cell, new Color(0.3f, 0.2f, 0.15f, 1f));
            TopAnchor(empty.rectTransform);
            empty.color = new Color(1f, 1f, 1f, 0.35f);
            Icon("Paw", empty.transform, cat.paw, Center, Vector2.zero, Vector2.one * cell * 0.36f).color = new Color(1f, 1f, 1f, 0.5f);
        }
        SetContentHeight(content, rows * (cell + gap) + 20f);
    }

    private void BuildSpecialPanels()
    {
        gemRerollPanel = BuildGemReroll();
        gemDecomposePanel = BuildGemDecompose();
        foreach (CanvasGroup g in new[] { gemRerollPanel, gemDecomposePanel })
        {
            g.alpha = 0f;
            g.blocksRaycasts = false;
            g.gameObject.SetActive(false);
        }
    }

    // ---------------- 수치 재설정 ----------------
    // 옵션 종류는 그대로, 수치만 다시 뽑음. 한 줄을 잠그면 그 줄은 유지하고 비용 x lockCostMul
    private RectTransform rerollGrid, rerollDetail;
    private int rerollSelected = -1;
    private int rerollLock = -1;                    // -1 없음, 0 증가 줄, 1 감소 줄
    private Text rerollCostText, rerollMessage;
    private Button rerollButton;

    private CanvasGroup BuildGemReroll()
    {
        RectTransform panel = NewPanel("Gem Reroll");
        Header(panel, "젬 재설정");
        ShardCounter(panel);

        Image left = GemFrame("Owned", panel, gc.paperPanel, new Vector2(-560f, 30f), new Vector2(600f, 520f), PaperTint);
        BandTitle(left.transform, 520f, "보유 특수 젬");
        rerollGrid = ScrollContent(left.transform, new Vector2(0f, -30f), new Vector2(560f, 430f));

        Image right = GemFrame("Reroll", panel, gc.paperPanel, new Vector2(330f, 30f), new Vector2(940f, 520f), PaperTint);
        BandTitle(right.transform, 520f, "특수 젬 재설정");
        rerollDetail = NewRect("Detail", right.transform, Center, Vector2.zero, new Vector2(940f, 520f));

        Image costBox = GemFrame("Cost", panel, PlainPaper, new Vector2(-170f, -295f), new Vector2(560f, 90f), PaperTint);
        Label("Title", costBox.transform, "필요 조각", 30, PaperText, TextAnchor.MiddleLeft, Center, new Vector2(-150f, 0f), new Vector2(200f, 60f), false);
        Image pill = Frame("Pill", costBox.transform, gc.buttonBrown, Vector4.zero, Color.white, Center, new Vector2(120f, 0f), new Vector2(260f, 64f));
        Icon("Icon", pill.transform, gc.shard, Center, new Vector2(-90f, 2f), new Vector2(46f, 46f));
        rerollCostText = Label("Cost", pill.transform, "", 32, Color.white, TextAnchor.MiddleCenter, Center, new Vector2(20f, 0f), new Vector2(180f, 60f), true);
        rerollButton = GemButton("Reroll", panel, "재설정", true, new Vector2(420f, -295f), new Vector2(400f, 86f), DoReroll, 36);

        BottomButton(panel, "돌아가기", false, false, cat.iconBack, () => Show(gemMainPanel));
        return panel.GetComponent<CanvasGroup>();
    }

    private void OnGemRerollOpen()
    {
        rerollLock = -1;
        if (GemSave.FindSpecial(rerollSelected) == null)
            rerollSelected = GemSave.Current.specials.Count > 0 ? GemSave.Current.specials[0].id : -1;
        RefreshGemReroll();
    }

    private void DoReroll()
    {
        if (!GemSave.RerollSpecial(rerollSelected, rerollLock)) return;
        RefreshGemReroll();
        for (int line = 0; line < 2; line++)
            if (line != rerollLock) UiFx.Play(UiFx.Kind.Sparkle, rerollDetail, new Vector2(255f, line == 0 ? 105f : -15f), 110f, null, 16f);
    }

    private void RefreshGemReroll()
    {
        RefreshShards();
        FillSpecialGrid(rerollGrid, g => g.id == rerollSelected, g => false, g =>
        {
            if (rerollSelected != g.id) rerollLock = -1;
            rerollSelected = g.id;
            RefreshGemReroll();
        });

        ClearChildren(rerollDetail);
        GemSave.SpecialGem sel = GemSave.FindSpecial(rerollSelected);
        int cost = GemSave.RerollCost(rerollLock);
        rerollCostText.text = cost.ToString();
        rerollCostText.color = GemSave.Current.shards >= cost ? Color.white : new Color(1f, 0.55f, 0.5f, 1f);
        SetDisabled(rerollButton, sel == null || GemSave.Current.shards < cost);
        if (sel == null)
        {
            GemLabel("None", rerollDetail, "보유한 특수 젬이 없습니다", 30, MutedText, Vector2.zero, new Vector2(700f, 60f));
            return;
        }

        // 큰 그림 + 이름
        Image big = Icon("Icon", rerollDetail, SpecialSprite(sel), Center, new Vector2(-325f, 40f), new Vector2(230f, 230f));
        GemCatalog.SpecialOption upOpt = gc.UpOption(sel.up);
        if (upOpt != null && sel.upValue == upOpt.to) UiFxLoop.Attach(big.rectTransform, Vector2.zero, 200f);   // 증가 옵션이 최대치일 때만 반짝임
        Text name = GemLabel("Name", rerollDetail, GemSave.SpecialName(sel), 26, PaperText, new Vector2(-325f, -110f), new Vector2(270f, 40f));
        FitOneLine(name, 26, 16, 270f);

        RerollRow(sel, 0, new Vector2(125f, 105f));
        RerollRow(sel, 1, new Vector2(125f, -15f));

        Image info = GemFrame("Info", rerollDetail, PlainPaper, new Vector2(125f, -150f), new Vector2(620f, 100f), PaperTint);
        info.color = new Color(0.93f, 0.85f, 0.74f, 1f);
        string msg = rerollLock < 0
            ? "증가·감소 옵션 수치를 둘 다 무작위로 다시 뽑습니다.\n(옵션 종류는 변경되지 않습니다)"
            : (rerollLock == 0 ? "증가" : "감소") + " 줄은 잠가 두고 나머지 줄 수치만 다시 뽑습니다.\n(잠금 재설정 비용 x" + gc.lockCostMul + ")";
        rerollMessage = GemLabel("Text", info.transform, msg, 20, PaperText, Vector2.zero, new Vector2(560f, 90f));
    }

    // 재설정 한 줄: 이름·수치, 범위, 범위 막대(현재 위치), 잠금 버튼
    private void RerollRow(GemSave.SpecialGem g, int line, Vector2 pos)
    {
        bool up = line == 0;
        GemCatalog.SpecialOption o = up ? gc.UpOption(g.up) : gc.DownOption(g.down);
        int value = up ? g.upValue : g.downValue;
        Color color = up ? gc.upColor : gc.downColor;

        Image row = GemFrame(up ? "Up Row" : "Down Row", rerollDetail, PlainPaper, pos, new Vector2(620f, 108f), PaperTint);
        if (rerollLock == line) row.color = SelectedTint;
        Text title = Label("Title", row.transform, up ? UpLine(g) : DownLine(g), 28, color, TextAnchor.MiddleLeft, Center, new Vector2(-105f, 20f), new Vector2(320f, 40f), false);
        FitOneLine(title, 28, 16, 320f);
        if (o == null) return;

        // 막대: 오른쪽일수록 좋음 (증가 = 약한 끝 → 강한 끝, 감소 = 큰 손해 → 약한 손해)
        int leftV = up ? o.from : o.to, rightV = up ? o.to : o.from;
        Label("Range", row.transform, "범위 : " + gc.OptionValue(o, o.from) + " ~ " + gc.OptionValue(o, o.to), 20, MutedText, TextAnchor.MiddleLeft, Center, new Vector2(-105f, -22f), new Vector2(320f, 30f), false);
        float barW = 160f;
        Vector2 barPos = new Vector2(140f, 10f);
        Frame("Track", row.transform, cat.sliderBar, Vector4.zero, Color.white, Center, barPos, new Vector2(barW, 22f));
        float t = rightV == leftV ? 1f : Mathf.Clamp01((value - leftV) / (float)(rightV - leftV));
        // 표시는 막대 양 끝 둥근 테두리 안쪽에서만 움직임 (끝값이어도 막대 밖으로 뚫고 나가지 않게)
        float inset = 12f, travel = barW - inset * 2f;
        float markerX = -barW * 0.5f + inset + travel * t;
        Image fill = Frame("Fill", row.transform, cat.sliderFill, Vector4.zero, color, Center, barPos, new Vector2(Mathf.Max(0f, markerX + barW * 0.5f - 4f), 14f));
        fill.rectTransform.pivot = new Vector2(0f, 0.5f);
        fill.rectTransform.anchoredPosition = barPos - new Vector2(barW * 0.5f - 4f, 0f);
        Icon("Marker", row.transform, cat.sliderHandle, Center, barPos + new Vector2(markerX, 0f), new Vector2(18f, 32f));
        Label("Left", row.transform, gc.OptionValue(o, leftV), 17, MutedText, TextAnchor.MiddleLeft, Center, barPos + new Vector2(-barW * 0.5f + 40f, -26f), new Vector2(80f, 24f), false);
        Label("Right", row.transform, gc.OptionValue(o, rightV), 17, MutedText, TextAnchor.MiddleRight, Center, barPos + new Vector2(barW * 0.5f - 40f, -26f), new Vector2(80f, 24f), false);

        // 잠금 (한 번에 한 줄만)
        LockToggle(row.transform, new Vector2(258f, 0f), 58f, rerollLock == line, () =>
        {
            rerollLock = rerollLock == line ? -1 : line;
            RefreshGemReroll();
        });
    }

    // ---------------- 분해 ----------------
    // 특수 젬 여러 개 → 조각 (잠금·장착 중인 젬은 선택 불가)
    private RectTransform decomposeGrid, decomposeList;
    private readonly HashSet<int> decomposeSelected = new HashSet<int>();
    private Text decomposeCountText, decomposeShardText;
    private Button decomposeButton, decomposeAllButton;

    private CanvasGroup BuildGemDecompose()
    {
        RectTransform panel = NewPanel("Gem Decompose");
        Header(panel, "젬 분해");
        ShardCounter(panel);

        Image left = GemFrame("Owned", panel, gc.paperPanel, new Vector2(-560f, 30f), new Vector2(600f, 520f), PaperTint);
        BandTitle(left.transform, 520f, "보유 특수 젬");
        decomposeGrid = ScrollContent(left.transform, new Vector2(0f, -30f), new Vector2(560f, 430f));

        Image right = GemFrame("List", panel, gc.paperPanel, new Vector2(330f, 30f), new Vector2(940f, 520f), PaperTint);
        BandTitle(right.transform, 520f, "분해할 젬 선택");
        decomposeList = ScrollContent(right.transform, new Vector2(0f, -30f), new Vector2(890f, 430f));

        Image bar = GemFrame("Summary", panel, PlainPaper, new Vector2(-235f, -295f), new Vector2(740f, 90f), PaperTint);
        Label("Selected", bar.transform, "선택한 젬", 28, PaperText, TextAnchor.MiddleLeft, Center, new Vector2(-255f, 0f), new Vector2(150f, 60f), false);
        Image countPill = Frame("Count Pill", bar.transform, gc.buttonBrown, Vector4.zero, Color.white, Center, new Vector2(-115f, 0f), new Vector2(140f, 60f));
        decomposeCountText = Label("Count", countPill.transform, "0개", 28, Color.white, TextAnchor.MiddleCenter, Center, Vector2.zero, new Vector2(130f, 56f), true);
        Icon("Arrow", bar.transform, gc.arrowRight, Center, new Vector2(-12f, 0f), new Vector2(30f, 44f));
        Label("Get", bar.transform, "획득 조각", 28, PaperText, TextAnchor.MiddleLeft, Center, new Vector2(100f, 0f), new Vector2(150f, 60f), false);
        Image shardPill = Frame("Shard Pill", bar.transform, gc.buttonBrown, Vector4.zero, Color.white, Center, new Vector2(262f, 0f), new Vector2(180f, 60f));
        Icon("Icon", shardPill.transform, gc.shard, Center, new Vector2(-62f, 2f), new Vector2(42f, 42f));
        decomposeShardText = Label("Shards", shardPill.transform, "0", 28, Color.white, TextAnchor.MiddleCenter, Center, new Vector2(18f, 0f), new Vector2(120f, 56f), true);
        decomposeAllButton = GemButton("Decompose All", panel, "일괄 분해", false, new Vector2(335f, -295f), new Vector2(290f, 86f), AskDecomposeAll, 32);
        decomposeButton = GemButton("Decompose", panel, "분해", true, new Vector2(645f, -295f), new Vector2(290f, 86f), AskDecompose, 36);

        BottomButton(panel, "돌아가기", false, false, cat.iconBack, () => Show(gemMainPanel));
        return panel.GetComponent<CanvasGroup>();
    }

    private void OnGemDecomposeOpen()
    {
        decomposeSelected.Clear();
        RefreshGemDecompose();
    }

    private void ToggleDecompose(GemSave.SpecialGem g)
    {
        if (!GemSave.CanDecompose(g)) return;
        if (!decomposeSelected.Remove(g.id)) decomposeSelected.Add(g.id);
        RefreshGemDecompose();
    }

    // 일괄 분해: 잠금·장착 중인 젬을 뺀 나머지 전부를 골라 한 번에 분해
    private void AskDecomposeAll()
    {
        decomposeSelected.Clear();
        foreach (GemSave.SpecialGem g in GemSave.Current.specials)
            if (GemSave.CanDecompose(g)) decomposeSelected.Add(g.id);
        RefreshGemDecompose();
        if (decomposeSelected.Count == 0) return;
        ConfirmPopup.Show("잠금·장착 중인 젬을 뺀 특수 젬 " + decomposeSelected.Count + "개를\n모두 분해할까냥? (조각 +" + decomposeSelected.Count * gc.shardPerGem + "개)", "분해한 젬은 되돌릴 수 없습니다.", "일괄 분해", DoDecompose, () =>
        {
            decomposeSelected.Clear();
            RefreshGemDecompose();
        });
    }

    private void AskDecompose()
    {
        if (decomposeSelected.Count == 0) return;
        ConfirmPopup.Show("특수 젬 " + decomposeSelected.Count + "개를 분해할까냥? (조각 +" + decomposeSelected.Count * gc.shardPerGem + "개)", "분해한 젬은 되돌릴 수 없습니다.", "분해", DoDecompose);
    }

    private void DoDecompose()
    {
        GemSave.Decompose(decomposeSelected);
        decomposeSelected.Clear();
        RefreshGemDecompose();
        if (shardTexts.Count > 0) UiFx.Play(UiFx.Kind.Sparkle, (RectTransform)shardTexts[shardTexts.Count - 1].transform.parent, Vector2.zero, 160f, null, 16f);
    }

    private void RefreshGemDecompose()
    {
        RefreshShards();
        decomposeSelected.RemoveWhere(id => !GemSave.CanDecompose(GemSave.FindSpecial(id)));
        FillSpecialGrid(decomposeGrid, g => false, g => decomposeSelected.Contains(g.id), ToggleDecompose);

        ClearChildren(decomposeList);
        List<GemSave.SpecialGem> list = GemSave.Current.specials;
        float rowH = 100f, gap = 10f;
        for (int i = 0; i < list.Count; i++)
        {
            GemSave.SpecialGem g = list[i];
            bool can = GemSave.CanDecompose(g);
            bool on = decomposeSelected.Contains(g.id);
            Image row = GemFrame("Row " + g.id, decomposeList, PlainPaper, new Vector2(0f, -rowH * 0.5f - 6f - i * (rowH + gap)), new Vector2(860f, rowH), PaperTint);
            TopAnchor(row.rectTransform);
            row.color = !can ? new Color(0.72f, 0.68f, 0.64f, 1f) : on ? SelectedTint : Color.white;
            Clickable(row, () => ToggleDecompose(g));

            Image box = GemFrame("Check", row.transform, gc.slotDark, new Vector2(-370f, 0f), Vector2.one * 56f, new Color(0.3f, 0.2f, 0.15f, 1f));
            box.color = can ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            if (on) Icon("Mark", box.transform, cat.iconConfirm, Center, new Vector2(4f, 6f), Vector2.one * 52f);
            Icon("Icon", row.transform, SpecialSprite(g), Center, new Vector2(-280f, 0f), Vector2.one * 92f);
            SpecialLines(row.transform, g, new Vector2(-30f, 0f), 380f, 24, 40f);
            if (!can) GemLabel("State", row.transform, g.locked ? "잠금" : "장착 중", 22, MutedText, new Vector2(250f, 0f), new Vector2(140f, 40f));
            LockToggle(row.transform, new Vector2(370f, 0f), 60f, g.locked, () =>
            {
                GemSave.SetSpecialLocked(g.id, !g.locked);
                RefreshGemDecompose();
            });
        }
        if (list.Count == 0) TopAnchor(GemLabel("None", decomposeList, "보유한 특수 젬이 없습니다", 30, MutedText, new Vector2(0f, -200f), new Vector2(700f, 60f)).rectTransform);
        SetContentHeight(decomposeList, list.Count * (rowH + gap) + 12f);

        decomposeCountText.text = decomposeSelected.Count + "개";
        decomposeShardText.text = (decomposeSelected.Count * gc.shardPerGem).ToString();
        SetDisabled(decomposeButton, decomposeSelected.Count == 0);
        SetDisabled(decomposeAllButton, !GemSave.Current.specials.Exists(GemSave.CanDecompose));
    }
}
