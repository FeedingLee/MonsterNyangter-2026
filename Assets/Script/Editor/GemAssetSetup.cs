using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 젬 UI 에셋 준비 (에디터 전용, 메뉴 MonNyang > 젬 UI 에셋 설정)
// - Assets/Resources/GemCatalog 에셋이 없으면 만들고(수치는 GemCatalog 기본값), 스프라이트만 다시 연결
// - Normal_Gem_1: 일반 젬 16종 (위 공격 6 / 가운데 생존 4 / 아래 유틸 6, GemCatalog.gems 순서)
// - Gem_Icon: 조각, 자물쇠, E 표시, ? 젬 (공격·생존·유틸·특수)
// - Gem_shop_panel: 판넬, 띠, 분야 표시판, 버튼, 분야별 젬 칸, 잠긴 칸, + 칸, 화살표, 구분선
public static class GemAssetSetup
{
    private const string CatalogPath = "Assets/Resources/GemCatalog.asset";
    private const string Img = "Assets/Assets Folder/MHS Asset/MHS IMAGE/";
    private const int Pad = 4;

    private static readonly int[,] GemTop = { { 64, 299 }, { 350, 583 }, { 634, 869 }, { 919, 1152 }, { 1202, 1435 }, { 1485, 1719 } };
    private static readonly int[,] GemMid = { { 346, 580 }, { 631, 867 }, { 917, 1152 }, { 1202, 1438 } };
    private static readonly int[,] GemBot = { { 64, 297 }, { 346, 580 }, { 630, 866 }, { 915, 1148 }, { 1196, 1433 }, { 1483, 1718 } };

    private static readonly MenuAssetSetup.Part[] IconParts =
    {
        new MenuAssetSetup.Part("gem_shard", 78, 123, 286, 376),
        new MenuAssetSetup.Part("gem_lock", 346, 136, 575, 366),
        new MenuAssetSetup.Part("gem_equipped", 1494, 145, 1707, 358),
        new MenuAssetSetup.Part("gem_unknown_attack", 121, 453, 434, 770),
        new MenuAssetSetup.Part("gem_unknown_survival", 487, 453, 800, 770),
        new MenuAssetSetup.Part("gem_unknown_utility", 852, 453, 1167, 770),
        new MenuAssetSetup.Part("gem_unknown_special", 1207, 371, 1669, 826),
    };

    private static readonly MenuAssetSetup.Part[] PanelParts =
    {
        new MenuAssetSetup.Part("gp_wood", 15, 50, 579, 405, 48),
        new MenuAssetSetup.Part("gp_paper", 599, 89, 995, 376, 34),
        new MenuAssetSetup.Part("gp_dark", 1011, 86, 1349, 377, 34),
        new MenuAssetSetup.Part("gp_slot_dark", 1382, 70, 1514, 206),
        new MenuAssetSetup.Part("gp_slot_special", 1376, 231, 1519, 377),
        new MenuAssetSetup.Part("gp_banner", 12, 453, 350, 535, 70, 20, 70, 20),
        new MenuAssetSetup.Part("gp_tab_attack", 376, 456, 548, 533),
        new MenuAssetSetup.Part("gp_tab_survival", 565, 454, 730, 533),
        new MenuAssetSetup.Part("gp_tab_utility", 748, 456, 924, 533),
        new MenuAssetSetup.Part("gp_btn_green", 24, 587, 333, 669, 24),
        new MenuAssetSetup.Part("gp_btn_brown", 364, 587, 651, 669, 24),
        new MenuAssetSetup.Part("gp_btn_beige", 683, 591, 882, 664, 24),
        new MenuAssetSetup.Part("gp_btn_grey", 906, 590, 1109, 668, 24),
        new MenuAssetSetup.Part("gp_arrow_right", 1272, 591, 1326, 668),
        new MenuAssetSetup.Part("gp_slot_attack", 22, 730, 148, 862),
        new MenuAssetSetup.Part("gp_slot_survival", 162, 730, 286, 862),
        new MenuAssetSetup.Part("gp_slot_utility", 301, 730, 428, 862),
        new MenuAssetSetup.Part("gp_slot_special_frame", 432, 680, 625, 898),   // 보라 광채가 잘리지 않게 넉넉히
        new MenuAssetSetup.Part("gp_slot_locked", 627, 728, 755, 862),
        new MenuAssetSetup.Part("gp_slot_plus", 785, 730, 911, 859),
        new MenuAssetSetup.Part("gp_divider", 1149, 919, 1509, 976),
    };

