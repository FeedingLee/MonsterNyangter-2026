using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 레벨업 UI 에셋 준비 (에디터 전용, 메뉴 MonNyang > 레벨업 UI 에셋 설정)
// - Assets/Resources/GimmickCatalog 에셋이 없으면 만들고(수치는 GimmickCatalog 기본값), 스프라이트만 다시 연결
// - Ingame_Levelup_panel: 목재 프레임 판넬, 제목 띠, 칸 묶음 판, 카드, 레벨 표시판, 묶음 표시(검·발바닥), 구분선, 잎, 버튼
// - Ingame_Levelup_icon: 전투 기믹 14종 (C 항목 번호 순서, 위 줄 1~7 / 아래 줄 8~14)
public static class LevelUpAssetSetup
{
    private const string CatalogPath = "Assets/Resources/GimmickCatalog.asset";
    private const string Img = "Assets/Assets Folder/MHS Asset/MHS IMAGE/";

    private static readonly MenuAssetSetup.Part[] PanelParts =
    {
        new MenuAssetSetup.Part("lv_frame", 94, 38, 762, 516, 48),
        new MenuAssetSetup.Part("lv_header", 826, 17, 1468, 126, 140, 20, 140, 20),
        new MenuAssetSetup.Part("lv_box", 794, 240, 1479, 492, 40),
        new MenuAssetSetup.Part("lv_card", 1055, 520, 1479, 940, 40, 115, 40, 115),
        new MenuAssetSetup.Part("lv_tag", 799, 145, 1019, 217, 22),
        new MenuAssetSetup.Part("lv_badge_weapon", 66, 538, 176, 717),
        new MenuAssetSetup.Part("lv_badge_gimmick", 66, 736, 174, 838),
        new MenuAssetSetup.Part("lv_divider", 295, 868, 995, 900),
        new MenuAssetSetup.Part("lv_leaf", 63, 860, 137, 914),
        new MenuAssetSetup.Part("lv_button_green", 108, 938, 350, 1008, 26),
        new MenuAssetSetup.Part("lv_button_brown", 375, 938, 585, 1008, 26),
        new MenuAssetSetup.Part("lv_button_grey", 613, 938, 836, 1008, 26),
    };

    // 기믹 아이콘 칸 (왼쪽 위 기준 x0, x1). 그림마다 폭이 달라 칸을 따로 지정 (밀어내기 오른쪽 잘림, 관통 화살촉 양옆 잔여 픽셀 방지)
    private static readonly int[] IconTopX = { 30, 298, 300, 520, 518, 766, 768, 1004, 1006, 1246, 1250, 1492, 1496, 1746 };
    private static readonly int[] IconBottomX = { 22, 270, 270, 516, 520, 752, 758, 1002, 1006, 1244, 1250, 1494, 1494, 1748 };

