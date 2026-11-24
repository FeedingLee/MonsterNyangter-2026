using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// UI 에셋 준비 (에디터 전용, 메뉴 MonNyang > UI 에셋 다시 설정)
// - Morning_card: 아침 선택지 카드·머리 바·보상/리스크 상자·아이콘 틀·리본을 잘라 RunConfig 에 연결
// - Skill_button: 6x4 버튼 틀을 잘라 무기 스킬(금빛) / 필살기(붉은) / 회피(초록) 버튼과 위협 위치 표시 테두리에 연결
// 영역은 시트 픽셀 (왼쪽 위 기준으로 적고 Unity 좌표로 바꿈), border 는 늘릴 때 유지할 테두리
public static class UiAssetSetup
{
    private const string Dir = "Assets/Assets Folder/MHS Asset/MHS IMAGE/";
    private const string CardPath = Dir + "Morning_card.png";
    private const string ButtonPath = Dir + "Skill_button.png";

    private struct Part
    {
        public string name; public RectInt topLeft; public Vector4 border;
        public Part(string n, int x0, int y0, int x1, int y1, float b) { name = n; topLeft = new RectInt(x0, y0, x1 - x0, y1 - y0); border = Vector4.one * b; }
    }

    private static readonly Part[] CardParts =
    {
        new Part("card_paper", 678, 30, 986, 406, 28),
        new Part("bar_green", 608, 777, 798, 836, 18),    // 양피지 이름판 (세 등급 같은 판, 색은 글자로 구분)
        new Part("bar_yellow", 608, 777, 798, 836, 18),
        new Part("bar_red", 608, 777, 798, 836, 18),
        new Part("box_light", 30, 432, 361, 573, 22),
        new Part("box_green", 700, 432, 968, 573, 22),
        new Part("box_red", 988, 432, 1239, 573, 22),
        new Part("slot_light", 477, 866, 603, 993, 22),
        new Part("ribbon_light", 35, 858, 432, 924, 60),
        // 타이틀 메뉴 (MenuAssetSetup)
        new Part("card_dark", 1005, 27, 1231, 407, 28),          // 어두운 목재 판
        new Part("plate_light", 1250, 28, 1519, 96, 24),         // 장식 양피지 띠 (제목)
        new Part("button_green_big", 32, 598, 284, 671, 18),     // 초록 버튼
        new Part("button_brown_big", 1187, 602, 1505, 675, 18),  // 갈색 버튼
        new Part("arrow_left", 1382, 874, 1408, 915, 0),
        new Part("arrow_right", 1433, 874, 1459, 914, 0),
    };

    // Skill_button 칸 경계 (열, 줄)
    private static readonly int[] ColX = { 50, 264, 295, 509, 539, 754, 784, 998, 1030, 1243, 1273, 1486 };
    private static readonly int[] RowY = { 52, 256, 298, 502, 536, 737, 772, 974 };

