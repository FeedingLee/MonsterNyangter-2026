using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 타이틀 메뉴 에셋 준비 (에디터 전용, 메뉴 MonNyang > 메뉴 UI 에셋 설정)
// - Assets/Resources/MenuCatalog 에셋이 없으면 만들고(캐릭터·스테이지 문구는 MenuCatalog의 기본값), 스프라이트만 다시 연결
// - Morning_card 시트에 메뉴용 조각(어두운 판, 제목 띠, 큰 버튼, 화살표)을 추가로 자름 (UiAssetSetup.CardParts, 기존 ID 유지)
// - 캐릭터 서 있는 프레임은 각 캐릭터의 Stand 애니메이션, 필살기 아이콘은 인게임 필살기 버튼과 같은 그림
public static class MenuAssetSetup
{
    private const string CatalogPath = "Assets/Resources/MenuCatalog.asset";
    private const string Img = "Assets/Assets Folder/MHS Asset/MHS IMAGE/";
    private const string Anim = "Assets/Assets Folder/MHS Asset/MHS Animation/Player/";

    // Panel_1 / Icon_1 시트 조각 (왼쪽 위 기준 픽셀 x0, y0, x1, y1, 9-slice 테두리 왼쪽·아래·오른쪽·위)
    internal struct Part
    {
        public string name; public RectInt topLeft; public Vector4 border;
        public Part(string n, int x0, int y0, int x1, int y1, float l = 0f, float b = -1f, float r = -1f, float t = -1f)
        {
            name = n;
            topLeft = new RectInt(x0, y0, x1 - x0, y1 - y0);
            border = new Vector4(l, b < 0f ? l : b, r < 0f ? l : r, t < 0f ? (b < 0f ? l : b) : t);
        }
    }

    internal static readonly Part[] PanelParts =
    {
        new Part("panel_dark", 18, 34, 462, 368, 48),             // 메인 배경 패널
        new Part("panel_paper", 477, 44, 826, 362, 44),           // 정보 패널
        new Part("panel_ornate", 823, 434, 1193, 656, 56),        // 강조 패널
        new Part("banner_light", 28, 387, 483, 470, 100, 24, 100, 24),   // 타이틀 배너
        new Part("banner_brown", 25, 486, 487, 569, 100, 24, 100, 24),
        new Part("banner_notice", 10, 691, 541, 770, 95, 30, 95, 30),    // 고지 배너
        new Part("btn_green", 1218, 417, 1363, 478, 20),
        new Part("btn_brown", 1218, 486, 1363, 547, 20),
        new Part("btn_dark", 1377, 622, 1520, 681, 20),
        new Part("slot_dark", 596, 691, 694, 788, 24),
        new Part("slot_light", 723, 691, 820, 788, 24),
        new Part("slider_track", 858, 744, 1175, 779, 14, 10, 14, 10),
        new Part("slider_fill", 860, 689, 983, 718, 14, 10, 4, 10),
        new Part("slider_handle", 983, 678, 1010, 727),
        new Part("arrow_gold_left", 698, 824, 737, 888),
        new Part("arrow_gold_right", 755, 825, 794, 888),
        new Part("paw_light", 995, 827, 1058, 883),
        new Part("flag_red", 688, 909, 774, 1001),                // 붉은 깃발 (상점 특수 선택지 표시)
    };

    private static readonly Part[] IconParts =
    {
        new Part("icon_play", 109, 133, 279, 323),
        new Part("icon_gear", 366, 113, 601, 346),
        new Part("icon_exit", 676, 118, 889, 333),
        new Part("icon_back", 958, 132, 1175, 322),
        new Part("icon_check", 1246, 133, 1449, 308),
        new Part("cell_green", 110, 465, 280, 633),
        new Part("cell_dark", 383, 465, 554, 633),
        new Part("icon_lock", 655, 429, 860, 652),
        new Part("icon_gamepad", 927, 470, 1183, 633),
        new Part("icon_monitor", 1233, 442, 1474, 659),
        new Part("icon_speaker", 106, 730, 341, 932),
        new Part("icon_keyboard", 415, 753, 671, 916),
        new Part("icon_dots", 750, 790, 962, 865),
        new Part("icon_reset", 1037, 728, 1251, 935),
    };

    // Panel_2 시트 조각 (금테 버튼, 잎 장식 제목판). 메뉴·젬 화면이 같은 목록으로 자름
    internal static readonly Part[] Panel2Parts =
    {
        new Part("p2_btn_green", 22, 718, 294, 801, 28),
        new Part("p2_btn_brown", 311, 719, 554, 801, 28),
        new Part("p2_btn_beige", 572, 719, 760, 799, 28),
        new Part("p2_btn_grey", 781, 720, 966, 799, 28),
        new Part("p2_header_leaf", 1087, 502, 1501, 593),
    };