    // Gem_shopper 프레임별 발 가운데 x (시트 픽셀), 줄별 발끝 y
    private static readonly int[] ShopperFeetX =
    {
        256, 590, 930, 1262, 1598, 1934,
        256, 593, 930, 1259, 1598, 1936,
        254, 591, 927, 1262, 1601, 1934,
    };
    private static readonly int[] ShopperFeetY = { 218, 462, 706 };

    private static readonly MenuAssetSetup.Part[] PaperParts =
    {
        new MenuAssetSetup.Part("gem_paper_band", 0, 0, 396, 287, 48, 40, 48, 64),
        new MenuAssetSetup.Part("gem_paper_plain", 0, 307, 396, 594, 44),
    };

    [MenuItem("MonNyang/젬 UI 에셋 설정")]
    public static void Run()
    {
        GemCatalog cat = AssetDatabase.LoadAssetAtPath<GemCatalog>(CatalogPath);
        if (cat == null)
        {
            cat = ScriptableObject.CreateInstance<GemCatalog>();
            AssetDatabase.CreateAsset(cat, CatalogPath);
        }

        List<MenuAssetSetup.Part> gemParts = new List<MenuAssetSetup.Part>();
        AddRow(gemParts, GemTop, 46, 287);
        AddRow(gemParts, GemMid, 322, 561);
        AddRow(gemParts, GemBot, 600, 841);
        Dictionary<string, Sprite> gems = MenuAssetSetup.Slice(Img + "Normal_Gem_1.png", gemParts.ToArray());
        for (int i = 0; i < cat.gems.Count && i < gemParts.Count; i++)
        {
            cat.gems[i].icon = UiAssetSetup.Get(gems, "normal_gem_" + i);
        }
        EnsureBonusPick(cat, gems);

        Dictionary<string, Sprite> icons = MenuAssetSetup.Slice(Img + "Gem_Icon.png", IconParts);
        cat.shard = UiAssetSetup.Get(icons, "gem_shard");
        cat.lockBig = UiAssetSetup.Get(icons, "gem_lock");
        cat.equippedBadge = UiAssetSetup.Get(icons, "gem_equipped");
        cat.unknownAttack = UiAssetSetup.Get(icons, "gem_unknown_attack");
        cat.unknownSurvival = UiAssetSetup.Get(icons, "gem_unknown_survival");
        cat.unknownUtility = UiAssetSetup.Get(icons, "gem_unknown_utility");
        cat.unknownSpecial = UiAssetSetup.Get(icons, "gem_unknown_special");
        // ? 특수 젬을 엠블렘 / 보라 광채로 나눈 복사본 (합성 전에는 광채 없이, 합성하면 광채가 피어남)
        Dictionary<string, Sprite> split = MenuAssetSetup.Slice(Img + "Gem_Unknown_Special_Split.png", new[]
        {
            new MenuAssetSetup.Part("gem_unknown_special_plain", 0, 0, 462, 455),
            new MenuAssetSetup.Part("gem_special_glow", 482, 0, 944, 455),
        });
        cat.unknownSpecialPlain = UiAssetSetup.Get(split, "gem_unknown_special_plain");
        cat.specialGlow = UiAssetSetup.Get(split, "gem_special_glow");

        Dictionary<string, Sprite> p = MenuAssetSetup.Slice(Img + "Gem_shop_panel.png", PanelParts);
        cat.woodPanel = UiAssetSetup.Get(MenuAssetSetup.SliceClean(), "wood_clean");   // 위 가로대의 잘린 판을 지운 복사본
        cat.darkPanel = UiAssetSetup.Get(p, "gp_dark");
        cat.slotDark = UiAssetSetup.Get(p, "gp_slot_dark");
        cat.slotSpecial = UiAssetSetup.Get(p, "gp_slot_special");
        cat.banner = UiAssetSetup.Get(p, "gp_banner");
        cat.tabAttack = UiAssetSetup.Get(p, "gp_tab_attack");
        cat.tabSurvival = UiAssetSetup.Get(p, "gp_tab_survival");
        cat.tabUtility = UiAssetSetup.Get(p, "gp_tab_utility");
        // 버튼은 Panel_2 금테 버튼 (메뉴 화면과 같은 그림)
        Dictionary<string, Sprite> p2 = MenuAssetSetup.SlicePanel2();
        cat.buttonGreen = UiAssetSetup.Get(p2, "p2_btn_green");
        cat.buttonBrown = UiAssetSetup.Get(p2, "p2_btn_brown");
        cat.buttonBeige = UiAssetSetup.Get(p2, "p2_btn_beige");
        cat.buttonGrey = UiAssetSetup.Get(p2, "p2_btn_grey");

        // 양피지 판: Gem_shop_panel 복사본 (위 띠의 다이아몬드를 지운 판 / 띠 없는 판, 원본은 그대로)
        Dictionary<string, Sprite> paper = MenuAssetSetup.Slice(Img + "Gem_Paper_Clean.png", PaperParts);
        cat.paperPanel = UiAssetSetup.Get(paper, "gem_paper_band");
        cat.plainPanel = UiAssetSetup.Get(paper, "gem_paper_plain");

        // 젬 상인 (Gem_shopper 6x3 프레임). 가로 2172라 최대 크기를 늘려 흐려지지 않게
        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(Img + "Gem_shopper.png");
        ti.maxTextureSize = 4096;
        List<MenuAssetSetup.Part> shopper = new List<MenuAssetSetup.Part>();
        // 프레임마다 발 가운데·발끝을 같은 자리에 맞춰 자름 (좌우로 꾸물거리지 않게)
        for (int i = 0; i < 18; i++)
        {
            int x0 = ShopperFeetX[i] - 212;
            int y0 = ShopperFeetY[i / 6] - 214;
            shopper.Add(new MenuAssetSetup.Part("gem_shopper_" + i, x0, y0, x0 + 337, y0 + 224));
        }
        Dictionary<string, Sprite> shop = MenuAssetSetup.Slice(Img + "Gem_shopper.png", shopper.ToArray());
        // 특수 젬 3종 (Unique_Gem_1: 공격 / 생존 / 유틸, 광채 포함 원본 그대로). 가로 2172라 최대 크기를 늘림
        TextureImporter uti = (TextureImporter)AssetImporter.GetAtPath(Img + "Unique_Gem_1.png");
        uti.maxTextureSize = 4096;
        Dictionary<string, Sprite> unique = MenuAssetSetup.Slice(Img + "Unique_Gem_1.png", new[]
        {
            new MenuAssetSetup.Part("special_gem_attack", 82, 44, 640, 676),
            new MenuAssetSetup.Part("special_gem_survival", 804, 44, 1364, 676),
            new MenuAssetSetup.Part("special_gem_utility", 1532, 44, 2087, 676),
        });
        cat.specialAttack = UiAssetSetup.Get(unique, "special_gem_attack");
        cat.specialSurvival = UiAssetSetup.Get(unique, "special_gem_survival");
        cat.specialUtility = UiAssetSetup.Get(unique, "special_gem_utility");
        // 광채(보라 빛·별)를 지운 복사본: 메인 화면 장착 칸 말고는 이쪽을 씀 (자르기 크기는 원본과 같음)
        Dictionary<string, Sprite> plain = MenuAssetSetup.Slice(Img + "Unique_Gem_Plain.png", new[]
        {
            new MenuAssetSetup.Part("special_gem_attack_plain", 0, 0, 558, 632),
            new MenuAssetSetup.Part("special_gem_survival_plain", 580, 0, 1140, 632),
            new MenuAssetSetup.Part("special_gem_utility_plain", 1160, 0, 1715, 632),
        });
        cat.specialAttackPlain = UiAssetSetup.Get(plain, "special_gem_attack_plain");
        cat.specialSurvivalPlain = UiAssetSetup.Get(plain, "special_gem_survival_plain");
        cat.specialUtilityPlain = UiAssetSetup.Get(plain, "special_gem_utility_plain");

        // 보석 전문가 앞 테이블
        Dictionary<string, Sprite> table = MenuAssetSetup.Slice(Img + "Table.png", new[] { new MenuAssetSetup.Part("gem_table", 100, 148, 1436, 932) });
        cat.shopperTable = UiAssetSetup.Get(table, "gem_table");

        cat.shopperFrames = new Sprite[shopper.Count];
        for (int i = 0; i < shopper.Count; i++) cat.shopperFrames[i] = UiAssetSetup.Get(shop, "gem_shopper_" + i);
        cat.arrowRight = UiAssetSetup.Get(p, "gp_arrow_right");
        cat.slotAttack = UiAssetSetup.Get(p, "gp_slot_attack");
        cat.slotSurvival = UiAssetSetup.Get(p, "gp_slot_survival");
        cat.slotUtility = UiAssetSetup.Get(p, "gp_slot_utility");
        cat.slotSpecialFrame = UiAssetSetup.Get(p, "gp_slot_special_frame");
        cat.slotLocked = UiAssetSetup.Get(p, "gp_slot_locked");
        cat.slotPlus = UiAssetSetup.Get(p, "gp_slot_plus");
        cat.divider = UiAssetSetup.Get(p, "gp_divider");

        Dictionary<string, Sprite> panel = UiAssetSetup.Load(Img + "Object_Panel.png");
        Dictionary<string, Sprite> logo = UiAssetSetup.Load(Img + "Logo_Title.png");
        cat.resultBar = UiAssetSetup.Get(panel, "Object_Panel_54");
        cat.questClear = UiAssetSetup.Get(logo, "Quest Clear");
        cat.questFailed = UiAssetSetup.Get(logo, "Quest Failed");

        EditorUtility.SetDirty(cat);
        AssetDatabase.SaveAssets();
        Debug.Log("[젬 UI 에셋] GemCatalog 설정 완료");
    }