    [MenuItem("MonNyang/레벨업 UI 에셋 설정")]
    public static void Run()
    {
        GimmickCatalog cat = AssetDatabase.LoadAssetAtPath<GimmickCatalog>(CatalogPath);
        if (cat == null)
        {
            cat = ScriptableObject.CreateInstance<GimmickCatalog>();
            AssetDatabase.CreateAsset(cat, CatalogPath);
        }

        Dictionary<string, Sprite> panel = MenuAssetSetup.Slice(Img + "Ingame_Levelup_panel.png", PanelParts);
        List<MenuAssetSetup.Part> iconParts = new List<MenuAssetSetup.Part>();
        for (int i = 0; i < 14; i++)
        {
            bool top = i < 7;
            int[] xs = top ? IconTopX : IconBottomX;
            int k = (i % 7) * 2;
            iconParts.Add(top ? new MenuAssetSetup.Part("gimmick_icon_" + i, xs[k], 175, xs[k + 1], 432)
                              : new MenuAssetSetup.Part("gimmick_icon_" + i, xs[k], 468, xs[k + 1], 724));
        }
        Dictionary<string, Sprite> icons = MenuAssetSetup.Slice(Img + "Ingame_Levelup_icon.png", iconParts.ToArray());
        Dictionary<string, Sprite> p1 = UiAssetSetup.Load(Img + "Panel_1.png");
        Dictionary<string, Sprite> i1 = UiAssetSetup.Load(Img + "Icon_1.png");

        cat.frame = UiAssetSetup.Get(panel, "lv_frame");
        cat.header = UiAssetSetup.Get(panel, "lv_header");
        cat.box = UiAssetSetup.Get(panel, "lv_box");
        cat.card = UiAssetSetup.Get(panel, "lv_card");
        cat.levelTag = UiAssetSetup.Get(panel, "lv_tag");
        cat.badgeWeapon = UiAssetSetup.Get(panel, "lv_badge_weapon");
        cat.badgeGimmick = UiAssetSetup.Get(panel, "lv_badge_gimmick");
        cat.divider = UiAssetSetup.Get(panel, "lv_divider");
        cat.leaf = UiAssetSetup.Get(panel, "lv_leaf");
        cat.buttonGreen = UiAssetSetup.Get(panel, "lv_button_green");
        cat.buttonBrown = UiAssetSetup.Get(panel, "lv_button_brown");
        cat.buttonGrey = UiAssetSetup.Get(panel, "lv_button_grey");
        cat.slot = UiAssetSetup.Get(p1, "slot_light");
        cat.rerollIcon = UiAssetSetup.Get(i1, "icon_reset");
        cat.selectIcon = UiAssetSetup.Get(i1, "icon_check");
        cat.icons = new Sprite[14];
        for (int i = 0; i < 14; i++) cat.icons[i] = UiAssetSetup.Get(icons, "gimmick_icon_" + i);

        // 보호막·냉기 이펙트 (Shield&ice_effact: 위 줄 막 8장, 아래 줄 얼음 바닥 8장)
        List<MenuAssetSetup.Part> fx = new List<MenuAssetSetup.Part>();
        for (int i = 0; i < 8; i++)
        {
            int x0 = Mathf.RoundToInt(i * 221.75f), x1 = Mathf.RoundToInt((i + 1) * 221.75f);
            fx.Add(new MenuAssetSetup.Part("shield_" + i, x0, 170, x1, 430));
            fx.Add(new MenuAssetSetup.Part("frost_" + i, x0, 520, x1, 735));
        }
        Dictionary<string, Sprite> fxSprites = MenuAssetSetup.Slice(Img + "Shield&ice_effact.png", fx.ToArray());
        // 프레임마다 그림 위치가 달라 넘기면 좌우로 꿀렁거림 → 가장 또렷한 한 장만 쓰고 투명도로 숨쉬기
        cat.shieldFrames = Frames(fxSprites, "shield_", 4, 5);
        cat.frostFrames = Frames(fxSprites, "frost_", 4, 5);

        // 연쇄 번개 (lightning_effect: 8 x 2칸, 왼쪽 위부터)
        List<MenuAssetSetup.Part> bolt = new List<MenuAssetSetup.Part>();
        for (int i = 0; i < 16; i++)
        {
            int col = i % 8, row = i / 8;
            int x0 = Mathf.RoundToInt(col * 271.5f), x1 = Mathf.RoundToInt((col + 1) * 271.5f);
            bolt.Add(new MenuAssetSetup.Part("lightning_" + i, x0, row * 362, x1, row * 362 + 362));
        }
        Dictionary<string, Sprite> boltSprites = MenuAssetSetup.Slice(Img + "lightning_effect.png", bolt.ToArray());
        cat.lightningFrames = Frames(boltSprites, "lightning_", 0, 16);

        // 슈퍼 그물망: Object_Net의 붉은 복사본 (원본 png는 그대로, 포획용 그물은 원래 색)
        cat.superMagnetIcon = RedNetCopy();

        // 배치 기본값 (텍스트 잘림·패널 겹침 수정)
        cat.panelSize = new Vector2(1060f, 1010f);
        cat.panelPosition = new Vector2(0f, -4f);
        cat.headerSize = new Vector2(880f, 92f);
        cat.headerFontSize = 32;
        cat.weaponBoxPosition = new Vector2(0f, 300f);
        cat.weaponSlotSize = new Vector2(96f, 96f);
        cat.weaponSlotGap = new Vector2(16f, 34f);
        cat.gimmickBoxPosition = new Vector2(0f, 26f);
        cat.gimmickSlotSize = new Vector2(80f, 80f);
        cat.gimmickSlotGap = new Vector2(14f, 34f);
        cat.cardSize = new Vector2(300f, 362f);
        cat.cardY = -294f;
        cat.cardSpacing = 322f;
        cat.cardDescFontSize = 20;
        cat.confirmSize = new Vector2(270f, 60f);
        cat.confirmY = -494f;
        cat.rerollX = 300f;
        cat.cardTagSize = new Vector2(214f, 66f);
        cat.lightningWidth = 1.8f;
        cat.crisisPushForceMul = 2.25f;

        EditorUtility.SetDirty(cat);
        AssetDatabase.SaveAssets();
        Debug.Log("[레벨업 UI 에셋] GimmickCatalog 설정 완료");
    }

    private static Sprite[] Frames(Dictionary<string, Sprite> map, string prefix, int from, int to)
    {
        List<Sprite> list = new List<Sprite>();
        for (int i = from; i < to; i++)
        {
            Sprite s = UiAssetSetup.Get(map, prefix + i);
            if (s != null) list.Add(s);
        }
        return list.ToArray();
    }

    private const string RedNetPath = Img + "Object_Net_Red.png";

    // Object_Net 그림을 잘라 색만 붉게 바꾼 복사본 png를 만들고 스프라이트로 가져옴 (이미 있으면 그대로 사용)
    private static Sprite RedNetCopy()
    {
        Sprite net = null;
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(Img + "Object_Weapon&Icon.png"))
            if (o is Sprite s && s.name == "Object_Net") net = s;
        if (net == null)
            return null;

        if (!System.IO.File.Exists(RedNetPath))
        {
            Texture2D src = new Texture2D(2, 2);
            src.LoadImage(System.IO.File.ReadAllBytes(Img + "Object_Weapon&Icon.png"));
            RectInt r = new RectInt((int)net.rect.x, (int)net.rect.y, (int)net.rect.width, (int)net.rect.height);
            Texture2D dst = new Texture2D(r.width, r.height, TextureFormat.RGBA32, false);
            for (int y = 0; y < r.height; y++)
                for (int x = 0; x < r.width; x++)
                {
                    Color c = src.GetPixel(r.x + x, r.y + y);
                    Color.RGBToHSV(c, out float h, out float sat, out float v);
                    if (c.a > 0f && v > 0.22f)
                    {
                        Color red = Color.HSVToRGB(0.0f, Mathf.Lerp(0.85f, 0.45f, v), Mathf.Lerp(0.55f, 1f, v));   // 어두운 외곽선은 그대로, 나머지는 밝기를 살린 붉은색
                        red.a = c.a;
                        c = red;
                    }
                    dst.SetPixel(x, y, c);
                }
            dst.Apply();
            System.IO.File.WriteAllBytes(RedNetPath, dst.EncodeToPNG());
            Object.DestroyImmediate(src);
            Object.DestroyImmediate(dst);
            AssetDatabase.ImportAsset(RedNetPath);
            TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(RedNetPath);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.spritePixelsPerUnit = net.pixelsPerUnit;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(RedNetPath);
    }
}
