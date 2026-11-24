using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 상점 UI 에셋 준비 (에디터 전용, 메뉴 MonNyang > 상점 UI 에셋 설정)
// - Assets/Resources/StatCatalog 에셋이 없으면 만들고(수치는 StatCatalog 기본값), 스프라이트만 다시 연결
// - Ingame_shop: 판넬, 등급별 카드, 고양이 3장(자는 고양이 제외), 랜턴, 코인 띠, 버튼, 구분선, 발바닥
// - Ingame_shop_icon_1: 상점 능력치 14종 (shopStats 순서), Ingame_shop_icon_2: 골드 특수 선택지 9종 (specials 순서)
public static class ShopAssetSetup
{
    private const string CatalogPath = "Assets/Resources/StatCatalog.asset";
    private const string Img = "Assets/Assets Folder/MHS Asset/MHS IMAGE/";

    private static readonly MenuAssetSetup.Part[] ShopParts =
    {
        new MenuAssetSetup.Part("shop_panel", 10, 222, 812, 750),
        new MenuAssetSetup.Part("shop_card_bronze", 822, 330, 1058, 648),
        new MenuAssetSetup.Part("shop_card_silver", 1064, 330, 1296, 648),
        new MenuAssetSetup.Part("shop_card_gold", 1298, 322, 1532, 664),
        new MenuAssetSetup.Part("shop_cat_idle", 40, 15, 272, 212),
        new MenuAssetSetup.Part("shop_cat_surprise", 292, 8, 522, 212),
        new MenuAssetSetup.Part("shop_cat_wave", 534, 18, 742, 212),     // 왼쪽 옆 고양이의 느낌표가 걸리지 않게
        new MenuAssetSetup.Part("shop_lantern", 1020, 12, 1216, 264),
        new MenuAssetSetup.Part("shop_shelf", 1205, 90, 1515, 297),
        new MenuAssetSetup.Part("shop_plate_light", 842, 687, 1098, 766, 30),
        new MenuAssetSetup.Part("shop_bar_dark", 1128, 694, 1509, 757, 28),
        new MenuAssetSetup.Part("shop_button_brown", 847, 783, 1144, 865, 24),
        new MenuAssetSetup.Part("shop_button_green", 1187, 780, 1492, 865, 24),
        new MenuAssetSetup.Part("shop_divider", 804, 901, 1047, 934),
        new MenuAssetSetup.Part("shop_paw", 1119, 890, 1186, 949),
    };

    // 아이콘 시트 칸 (왼쪽 위 기준 x0, y0, x1, y1)
    private static readonly int[] Icon1CenterTop = { 153, 400, 645, 885, 1142, 1380, 1632 };      // 위 줄: 공격력 ~ 최대 체력
    private static readonly int[] Icon1CenterBottom = { 145, 400, 652, 890, 1135, 1385, 1621 };   // 아래 줄: 받는 피해 ~ 무기·총알 크기
    private static readonly RectInt[] Icon2Cells =
    {
        new RectInt(70, 20, 360, 325), new RectInt(490, 20, 340, 325), new RectInt(895, 20, 365, 325),
        new RectInt(1295, 20, 365, 325), new RectInt(1740, 20, 350, 325),
        new RectInt(200, 365, 320, 330), new RectInt(595, 365, 365, 330), new RectInt(1000, 365, 400, 330),
        new RectInt(1455, 365, 435, 330),
    };