    [MenuItem("MonNyang/UI 에셋 다시 설정")]
    public static void Run()
    {
        RunConfig rc = AssetDatabase.LoadAssetAtPath<RunConfig>("Assets/Resources/RunConfig.asset");
        if (rc == null) { Debug.LogError("[UI 에셋] RunConfig 없음"); return; }

        // 1) Morning_card
        Dictionary<string, Sprite> card = SliceCard();
        List<SpriteMetaData> list = new List<SpriteMetaData>();
        int H;
        rc.morningCardPaper = Get(card, "card_paper");
        rc.morningBarGreen = Get(card, "bar_green");
        rc.morningBarYellow = Get(card, "bar_yellow");
        rc.morningBarRed = Get(card, "bar_red");
        rc.morningBoxLight = Get(card, "box_light");
        rc.morningBoxGreen = Get(card, "box_green");
        rc.morningBoxRed = Get(card, "box_red");
        rc.morningSlot = Get(card, "slot_light");
        rc.morningRibbon = Get(card, "ribbon_light");
        rc.rerollPanel = rc.morningRibbon;

        // 2) Skill_button (6열 x 4줄: 베이지 / 초록 / 금빛 / 붉은, 오른쪽으로 갈수록 강조)
        list.Clear();
        H = TexHeight(ButtonPath);
        for (int r = 0; r < 4; r++)
            for (int c = 0; c < 6; c++)
                list.Add(Meta("button_" + r + "_" + c, new RectInt(ColX[c * 2], RowY[r * 2], ColX[c * 2 + 1] - ColX[c * 2], RowY[r * 2 + 1] - RowY[r * 2]), H, Vector4.one * 40f));
        Apply(ButtonPath, list);
        Dictionary<string, Sprite> btn = Load(ButtonPath);
        rc.skillButtonFrame = Get(btn, "button_2_2");      // 무기 스킬: 금빛
        rc.ultimateButtonFrame = Get(btn, "button_3_3");   // 필살기: 어두운 붉은 틀 (흰 글자가 잘 보이게)
        rc.dodgeButtonFrame = Get(btn, "button_1_2");      // 회피: 초록
        rc.threatIndicatorFrame = Get(btn, "button_3_5");  // 위협 위치 표시: 붉은 빛 테두리
        // 3) Select_icon: 왼쪽 = 무기 숙련(교차한 검), 오른쪽 = 밀어내기(휘두르기)
        string iconPath = Dir + "Select_icon.png";
        if (System.IO.File.Exists(iconPath))
        {
            list.Clear();
            H = TexHeight(iconPath);
            list.Add(Meta("icon_mastery", new RectInt(100, 150, 640, 620), H, Vector4.zero));
            list.Add(Meta("icon_knockback", new RectInt(850, 210, 900, 520), H, Vector4.zero));
            Apply(iconPath, list);
            Dictionary<string, Sprite> icons = Load(iconPath);
            rc.masteryIcon = Get(icons, "icon_mastery");
            rc.knockbackIcon = Get(icons, "icon_knockback");
        }
        EditorUtility.SetDirty(rc);

        MiniBossConfig mb = AssetDatabase.LoadAssetAtPath<MiniBossConfig>("Assets/Resources/MiniBossConfig.asset");
        if (mb != null && rc.threatIndicatorFrame != null)
        {
            mb.indicatorFrame = rc.threatIndicatorFrame;
            EditorUtility.SetDirty(mb);
        }
        // 코인 주머니 카드 아이콘 = 보상 상자 (닫힌 상자 첫 프레임)
        if (mb != null && mb.boxIdle != null && mb.boxIdle.Length > 0 && mb.boxIdle[0] != null)
        {
            rc.coinPouchIcon = mb.boxIdle[0];
            EditorUtility.SetDirty(rc);
        }

        // 3) 씬의 버튼에 연결
        foreach (UltimateManager u in Object.FindObjectsOfType<UltimateManager>(true))
        {
            u.buttonSprite = rc.ultimateButtonFrame;
            EditorUtility.SetDirty(u);
        }
        foreach (DodgeCooldownUI d in Object.FindObjectsOfType<DodgeCooldownUI>(true))
        {
            d.buttonSprite = rc.dodgeButtonFrame;
            EditorUtility.SetDirty(d);
        }
        foreach (GameManager gm in Object.FindObjectsOfType<GameManager>(true))
        {
            if (gm.mainWeapon != null) gm.mainWeapon.skillButtonSprite = rc.skillButtonFrame;
            EditorUtility.SetDirty(gm);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[UI 에셋] Morning_card, Skill_button 설정 완료");
    }

    // Morning_card 시트를 CardParts대로 자름 (기존 스프라이트 ID 유지)
    internal static Dictionary<string, Sprite> SliceCard()
    {
        List<SpriteMetaData> list = new List<SpriteMetaData>();
        int h = TexHeight(CardPath);
        foreach (Part p in CardParts) list.Add(Meta(p.name, p.topLeft, h, p.border));
        Apply(CardPath, list);
        return Load(CardPath);
    }

    internal static int TexHeight(string path)
    {
        Texture2D t = new Texture2D(2, 2);
        t.LoadImage(System.IO.File.ReadAllBytes(path));
        int h = t.height;
        Object.DestroyImmediate(t);
        return h;
    }

    internal static SpriteMetaData Meta(string name, RectInt tl, int texH, Vector4 border)
    {
        return new SpriteMetaData
        {
            name = name,
            rect = new Rect(tl.x, texH - tl.y - tl.height, tl.width, tl.height),
            alignment = (int)SpriteAlignment.Center,
            pivot = new Vector2(0.5f, 0.5f),
            border = border,
        };
    }

    internal static void Apply(string path, List<SpriteMetaData> list)
    {
        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        TextureImporterSettings st = new TextureImporterSettings();
        ti.ReadTextureSettings(st);
        st.spriteMeshType = SpriteMeshType.FullRect;
        ti.SetTextureSettings(st);

        var factory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
        factory.Init();
        var dp = factory.GetSpriteEditorDataProviderFromObject(ti);
        dp.InitSpriteEditorDataProvider();
        Dictionary<string, GUID> ids = new Dictionary<string, GUID>();
        foreach (SpriteRect old in dp.GetSpriteRects()) ids[old.name] = old.spriteID;
        List<SpriteRect> rects = new List<SpriteRect>();
        List<SpriteNameFileIdPair> pairs = new List<SpriteNameFileIdPair>();
        foreach (SpriteMetaData m in list)
        {
            SpriteRect r = new SpriteRect
            {
                name = m.name, rect = m.rect, alignment = (SpriteAlignment)m.alignment, pivot = m.pivot, border = m.border,
                spriteID = ids.TryGetValue(m.name, out GUID id) ? id : GUID.Generate(),
            };
            rects.Add(r);
            pairs.Add(new SpriteNameFileIdPair(r.name, r.spriteID));
        }
        dp.SetSpriteRects(rects.ToArray());
        dp.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteNameFileIdDataProvider>()?.SetNameFileIdPairs(pairs);
        dp.Apply();
        ti.SaveAndReimport();
    }

    internal static Dictionary<string, Sprite> Load(string path)
    {
        Dictionary<string, Sprite> map = new Dictionary<string, Sprite>();
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is Sprite s) map[s.name] = s;
        return map;
    }

    internal static Sprite Get(Dictionary<string, Sprite> map, string name) => map.TryGetValue(name, out Sprite s) ? s : null;
}
