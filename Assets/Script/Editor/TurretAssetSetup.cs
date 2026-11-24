using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 포탑 시트 4장 분할 (Turret_A~D, 원본 png는 그대로, Sprite Editor 분할만) + TurretConfig 그림·효과음 지정
// 칸 간격이 불규칙해 칸마다 불투명 영역으로 자르고, 받침이 같은 자리에 오도록 칸마다 기준점(pivot)을 맞춤
// 회전 머리는 무기 몸통(석궁 65%, 발리스타·화룡포 60% 높이)을 중심으로 돎. Build_turret은 제작 발판 한 장
public static class TurretAssetSetup
{
    private const string Img = "Assets/Assets Folder/MHS Asset/MHS IMAGE/";
    private const string Sfx = "Assets/Assets Folder/MHS Asset/MHS SFX/";
    private const string ConfigPath = "Assets/Resources/TurretConfig.asset";

    private struct P
    {
        public string name; public int x0, y0, x1, y1; public float px, py;
        public P(string n, int a, int b, int c, int d, float pivotX, float pivotY) { name = n; x0 = a; y0 = b; x1 = c; y1 = d; px = pivotX; py = pivotY; }
    }

    // 머리 그림 아래 깃발 잘라내기: 회전 중심(pivot)에서 이만큼(픽셀) 아래까지만 씀 → 받침의 깃발 하나만 보임 (원본 그림은 그대로, 자르는 범위만)
    private static readonly Dictionary<string, int> HeadCutBelowPivot = new Dictionary<string, int> { { "ta", 72 }, { "tc", 75 }, { "td", 78 } };

    private static P CutHead(P p)
    {
        int us = p.name.IndexOf('_');
        if (us < 0 || !p.name.Contains("_head_") || !HeadCutBelowPivot.TryGetValue(p.name.Substring(0, us), out int cut))
            return p;
        float pivotFromTop = p.y1 - p.py * (p.y1 - p.y0);
        int y1 = Mathf.Min(p.y1, Mathf.RoundToInt(pivotFromTop + cut));
        if (y1 <= p.y0 + 4) return p;
        return new P(p.name, p.x0, p.y0, p.x1, y1, p.px, (y1 - pivotFromTop) / (y1 - p.y0));
    }