    [MenuItem("MonNyang/상점 UI 에셋 설정")]
    public static void Run()
    {
        StatCatalog cat = AssetDatabase.LoadAssetAtPath<StatCatalog>(CatalogPath);
        if (cat == null)
        {
            cat = ScriptableObject.CreateInstance<StatCatalog>();
            AssetDatabase.CreateAsset(cat, CatalogPath);
        }

        Dictionary<string, Sprite> shop = MenuAssetSetup.Slice(Img + "Ingame_shop.png", ShopParts);

        List<MenuAssetSetup.Part> icon1 = new List<MenuAssetSetup.Part>();
        for (int i = 0; i < 14; i++)
        {
            bool top = i < 7;
            int c = top ? Icon1CenterTop[i] : Icon1CenterBottom[i - 7];
            icon1.Add(top ? new MenuAssetSetup.Part("stat_icon_" + i, c - 125, 180, c + 125, 440)
                          : new MenuAssetSetup.Part("stat_icon_" + i, c - 125, 480, c + 125, 745));
        }
        Dictionary<string, Sprite> statIcons = MenuAssetSetup.Slice(Img + "Ingame_shop_icon_1.png", icon1.ToArray());

        List<MenuAssetSetup.Part> icon2 = new List<MenuAssetSetup.Part>();
        for (int i = 0; i < Icon2Cells.Length; i++)
        {
            RectInt r = Icon2Cells[i];
            icon2.Add(new MenuAssetSetup.Part("special_icon_" + i, r.x, r.y, r.xMax, r.yMax));
        }
        Dictionary<string, Sprite> specialIcons = MenuAssetSetup.Slice(Img + "Ingame_shop_icon_2.png", icon2.ToArray());

        Dictionary<string, Sprite> p1 = MenuAssetSetup.Slice(Img + "Panel_1.png", MenuAssetSetup.PanelParts);   // 붉은 깃발 포함
        Dictionary<string, Sprite> i1 = UiAssetSetup.Load(Img + "Icon_1.png");

        cat.shopPanel = UiAssetSetup.Get(shop, "shop_panel");
        cat.cardBronze = UiAssetSetup.Get(shop, "shop_card_bronze");
        cat.cardSilver = UiAssetSetup.Get(shop, "shop_card_silver");
        cat.cardGold = UiAssetSetup.Get(shop, "shop_card_gold");
        cat.catIdle = UiAssetSetup.Get(shop, "shop_cat_idle");
        cat.catSurprise = UiAssetSetup.Get(shop, "shop_cat_surprise");
        cat.catWave = UiAssetSetup.Get(shop, "shop_cat_wave");
        cat.lantern = UiAssetSetup.Get(shop, "shop_lantern");
        cat.coinBar = UiAssetSetup.Get(shop, "shop_bar_dark");
        cat.buttonBrown = UiAssetSetup.Get(shop, "shop_button_brown");
        cat.buttonGreen = UiAssetSetup.Get(shop, "shop_button_green");
        cat.divider = UiAssetSetup.Get(shop, "shop_divider");
        cat.paw = UiAssetSetup.Get(shop, "shop_paw");
        cat.specialTag = UiAssetSetup.Get(p1, "flag_red");
        cat.titlePlate = UiAssetSetup.Get(shop, "shop_plate_light");
        cat.iconExit = UiAssetSetup.Get(i1, "icon_back");
        cat.iconSelect = UiAssetSetup.Get(i1, "icon_check");
        cat.coinIcon = RunConfig.Instance != null ? RunConfig.Instance.coinIcon : null;

        // 능력치 아이콘: 상점 능력치 순서 = 아이콘 시트 순서
        for (int i = 0; i < cat.shopStats.Count && i < 14; i++)
        {
            StatCatalog.StatDef def = cat.Def(cat.shopStats[i].stat);
            if (def != null) def.icon = UiAssetSetup.Get(statIcons, "stat_icon_" + i);
        }
        cat.specialIcons = new Sprite[Icon2Cells.Length];
        for (int i = 0; i < Icon2Cells.Length; i++)
        {
            cat.specialIcons[i] = UiAssetSetup.Get(specialIcons, "special_icon_" + i);
            if (i < cat.specials.Count) cat.specials[i].icon = cat.specialIcons[i];
        }

        // 배치 기본값 (화면에서 확인 후 맞춘 값)
        cat.panelPosition = new Vector2(0f, -40f);
        cat.panelHeight = 600f;
        cat.subtitleY = 192f;
        cat.catPosition = new Vector2(-285f, 284f);
        cat.catHeight = 140f;
        cat.lanternPosition = new Vector2(-405f, 262f);
        cat.lanternHeight = 160f;
        cat.coinBarPosition = new Vector2(320f, 292f);
        cat.cardY = 0f;
        cat.cardHeight = 320f;
        cat.cardSpacing = 285f;
        cat.buttonSize = new Vector2(240f, 62f);
        cat.buttonX = 285f;
        cat.buttonY = -214f;

        // 이름 변경 (전용 스킬 → 무기 스킬)
        foreach (StatCatalog.StatDef d in cat.stats)
        {
            if (d.stat == RunStat.SkillPower) d.name = "무기 스킬 피해";
            else if (d.stat == RunStat.SkillCooldown) d.name = "무기 스킬 쿨타임";
        }

        EditorUtility.SetDirty(cat);
        AssetDatabase.SaveAssets();
        Debug.Log("[상점 UI 에셋] StatCatalog 설정 완료");
    }
}