    // 1118 필살기 위력 제거: 기존 에셋에 새 젬·옵션·능력치 이름을 넣고 필살기 위력은 풀에서 제외 (여러 번 실행해도 같음)
    private static void EnsureBonusPick(GemCatalog cat, Dictionary<string, Sprite> gemSprites)
    {
        foreach (GemCatalog.GemDef d in cat.gems)
            if (d.stat == RunStat.UltPower) d.retired = true;
        GemCatalog.GemDef bonus = cat.gems.Find(d => d.stat == RunStat.BonusPicks);
        if (bonus == null)
        {
            bonus = new GemCatalog.GemDef("보너스 선택 젬", StatCategory.Utility, RunStat.BonusPicks, cat.bonusPickPerGem);
            cat.gems.Add(bonus);                                   // 맨 끝 (저장 번호 유지)
        }
        bonus.value = cat.bonusPickPerGem;
        // 아이콘: Normal_Gem_1의 normal_gem_13 (필살기 위력 젬이 쓰던 그림)
        bonus.icon = UiAssetSetup.Get(gemSprites, "normal_gem_13");

        foreach (GemCatalog.SpecialOption o in cat.specialUpOptions)
            if (o.stat == RunStat.UltPower) o.retired = true;
        if (cat.specialUpOptions.Find(o => o.stat == RunStat.WeaponSize) == null)
            cat.specialUpOptions.Add(new GemCatalog.SpecialOption(RunStat.WeaponSize, StatCategory.Utility, 15, 25));

        StatCatalog stats = AssetDatabase.LoadAssetAtPath<StatCatalog>("Assets/Resources/StatCatalog.asset");
        if (stats != null && stats.stats.Find(d => d != null && d.stat == RunStat.BonusPicks) == null)
        {
            stats.stats.Add(new StatCatalog.StatDef(RunStat.BonusPicks, "보너스 선택", StatCategory.Utility, StatUnit.Count, StatDirection.Additive));
            EditorUtility.SetDirty(stats);
        }
    }

    private static void AddRow(List<MenuAssetSetup.Part> list, int[,] xs, int y0, int y1)
    {
        for (int i = 0; i < xs.GetLength(0); i++)
        {
            list.Add(new MenuAssetSetup.Part("normal_gem_" + list.Count, xs[i, 0] - Pad, y0 - Pad, xs[i, 1] + Pad, y1 + Pad));
        }
    }
}