    // Menu_Frame_P2: Panel_2 나무 액자 복사본 (가운데 판·안쪽 양피지를 지운 화면 테두리, 원본은 그대로)
    internal static readonly Part[] MenuFrameParts =
    {
        new Part("menu_frame", 3, 13, 596, 373, 140, 86, 135, 170),
    };

    internal static Dictionary<string, Sprite> SlicePanel2() => Slice(Img + "Panel_2.png", Panel2Parts);

    // UI_Clean_Parts: 복사본 조각 (원본은 그대로)
    // - 젬 결과 목재 판 (Gem_shop_panel 목재 판, 위 가로대의 잘린 판·잎 제거)
    // - 제목판 (Panel_2 잎 장식판, 위쪽 발바닥 혹 제거 → 글자를 판 가운데에)
    internal static readonly Part[] CleanParts =
    {
        new Part("wood_clean", 0, 0, 564, 355, 48),
        new Part("header_clean", 584, 0, 998, 91),
    };

    internal static Dictionary<string, Sprite> SliceClean() => Slice(Img + "UI_Clean_Parts.png", CleanParts);

    internal static Dictionary<string, Sprite> Slice(string path, Part[] parts)
    {
        List<SpriteMetaData> list = new List<SpriteMetaData>();
        int h = UiAssetSetup.TexHeight(path);
        foreach (Part p in parts) list.Add(UiAssetSetup.Meta(p.name, p.topLeft, h, p.border));
        UiAssetSetup.Apply(path, list);
        return UiAssetSetup.Load(path);
    }