    // 좌표는 그림 왼쪽 위 기준, pivot은 칸 안 비율 (아래 = 0)
    private static readonly Dictionary<string, P[]> Sheets = new Dictionary<string, P[]>
    {
        { "Turret_A", new[] {
            new P("ta_base_0", 122, 114, 338, 260, 0.5093f, 0.0137f),
            new P("ta_head_0", 428, 78, 620, 264, 0.4453f, 0.6468f),
            new P("ta_head_1", 704, 78, 898, 264, 0.4691f, 0.6468f),
            new P("ta_head_2", 118, 338, 322, 510, 0.5172f, 0.6994f),
            new P("ta_head_3", 420, 314, 660, 510, 0.4271f, 0.6138f),
            new P("ta_head_4", 718, 336, 916, 512, 0.4874f, 0.6835f),
            new P("ta_sleep_0", 118, 570, 334, 792, 0.5023f, 0.0090f),
            new P("ta_arrow_0", 126, 898, 314, 954, 0.5000f, 0.5000f),
            new P("ta_hit_0", 466, 850, 634, 992, 0.5000f, 0.5000f),
            new P("ta_hit_1", 748, 834, 938, 996, 0.5000f, 0.5000f),
        } },
        { "Turret_B", new[] {
            new P("tb_body_0", 128, 44, 346, 230, 0.4977f, 0.0108f),
            new P("tb_body_1", 410, 42, 626, 230, 0.5000f, 0.0106f),
            new P("tb_body_2", 124, 254, 346, 470, 0.5113f, 0.0093f),
            new P("tb_body_3", 426, 258, 646, 470, 0.4977f, 0.0094f),
            new P("tb_body_4", 718, 292, 988, 470, 0.4130f, 0.0112f),
            new P("tb_body_5", 1048, 290, 1274, 470, 0.5000f, 0.0111f),
            new P("tb_sleep_0", 126, 500, 348, 702, 0.5000f, 0.0099f),
            new P("tb_blob_0", 160, 756, 280, 818, 0.5000f, 0.5000f),
            new P("tb_blob_1", 372, 746, 520, 832, 0.5000f, 0.5000f),
            new P("tb_splash_0", 612, 724, 758, 834, 0.5000f, 0.5000f),
            new P("tb_splash_1", 856, 702, 1048, 840, 0.5000f, 0.5000f),
            new P("tb_pool_0", 128, 902, 294, 996, 0.5000f, 0.5000f),
            new P("tb_pool_1", 408, 878, 662, 1008, 0.5000f, 0.5000f),
            new P("tb_pool_2", 738, 886, 1014, 1002, 0.5000f, 0.5000f),
        } },
        { "Turret_C", new[] {
            new P("tc_base_0", 102, 82, 362, 270, 0.4981f, 0.0106f),
            new P("tc_head_0", 388, 32, 698, 270, 0.3645f, 0.5983f),
            new P("tc_head_1", 714, 32, 1012, 270, 0.3876f, 0.5983f),
            new P("tc_head_2", 74, 306, 380, 538, 0.4003f, 0.6138f),
            new P("tc_head_3", 410, 298, 704, 538, 0.3878f, 0.5933f),
            new P("tc_head_4", 700, 314, 1048, 538, 0.2989f, 0.6357f),
            new P("tc_head_5", 1100, 302, 1418, 540, 0.3632f, 0.5983f),
            new P("tc_sleep_0", 78, 560, 380, 842, 0.4934f, 0.0071f),
            new P("tc_bolt_0", 62, 922, 368, 1002, 0.5000f, 0.5000f),
            new P("tc_bolt_1", 394, 910, 730, 1002, 0.5000f, 0.5000f),
            new P("tc_pierce_0", 770, 896, 1026, 1006, 0.5000f, 0.5000f),
            new P("tc_pierce_1", 1040, 870, 1368, 1036, 0.5000f, 0.5000f),
        } },
        { "Turret_D", new[] {
            new P("td_base_0", 80, 70, 342, 256, 0.5000f, 0.0108f),
            new P("td_head_0", 378, 12, 654, 258, 0.4366f, 0.5984f),
            new P("td_head_1", 694, 12, 970, 258, 0.4438f, 0.5984f),
            new P("td_head_2", 70, 274, 342, 508, 0.4522f, 0.6291f),
            new P("td_head_3", 390, 270, 698, 510, 0.4123f, 0.6133f),
            new P("td_head_4", 712, 278, 1054, 506, 0.3538f, 0.6456f),
            new P("td_head_5", 1066, 264, 1406, 512, 0.3662f, 0.5935f),
            new P("td_sleep_0", 34, 524, 356, 836, 0.5031f, 0.0064f),
            new P("td_fireball_0", 444, 588, 602, 658, 0.5000f, 0.5000f),
            new P("td_fireball_1", 638, 572, 848, 666, 0.5000f, 0.5000f),
            new P("td_fireball_2", 894, 566, 1086, 676, 0.5000f, 0.5000f),
            new P("td_boom_0", 426, 750, 546, 852, 0.5000f, 0.5000f),
            new P("td_boom_1", 584, 724, 766, 856, 0.5000f, 0.5000f),
            new P("td_boom_2", 796, 712, 1022, 856, 0.5000f, 0.5000f),
            new P("td_boom_3", 1046, 718, 1238, 856, 0.5000f, 0.5000f),
            new P("td_flame_0", 380, 926, 550, 1026, 0.5000f, 0.5000f),
            new P("td_flame_1", 608, 902, 834, 1026, 0.5000f, 0.5000f),
            new P("td_flame_2", 882, 914, 1104, 1026, 0.5000f, 0.5000f),
        } },
        { "Build_turret", new[] {
            new P("build_pad", 368, 120, 1167, 892, 0.5f, 0.5f),
        } },
    };

    [MenuItem("MonNyang/포탑 에셋 설정")]
    public static void Run()
    {
        Dictionary<string, Sprite> all = new Dictionary<string, Sprite>();
        foreach (KeyValuePair<string, P[]> sheet in Sheets)
        {
            string path = Img + sheet.Key + ".png";
            TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti == null) { Debug.LogError("[포탑 에셋] 없음: " + path); continue; }
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Multiple;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.GetSourceTextureWidthAndHeight(out int w, out int h);
            List<SpriteMetaData> list = new List<SpriteMetaData>();
            foreach (P raw in sheet.Value)
            {
                P p = CutHead(raw);
                list.Add(new SpriteMetaData
                {
                    name = p.name,
                    rect = new Rect(p.x0, h - p.y1, p.x1 - p.x0, p.y1 - p.y0),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = new Vector2(p.px, p.py),
                });
            }
#pragma warning disable 0618
            ti.spritesheet = list.ToArray();
#pragma warning restore 0618
            ti.SaveAndReimport();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Sprite s) all[s.name] = s;
        }

        TurretConfig cfg = AssetDatabase.LoadAssetAtPath<TurretConfig>(ConfigPath);
        if (cfg == null)
        {
            cfg = ScriptableObject.CreateInstance<TurretConfig>();
            AssetDatabase.CreateAsset(cfg, ConfigPath);
        }
        Fill(cfg.crossbow, all, "ta", "head", 2, 3, "arrow", 1, "hit", 2, null, 0);
        // 석궁 대기 그림(0·1)은 발밑 풀·기둥이 있고 발사 그림(2~4)에는 없어 쏠 때 깜빡임 → 대기도 발사 그림(화살 장전, 2)으로 통일
        cfg.crossbow.idle = new[] { Get(all, "ta_head_2"), Get(all, "ta_head_2") };
        Fill(cfg.sticky, all, "tb", "body", 2, 4, "blob", 2, "splash", 2, "pool", 3);
        Fill(cfg.ballista, all, "tc", "head", 2, 4, "bolt", 2, "pierce", 2, null, 0);
        Fill(cfg.dragon, all, "td", "head", 2, 4, "fireball", 3, "boom", 4, "flame", 3);
        cfg.sticky.baseSprite = null;                        // 끈적이는 본체 하나
        cfg.padSprite = Get(all, "build_pad");             // 포탑 제작 발판 (Build_turret)
        cfg.crossbow.fireSound = Clip("[MHS] Range_1.wav");
        cfg.sticky.fireSound = Clip("[MHS] Range_2.wav");
        cfg.ballista.fireSound = Clip("[MHS] Sniper_2.wav");
        cfg.dragon.fireSound = Clip("[MHS] Anj_FireShoot.wav");
        cfg.buildSound = Clip("[MHS] Anj_Landing.wav");
        cfg.explodeSound = Clip("[MHS] Anj_Rock.wav");
        EditorUtility.SetDirty(cfg);
        AssetDatabase.SaveAssets();
        Debug.Log("[포탑 에셋] 그림 " + all.Count + "개 분할, TurretConfig 지정 완료");
    }

    // 몸(머리) 묶음: 앞 idleCount장 = 대기, 다음 attackCount장 = 공격
    private static void Fill(TurretConfig.TurretType t, Dictionary<string, Sprite> all, string pre, string body, int idleCount, int attackCount,
        string proj, int projCount, string hit, int hitCount, string ground, int groundCount)
    {
        t.baseSprite = Get(all, pre + "_base_0");
        t.idle = Range(all, pre + "_" + body + "_", 0, idleCount);
        t.attack = Range(all, pre + "_" + body + "_", idleCount, attackCount);
        t.sleep = Get(all, pre + "_sleep_0");
        t.projectile = Range(all, pre + "_" + proj + "_", 0, projCount);
        t.hitFx = Range(all, pre + "_" + hit + "_", 0, hitCount);
        t.groundFx = ground != null ? Range(all, pre + "_" + ground + "_", 0, groundCount) : new Sprite[0];
    }

    private static Sprite[] Range(Dictionary<string, Sprite> all, string prefix, int start, int count)
    {
        Sprite[] r = new Sprite[count];
        for (int i = 0; i < count; i++) r[i] = Get(all, prefix + (start + i));
        return r;
    }

    private static Sprite Get(Dictionary<string, Sprite> all, string name) => all.TryGetValue(name, out Sprite s) ? s : null;

    private static AudioClip Clip(string file) => AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + file);
}