    [MenuItem("MonNyang/메뉴 UI 에셋 설정")]
    public static void Run()
    {
        MenuCatalog cat = AssetDatabase.LoadAssetAtPath<MenuCatalog>(CatalogPath);
        if (cat == null)
        {
            cat = ScriptableObject.CreateInstance<MenuCatalog>();
            AssetDatabase.CreateAsset(cat, CatalogPath);
        }

        Dictionary<string, Sprite> card = UiAssetSetup.SliceCard();
        Dictionary<string, Sprite> panel = UiAssetSetup.Load(Img + "Object_Panel.png");
        Dictionary<string, Sprite> button = UiAssetSetup.Load(Img + "Skill_button.png");
        Dictionary<string, Sprite> logo = UiAssetSetup.Load(Img + "Logo_Title.png");
        Dictionary<string, Sprite> weaponIcons = UiAssetSetup.Load(Img + "Object_Weapon&Icon.png");
        Dictionary<string, Sprite> gameIcons = UiAssetSetup.Load(Img + "Icon_InGame.png");
        Dictionary<string, Sprite> wiggler = UiAssetSetup.Load(Img + "Player_Wiggler.png");
        Dictionary<string, Sprite> p1 = Slice(Img + "Panel_1.png", PanelParts);
        Dictionary<string, Sprite> i1 = Slice(Img + "Icon_1.png", IconParts);

        cat.logo = UiAssetSetup.Get(logo, "Title Logo");
        Dictionary<string, Sprite> p2 = SlicePanel2();
        Dictionary<string, Sprite> frame = Slice(Img + "Menu_Frame_P2.png", MenuFrameParts);
        cat.screenFrame = UiAssetSetup.Get(frame, "menu_frame");          // 하위 화면 테두리 (Panel_2 나무 액자)
        cat.parchment = UiAssetSetup.Get(p1, "panel_paper");
        cat.ornatePanel = UiAssetSetup.Get(p1, "panel_ornate");
        cat.headerPlate = UiAssetSetup.Get(SliceClean(), "header_clean");  // 잎 장식 제목판 (발바닥 혹 제거 복사본)
        cat.noticeBanner = UiAssetSetup.Get(p1, "banner_notice");
        cat.buttonGreen = UiAssetSetup.Get(p2, "p2_btn_green");           // 금테 버튼
        cat.buttonBrown = UiAssetSetup.Get(p2, "p2_btn_brown");
        cat.arrowLeft = UiAssetSetup.Get(p1, "arrow_gold_left");
        cat.arrowRight = UiAssetSetup.Get(p1, "arrow_gold_right");
        cat.slot = UiAssetSetup.Get(p1, "slot_light");
        cat.rowNormal = UiAssetSetup.Get(card, "box_light");         // Morning_card 양피지 줄
        cat.rowSelected = UiAssetSetup.Get(card, "box_green");
        cat.rowLocked = UiAssetSetup.Get(p1, "btn_dark");
        cat.paw = UiAssetSetup.Get(p1, "paw_light");
        cat.statFilled = UiAssetSetup.Get(i1, "cell_green");
        cat.statEmpty = UiAssetSetup.Get(i1, "cell_dark");
        cat.divider = UiAssetSetup.Get(panel, "Inventory_line");
        cat.sliderBar = UiAssetSetup.Get(p1, "slider_track");
        cat.sliderFill = UiAssetSetup.Get(p1, "slider_fill");
        cat.sliderHandle = UiAssetSetup.Get(p1, "slider_handle");
        cat.bgmIcon = UiAssetSetup.Get(panel, "Object_Panel_26");     // 기존 옵션 창 배경음 아이콘
        cat.sfxIcon = UiAssetSetup.Get(panel, "Object_Panel_27");     // 기존 옵션 창 효과음 아이콘
        cat.lockIcon = UiAssetSetup.Get(i1, "icon_lock");
        cat.iconBack = UiAssetSetup.Get(i1, "icon_back");
        cat.iconConfirm = UiAssetSetup.Get(i1, "icon_check");
        cat.iconPlay = UiAssetSetup.Get(i1, "icon_play");
        cat.iconReset = UiAssetSetup.Get(i1, "icon_reset");
        cat.settingTabIcons = new[]
        {
            UiAssetSetup.Get(i1, "icon_gamepad"), UiAssetSetup.Get(i1, "icon_monitor"), UiAssetSetup.Get(i1, "icon_speaker"),
            UiAssetSetup.Get(i1, "icon_keyboard"), UiAssetSetup.Get(i1, "icon_dots"),
        };
        cat.cardSelected = UiAssetSetup.Get(button, "button_1_5");   // 초록 빛 테두리 (Skill_button)
        cat.cardNormal = UiAssetSetup.Get(p1, "slot_dark");          // 어두운 카드
        cat.cardLocked = UiAssetSetup.Get(p1, "slot_dark");

        // 캐릭터: Stand 애니메이션 프레임, 인게임 필살기 아이콘
        string[] stand = { "Palico/Palico_Stand.anim", "Gazabu/Gazabu_Stand.anim", "BowaBowa/BowaBowa_Stand.anim", "Wiggler/Wiggler_Stand.anim" };
        Sprite[] ultimate =
        {
            UiAssetSetup.Get(weaponIcons, "Object_Heal"),
            UiAssetSetup.Get(gameIcons, "Power_up"),
            UiAssetSetup.Get(weaponIcons, "Decoy_Icon"),
            UiAssetSetup.Get(wiggler, "Damage_1"),
        };
        foreach (MenuCatalog.CharacterEntry c in cat.characters)
        {
            if (c.playerId < 0 || c.playerId >= stand.Length)
                continue;
            Sprite[] frames = StandFrames(Anim + stand[c.playerId]);
            if (frames.Length > 0) c.idleFrames = frames;
            if (ultimate[c.playerId] != null) c.ultimateIcon = ultimate[c.playerId];
        }

        // 스테이지 1 대표 이미지
        Sprite stageImage = AssetDatabase.LoadAssetAtPath<Sprite>(Img + "Stage_Image_1.png");
        if (cat.stages.Count > 0 && cat.stages[0].image == null && stageImage != null) cat.stages[0].image = stageImage;

        // 메뉴 버튼 소리 = 기존 타이틀 시작 버튼 소리 (Select)
        // 바뀐 기본 수치 (에셋에 예전 값이 남아 있을 때)
        cat.defaultBgmVolume = 0.5f;
        cat.bgmOutputScale = 1.4f;
        cat.screenFrameTint = Color.white;
        cat.frameScale = 1f;
        cat.goldFrameWidth = 2f;
        cat.statCellSize = new Vector2(30f, 30f);

        if (cat.clickSound == null)
        {
            foreach (string guid in AssetDatabase.FindAssets("Select t:AudioClip"))
            {
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (clip != null && clip.name == "Select") { cat.clickSound = clip; break; }
            }
        }

        EditorUtility.SetDirty(cat);
        AssetDatabase.SaveAssets();
        Debug.Log("[메뉴 UI 에셋] MenuCatalog 설정 완료");
    }

    private static Sprite[] StandFrames(string path)
    {
        List<Sprite> frames = new List<Sprite>();
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
            return frames.ToArray();

        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            if (binding.propertyName != "m_Sprite")
                continue;
            foreach (ObjectReferenceKeyframe key in AnimationUtility.GetObjectReferenceCurve(clip, binding))
            {
                if (key.value is Sprite s && (frames.Count == 0 || frames[frames.Count - 1] != s)) frames.Add(s);
            }
        }
        return frames.ToArray();
    }
}
