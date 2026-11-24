using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 중간보스 에셋 준비 (에디터 전용)
// - 종별 시트(MiniBoss_Moss/Kestodon/Wasp/Hercudrome.png, 한 줄 11프레임)를 그림 덩어리 기준으로 프레임마다 자름
//   프레임 번호는 왼쪽부터 0, 1, 2 ... (예: MiniBoss_Moss_0). 옆 프레임과 겹치는 부분이 그려지지 않게 테두리를 그림 모양으로 지정
//   기준점: 가로는 몸 무게중심, 세로는 시트의 바닥선(그림자) → 프레임이 바뀌어도 제자리
// - Reward_box.png 자르기
// - Resources/MiniBossConfig 에셋의 종별 프레임·초상화, 보상 상자 프레임, 위치 표시 해골 아이콘을 채움
// 처음 한 번은 컴파일 직후 자동 실행, 이후에는 메뉴 MonNyang > 중간보스 에셋 다시 설정
public static class MiniBossAssetSetup
{
    private const string ImageDir = "Assets/Assets Folder/MHS Asset/MHS IMAGE/";
    private const string BoxPath = ImageDir + "Reward_box.png";
    private const string ForestPath = ImageDir + "Forest_object.png";
    private const string ConfigPath = "Assets/Resources/MiniBossConfig.asset";
    private const string AttackFxPath = ImageDir + "Attack_Effect.png";
    private static readonly int[] AttackFxRowSplits = { 140, 270, 430 };    // 원 / 네모 / 세모 / 화살표 띠
    private const int ChargeWarnFirst = 18, ChargeWarnCount = 6;            // 넷째 줄 화살표 띠 6프레임
    private static readonly Vector4 ChargeWarnBorder = new Vector4(16f, 0f, 16f, 0f);   // 띠 양 끝은 늘이지 않음

    // 종별 시트 (단계 순서: 1 모스, 2 케스토돈, 3 말벌, 4 헤라크로돔)
    // 시트 배치 (4종 공통): 0~3 이동 / 4 경계·예고 / 5~6 공격 / 7~8 피격 / 9~10 쓰러짐
    // 애니메이션 = 프레임 번호 목록. alert 는 케스토돈 돌진 예고·사슴벌레 소환 중, attack 은 케스토돈 돌진 중에 사용
    private class SheetDef
    {
        public string file;
        public int portrait;
        public int[] idle, move, attack, alert, hit, death;
        public float animSpeed = 1f;     // 애니메이션 속도 배율 (메뉴 실행 시 MiniBossConfig 에 넣음)
    }

    private static readonly SheetDef[] Sheets =
    {
        // 움직임이 자연스러운 것을 우선: 이동은 다리가 반대인 두 프레임만, 공격·예고는 한 프레임 고정, 피격은 프레임 대신 색 깜빡임
        // 멧돼지: 0·1(선 자세)과 2·3(걷는 자세)은 키가 15px 달라 섞으면 절고 출렁임 → 걷는 자세 두 장만
        new SheetDef { file = "MiniBoss_Moss", portrait = 0,
            idle = new[] { 2 }, move = new[] { 2, 3 }, alert = new[] { 4 }, attack = new[] { 5 }, hit = new int[0], death = R(9, 10), animSpeed = 0.5f },   // 사망 두 프레임 (작게 그려진 크기는 MiniBoss 에서 보정)
        // 케스킹: 0~3 이 한 걸음 주기 → 네 장을 느리게 (묵직하게)
        new SheetDef { file = "MiniBoss_Kestodon", portrait = 0,
            idle = new[] { 0 }, move = R(0, 3), alert = new[] { 4 }, attack = new[] { 6 }, hit = new int[0], death = R(9, 10), animSpeed = 0.45f },
        // 비드론: 전용 시트 대신 일반 몬스터 Vespoid 그림을 키우고 색을 바꿔 사용 (VespoidClips)
        new SheetDef { file = null, portrait = 0,
            idle = new int[0], move = new int[0], alert = new int[0], attack = new int[0], hit = new int[0], death = new int[0], animSpeed = 1f },
        new SheetDef { file = "MiniBoss_Hercudrome", portrait = 0,
            idle = new[] { 1 }, move = new[] { 1, 2 }, alert = new[] { 4 }, attack = new[] { 5 }, hit = new int[0], death = R(9, 10), animSpeed = 0.5f },   // 1·2 키가 같음
    };

    // Range_effect_1 (예전 Attack_Effect_2): 6칸 x 4줄 격자 (위 줄부터 0~5 위 화살표 / 6~11 겹화살표 / 12~17 부채꼴 / 18~23 마법진)
    private const string AttackFx2Path = ImageDir + "Range_effect_1.png";
    private const string AttackFx2Name = "Range_effect_1";
    private const string BodyMaterialPath = ImageDir + "Materials/MiniBossSprite.mat";
    private const string VespoidAnimDir = "Assets/Assets Folder/MHS Asset/MHS Animation/Enemy/Vespoid/";

    // 비드론 = Vespoid 애니메이션 클립의 스프라이트 (이동 / 사망)
    private static void SetupVespoid(MiniBossConfig.Species s, float animSpeed)
    {
        Sprite[] move = ClipSprites(VespoidAnimDir + "Vespoid_Move.anim");
        Sprite[] dead = ClipSprites(VespoidAnimDir + "Vespoid_Dead.anim");
        if (move.Length == 0)
        {
            Debug.LogError("[MiniBossAssetSetup] Vespoid 이동 클립을 찾지 못함");
            return;
        }
        s.portrait = move[0];
        s.idle = new[] { move[0] };
        s.move = move;
        s.alert = new[] { move[0] };
        s.attack = new[] { move[0] };
        s.hit = new Sprite[0];
        s.death = dead.Length > 0 ? dead : new[] { move[0] };
        s.animSpeed = animSpeed;
        Debug.Log("[MiniBossAssetSetup] 비드론 = Vespoid 프레임 " + move.Length + "개");
    }

    private static Sprite[] ClipSprites(string clipPath)
    {
        List<Sprite> list = new List<Sprite>();
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
            return list.ToArray();
        foreach (EditorCurveBinding b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            foreach (ObjectReferenceKeyframe k in AnimationUtility.GetObjectReferenceCurve(clip, b))
            {
                if (k.value is Sprite sp && !list.Contains(sp)) list.Add(sp);
            }
        }
        return list.ToArray();
    }
    private const int AttackFx2Cols = 6, AttackFx2Rows = 4;

    private const int BigBlobPixels = 3000;    // 이보다 작은 덩어리(먼지, '!', 그림자, 베기 궤적)는 가까운 프레임에 붙임
    private const int OutlineStep = 2;         // 테두리 모양을 따는 세로 간격 (픽셀)
    private const int FootRows = 10;           // 기준점 x를 잡을 바닥선 위 줄 수

    private const int AlphaThreshold = 20;     // 이 값보다 진한 픽셀을 그림으로 판단
    private const int ColumnGap = 3;           // 이 픽셀 수보다 넓게 비면 다음 프레임
    private const int Padding = 2;

    private static int[] R(int from, int to)
    {
        int[] r = new int[to - from + 1];
        for (int i = 0; i < r.Length; i++) r[i] = from + i;
        return r;
    }


    [InitializeOnLoadMethod]
    private static void AutoRun()
    {
        EditorApplication.delayCall += () =>
        {
            MiniBossConfig config = AssetDatabase.LoadAssetAtPath<MiniBossConfig>(ConfigPath);
            bool ready = config != null && config.species != null && config.species.Length > 0
                && config.species[0].idle != null && config.species[0].idle.Length > 0 && config.species[0].idle[0] != null;
            if (!ready)
            {
                Run();
            }
        };
    }

    [MenuItem("MonNyang/중간보스 에셋 다시 설정")]
    public static void Run()
    {
        SliceBox();

        MiniBossConfig config = AssetDatabase.LoadAssetAtPath<MiniBossConfig>(ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<MiniBossConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
        }

        for (int r = 0; r < Sheets.Length && r < config.species.Length; r++)
        {
            SheetDef def = Sheets[r];
            if (def.file == null)
            {
                SetupVespoid(config.species[r], def.animSpeed);
                continue;
            }
            string path = ImageDir + def.file + ".png";
            int count = SliceByBlobs(path, def.file);
            if (count == 0)
            {
                Debug.LogError("[MiniBossAssetSetup] 시트를 자르지 못함: " + path);
                continue;
            }

            Dictionary<string, Sprite> sheet = Load(path);
            string p = def.file + "_";
            MiniBossConfig.Species s = config.species[r];
            s.portrait = Get(sheet, p + def.portrait);
            s.idle = Frames(sheet, p, def.idle);
            s.move = Frames(sheet, p, def.move);
            s.attack = Frames(sheet, p, def.attack);
            s.alert = Frames(sheet, p, def.alert);
            s.hit = Frames(sheet, p, def.hit);
            s.death = Frames(sheet, p, def.death);
            s.animSpeed = def.animSpeed;
            Debug.Log("[MiniBossAssetSetup] " + def.file + " 프레임 " + count + "개");
        }

        // 케스토돈 돌진 예고 = 겹화살표 줄, 갑각군주 소환 = 마법진 줄 (Attack_Effect_2)
        if (SliceGrid(AttackFx2Path, AttackFx2Name, AttackFx2Cols, AttackFx2Rows) > 0)
        {
            Dictionary<string, Sprite> fx = Load(AttackFx2Path);
            config.chargeWarnFrames = Frames(fx, AttackFx2Name + "_", R(6, 11));
            config.summonCircleFrames = Frames(fx, AttackFx2Name + "_", R(18, 23));
        }

        // 몸 재질 (반투명 테두리 자르기)
        Shader shader = Shader.Find("MonNyang/MiniBoss Sprite");
        if (shader != null)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(BodyMaterialPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, BodyMaterialPath);
            }
            mat.shader = shader;
            config.bodyMaterial = mat;
        }

        // 위치 표시: 나침반 고리 + 위 화살표
        Sprite frame = Get(Load(ImageDir + "Object_Panel.png"), "Object_Panel_64");
        if (frame != null) config.indicatorFrame = frame;
        Sprite arrow = Get(Load(AttackFx2Path), AttackFx2Name + "_0");
        if (arrow != null) config.indicatorArrow = arrow;

        Dictionary<string, Sprite> box = Load(BoxPath);
        config.boxIdle = Frames(box, "box_idle", BoxCols);
        config.boxOpen = Frames(box, "box_open", BoxCols);
        config.boxBurst = Frames(box, "box_burst", BoxCols);

        Dictionary<string, Sprite> forest = Load(ForestPath);
        if (config.indicatorIcon == null)
        {
            config.indicatorIcon = Get(forest, "Forest_object_44");     // 해골
        }

        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        Debug.Log("[MiniBossAssetSetup] 중간보스 프레임, 보상 상자, MiniBossConfig 설정 완료");
    }

    // 줄마다 알파가 있는 열 구간을 프레임으로 보고, 그 안에서 위아래 경계를 찾아 자른다
    // bodyPivot: 기준점 x를 그림 아래쪽 절반(몸통·다리)의 무게중심으로 → 프레임마다 그림 폭이 달라도 제자리에서 흔들리지 않음
    // borderFrom~borderCount 번 프레임에는 9-slice 테두리(border) 지정, 기준점은 가운데
    private static int SliceByAlpha(string path, string prefix, int[] rowSplits, bool bodyPivot, Vector4 border,
        int borderFrom = -1, int borderCount = 0, Vector4 borderValue = default)
    {
        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(path);
        if (ti == null || !System.IO.File.Exists(path))
            return 0;

        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(System.IO.File.ReadAllBytes(path)))
            return 0;
        int w = tex.width, h = tex.height;
        Color32[] px = tex.GetPixels32();      // 아래 줄부터
        Object.DestroyImmediate(tex);
        System.Func<int, int, bool> solid = (x, yTop) => px[(h - 1 - yTop) * w + x].a > AlphaThreshold;

        List<int> bands = new List<int> { 0 };
        bands.AddRange(rowSplits);
        bands.Add(h);

        List<SpriteMetaData> list = new List<SpriteMetaData>();
        for (int b = 0; b + 1 < bands.Count; b++)
        {
            int top = bands[b], bottom = bands[b + 1];
            int start = -1, end = -1, gap = 0;
            for (int x = 0; x <= w; x++)
            {
                bool any = false;
                if (x < w)
                {
                    for (int y = top; y < bottom && !any; y++) any = solid(x, y);
                }
                if (any)
                {
                    if (start < 0) start = x;
                    end = x;
                    gap = 0;
                }
                else if (start >= 0 && (++gap > ColumnGap || x == w))
                {
                    // 세로 경계
                    int yMin = bottom, yMax = top;
                    for (int y = top; y < bottom; y++)
                        for (int xx = start; xx <= end; xx++)
                            if (solid(xx, y)) { yMin = Mathf.Min(yMin, y); yMax = Mathf.Max(yMax, y); break; }

                    int x0 = Mathf.Max(0, start - Padding), x1 = Mathf.Min(w - 1, end + Padding);
                    int t0 = Mathf.Max(0, yMin - Padding), t1 = Mathf.Min(h - 1, yMax + Padding);
                    // 이미지 위 기준 → Unity 아래 기준
                    Rect rect = new Rect(x0, h - 1 - t1, x1 - x0 + 1, t1 - t0 + 1);
                    SpriteMetaData meta = Meta(prefix + "_" + list.Count, rect);
                    if (bodyPivot)
                    {
                        float sum = 0f, wsum = 0f;
                        int bodyTop = yMin + (yMax - yMin) / 2;
                        for (int y = bodyTop; y <= yMax; y++)
                            for (int xx = start; xx <= end; xx++)
                            {
                                float a = px[(h - 1 - y) * w + xx].a;
                                sum += a * (xx + 0.5f);
                                wsum += a;
                            }
                        float cx = wsum > 0f ? sum / wsum : (x0 + x1) * 0.5f;
                        meta.alignment = (int)SpriteAlignment.Custom;
                        meta.pivot = new Vector2((cx - x0) / rect.width, (float)Padding / rect.height);
                    }
                    int index = list.Count;
                    if (index >= borderFrom && index < borderFrom + borderCount)
                    {
                        meta.border = borderValue;
                        meta.alignment = (int)SpriteAlignment.Center;
                        meta.pivot = new Vector2(0.5f, 0.5f);
                    }
                    else
                    {
                        meta.border = border;
                    }
                    list.Add(meta);
                    start = -1;
                    gap = 0;
                }
            }
        }

        ti.filterMode = FilterMode.Point;
        Apply(ti, list);
        return list.Count;
    }

    // 보상 상자 (8칸 x 3줄: 대기 / 열림 / 반짝임). 칸마다 상자 몸통 위치가 달라 좌우로 꿀렁이지 않게,
    // 몸통(진한 픽셀) 아래쪽 절반의 가로 중심과 바닥선을 기준으로 칸을 옮겨 맞추고 기준점을 바닥 가운데로
    private const int BoxCols = 8, BoxRows = 3;

    private static void SliceBox()
    {
        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(BoxPath);
        if (ti == null)
            return;
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(System.IO.File.ReadAllBytes(BoxPath));
        int W = tex.width, H = tex.height;
        Color32[] px = tex.GetPixels32();
        Object.DestroyImmediate(tex);
        float cw = W / (float)BoxCols, ch = H / (float)BoxRows;
        string[] names = { "box_idle", "box_open", "box_burst" };

        // 1) 칸마다 몸통 기준점 (시트 좌표)
        Vector2[] anchor = new Vector2[BoxCols * BoxRows];
        for (int r = 0; r < BoxRows; r++)
            for (int c = 0; c < BoxCols; c++)
            {
                int x0 = Mathf.RoundToInt(c * cw), y0 = Mathf.RoundToInt((BoxRows - 1 - r) * ch);
                int w = Mathf.FloorToInt(cw), h = Mathf.FloorToInt(ch);
                int bottom = -1;
                for (int y = 0; y < h && bottom < 0; y++)
                {
                    int cnt = 0;
                    for (int x = 0; x < w; x++) if (px[(y0 + y) * W + x0 + x].a > 230) cnt++;
                    if (cnt > w * 0.2f) bottom = y;                 // 몸통 바닥 (흩날리는 빛 무시)
                }
                if (bottom < 0) bottom = 0;
                double sx = 0, n = 0;
                for (int y = bottom; y < Mathf.Min(h, bottom + Mathf.RoundToInt(h * 0.35f)); y++)
                    for (int x = 0; x < w; x++) if (px[(y0 + y) * W + x0 + x].a > 230) { sx += x; n++; }
                anchor[r * BoxCols + c] = new Vector2(x0 + (n > 0 ? (float)(sx / n) : w * 0.5f), y0 + bottom);
            }

        // 2) 기준점이 칸 안 같은 자리에 오도록 칸을 옮김 (칸 크기 고정, 시트 밖으로는 안 나감)
        int cellW = Mathf.FloorToInt(cw), cellH = Mathf.FloorToInt(ch);
        float ax = cellW * 0.5f, ay = 4f;
        List<SpriteMetaData> list = new List<SpriteMetaData>();
        for (int r = 0; r < BoxRows; r++)
            for (int c = 0; c < BoxCols; c++)
            {
                Vector2 a = anchor[r * BoxCols + c];
                float x = Mathf.Clamp(Mathf.Round(a.x - ax), 0f, W - cellW);
                float y = Mathf.Clamp(Mathf.Round(a.y - ay), 0f, H - cellH);
                SpriteMetaData m = Meta(names[r] + "_" + c, new Rect(x, y, cellW, cellH));
                m.alignment = (int)SpriteAlignment.Custom;
                m.pivot = new Vector2((a.x - x) / cellW, (a.y - y) / cellH);
                list.Add(m);
            }
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.maxTextureSize = 4096;
        Apply(ti, list);
    }

    private static void AddRects(List<SpriteMetaData> list, string prefix, Rect[] rects)
    {
        for (int i = 0; i < rects.Length; i++)
        {
            Rect r = rects[i];
            list.Add(Meta(prefix + "_" + i, new Rect(r.x - 4f, r.y - 2f, r.width + 8f, r.height + 8f)));
        }
    }

    private static SpriteMetaData Meta(string name, Rect rect)
    {
        return new SpriteMetaData
        {
            name = name,
            rect = rect,
            alignment = (int)SpriteAlignment.BottomCenter,
            pivot = new Vector2(0.5f, 0f),
        };
    }

    // 같은 크기 칸으로 나눈 시트 (위 줄 왼쪽부터 번호, 칸 가운데 기준점)
    private static int SliceGrid(string path, string prefix, int cols, int rows)
    {
        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(path);
        if (ti == null || !System.IO.File.Exists(path))
            return 0;
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(System.IO.File.ReadAllBytes(path)))
            return 0;
        float cw = tex.width / (float)cols, ch = tex.height / (float)rows;
        Object.DestroyImmediate(tex);
        List<SpriteMetaData> list = new List<SpriteMetaData>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                Rect rect = new Rect(Mathf.Round(c * cw), Mathf.Round((rows - 1 - r) * ch), Mathf.Floor(cw), Mathf.Floor(ch));
                SpriteMetaData m = Meta(prefix + "_" + list.Count, rect);
                m.alignment = (int)SpriteAlignment.Center;
                m.pivot = new Vector2(0.5f, 0.5f);
                list.Add(m);
            }
        ti.filterMode = FilterMode.Point;
        Apply(ti, list);
        return list.Count;
    }

    // 알파 덩어리(8방향 연결)를 찾아 큰 덩어리 = 프레임, 작은 덩어리는 가로로 가장 가까운 프레임에 포함
    private static int SliceByBlobs(string path, string prefix)
    {
        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(path);
        if (ti == null || !System.IO.File.Exists(path))
            return 0;
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(System.IO.File.ReadAllBytes(path)))
            return 0;
        int w = tex.width, h = tex.height;
        Color32[] px = tex.GetPixels32();      // y 아래부터 (Unity 좌표)
        Object.DestroyImmediate(tex);

        // 덩어리 번호 매기기
        int[] label = new int[w * h];
        List<int> counts = new List<int> { 0 };
        List<double> sumX = new List<double> { 0 };
        List<RectInt> boxes = new List<RectInt> { new RectInt() };
        Stack<int> stack = new Stack<int>();
        for (int i = 0; i < px.Length; i++)
        {
            if (label[i] != 0 || px[i].a <= AlphaThreshold)
                continue;
            int id = counts.Count;
            int n = 0; double sx = 0; int x0 = w, x1 = -1, y0 = h, y1 = -1;
            label[i] = id;
            stack.Push(i);
            while (stack.Count > 0)
            {
                int k = stack.Pop();
                int kx = k % w, ky = k / w;
                n++; sx += kx;
                if (kx < x0) x0 = kx;
                if (kx > x1) x1 = kx;
                if (ky < y0) y0 = ky;
                if (ky > y1) y1 = ky;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = kx + dx, ny = ky + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int j = ny * w + nx;
                        if (label[j] == 0 && px[j].a > AlphaThreshold) { label[j] = id; stack.Push(j); }
                    }
            }
            counts.Add(n); sumX.Add(sx); boxes.Add(new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1));
        }

        // 큰 덩어리 = 프레임 (왼쪽부터)
        List<int> frames = new List<int>();
        for (int id = 1; id < counts.Count; id++)
            if (counts[id] >= BigBlobPixels) frames.Add(id);
        frames.Sort((a, b) => boxes[a].xMin.CompareTo(boxes[b].xMin));
        if (frames.Count == 0)
            return 0;

        // 작은 덩어리 → 가로로 가장 가까운 프레임
        int[] owner = new int[counts.Count];
        for (int id = 1; id < counts.Count; id++)
        {
            if (counts[id] >= BigBlobPixels) { owner[id] = id; continue; }
            if (counts[id] < 4) { owner[id] = 0; continue; }          // 점 몇 개짜리 티끌은 버림
            float cx = boxes[id].center.x;
            int best = frames[0]; float bestD = float.MaxValue;
            foreach (int f in frames)
            {
                RectInt b = boxes[f];
                float d = cx < b.xMin ? b.xMin - cx : cx > b.xMax ? cx - b.xMax : 0f;
                if (d < bestD) { bestD = d; best = f; }
            }
            owner[id] = best;
        }

        // 바닥선: 프레임마다 (자기 덩어리 + 붙은 덩어리)의 가장 낮은 줄 중 가장 흔한 값 → 쓰러진 프레임도 같은 바닥에
        int[] frameBottom = new int[frames.Count];
        for (int fi = 0; fi < frames.Count; fi++)
        {
            int b = boxes[frames[fi]].yMin;
            for (int id = 1; id < counts.Count; id++)
                if (owner[id] == frames[fi]) b = Mathf.Min(b, boxes[id].yMin);
            frameBottom[fi] = b;
        }
        Dictionary<int, int> votes = new Dictionary<int, int>();
        foreach (int b in frameBottom)
            for (int d = -2; d <= 2; d++) votes[b + d] = (votes.TryGetValue(b + d, out int c) ? c : 0) + (d == 0 ? 3 : 1);
        int ground = frameBottom[0]; int bestVote = -1;
        foreach (var kv in votes) if (kv.Value > bestVote || (kv.Value == bestVote && kv.Key < ground)) { bestVote = kv.Value; ground = kv.Key; }

        List<SpriteMetaData> list = new List<SpriteMetaData>();
        Dictionary<string, List<Vector2[]>> outlines = new Dictionary<string, List<Vector2[]>>();
        for (int fi = 0; fi < frames.Count; fi++)
        {
            int f = frames[fi];
            int x0 = w, y0 = h, x1 = -1, y1 = -1;
            List<int> parts = new List<int>();
            for (int id = 1; id < counts.Count; id++)
            {
                if (owner[id] != f) continue;
                parts.Add(id);
                RectInt b = boxes[id];
                x0 = Mathf.Min(x0, b.xMin); y0 = Mathf.Min(y0, b.yMin); x1 = Mathf.Max(x1, b.xMax); y1 = Mathf.Max(y1, b.yMax);
            }
            x0 = Mathf.Max(0, x0 - 1); y0 = Mathf.Max(0, Mathf.Min(y0, ground) - 1);
            x1 = Mathf.Min(w, x1 + 2); y1 = Mathf.Min(h, y1 + 2);
            Rect rect = new Rect(x0, y0, x1 - x0, y1 - y0);

            string name = prefix + "_" + fi;
            SpriteMetaData meta = Meta(name, rect);
            // 기준점 x: 바닥선 위 몇 줄(발·그림자)의 가운데 → 프레임마다 몸 모양이 달라도 발 위치가 고정됨
            double fsum = 0; int fcount = 0;
            for (int yy = ground; yy < Mathf.Min(h, ground + FootRows); yy++)
                for (int xx = x0; xx < x1; xx++)
                {
                    int lb = label[yy * w + xx];
                    if (lb != 0 && owner[lb] == f) { fsum += xx; fcount++; }
                }
            float bodyX = fcount > 0 ? (float)(fsum / fcount) + 0.5f : (float)(sumX[f] / counts[f]) + 0.5f;
            meta.alignment = (int)SpriteAlignment.Custom;
            meta.pivot = new Vector2((bodyX - x0) / rect.width, (ground - y0) / rect.height);
            list.Add(meta);

            // 테두리: 덩어리마다 줄 단위 좌우 끝을 이은 모양 (옆 프레임 픽셀 제외)
            List<Vector2[]> shapes = new List<Vector2[]>();
            Vector2 center = rect.center;
            foreach (int id in parts)
            {
                RectInt b = boxes[id];
                List<Vector2> right = new List<Vector2>(), left = new List<Vector2>();
                for (int y = b.yMin; y <= b.yMax; y += OutlineStep)
                {
                    int yEnd = Mathf.Min(b.yMax + 1, y + OutlineStep);
                    int mn = int.MaxValue, mx = int.MinValue;
                    for (int yy = y; yy < yEnd; yy++)
                        for (int xx = b.xMin; xx <= b.xMax; xx++)
                            if (label[yy * w + xx] == id) { if (xx < mn) mn = xx; if (xx > mx) mx = xx; }
                    if (mx < mn) continue;
                    right.Add(new Vector2(mx + 1, y)); right.Add(new Vector2(mx + 1, yEnd));
                    left.Add(new Vector2(mn, y)); left.Add(new Vector2(mn, yEnd));
                }
                if (right.Count < 2) continue;
                left.Reverse();
                right.AddRange(left);
                for (int i = 0; i < right.Count; i++) right[i] -= center;      // 스프라이트 중심 기준
                shapes.Add(right.ToArray());
            }
            outlines[name] = shapes;
        }

        ti.filterMode = FilterMode.Point;
        Apply(ti, list, SpriteMeshType.Tight, outlines);
        return list.Count;
    }

    private static void Apply(TextureImporter ti, List<SpriteMetaData> list,
        SpriteMeshType meshType = SpriteMeshType.FullRect, Dictionary<string, List<Vector2[]>> outlines = null)
    {
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        TextureImporterSettings settings = new TextureImporterSettings();
        ti.ReadTextureSettings(settings);
        settings.spriteMeshType = meshType;                   // FullRect: 9-slice(늘려 그리기)용 / Tight: 지정한 테두리 모양대로
        ti.SetTextureSettings(settings);
        ti.maxTextureSize = 4096;                             // 가로 2048 넘는 시트가 줄어들어 흐려지지 않게
        ti.textureCompression = TextureImporterCompression.Uncompressed;   // 크게 그려도 압축 얼룩·흐림 없게

        // Sprite Editor 데이터로 기록 (이름이 같으면 기존 spriteID 유지 → 참조 안 깨짐)
        var factory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
        factory.Init();
        var dp = factory.GetSpriteEditorDataProviderFromObject(ti);
        dp.InitSpriteEditorDataProvider();
        Dictionary<string, GUID> ids = new Dictionary<string, GUID>();
        foreach (var old in dp.GetSpriteRects())
            ids[old.name] = old.spriteID;

        List<UnityEditor.SpriteRect> rects = new List<UnityEditor.SpriteRect>();
        List<UnityEditor.SpriteNameFileIdPair> pairs = new List<UnityEditor.SpriteNameFileIdPair>();
        foreach (SpriteMetaData m in list)
        {
            var r = new UnityEditor.SpriteRect
            {
                name = m.name,
                rect = m.rect,
                alignment = (SpriteAlignment)m.alignment,
                pivot = m.pivot,
                border = m.border,
                spriteID = ids.TryGetValue(m.name, out GUID id) ? id : GUID.Generate(),
            };
            rects.Add(r);
            pairs.Add(new UnityEditor.SpriteNameFileIdPair(r.name, r.spriteID));
        }
        dp.SetSpriteRects(rects.ToArray());
        if (outlines != null)
        {
            var outlineProvider = dp.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteOutlineDataProvider>();
            if (outlineProvider != null)
            {
                foreach (UnityEditor.SpriteRect r in rects)
                {
                    if (outlines.TryGetValue(r.name, out List<Vector2[]> shapes))
                    {
                        outlineProvider.SetOutlines(r.spriteID, shapes);
                        outlineProvider.SetTessellationDetail(r.spriteID, 0f);
                    }
                }
            }
        }
        dp.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteNameFileIdDataProvider>()?.SetNameFileIdPairs(pairs);
        dp.Apply();
        ti.SaveAndReimport();
    }

    private static Dictionary<string, Sprite> Load(string path)
    {
        Dictionary<string, Sprite> map = new Dictionary<string, Sprite>();
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (o is Sprite s)
            {
                map[s.name] = s;
            }
        }
        return map;
    }

    private static Sprite Get(Dictionary<string, Sprite> map, string name)
    {
        return map.TryGetValue(name, out Sprite s) ? s : null;
    }

    private static Sprite[] Frames(Dictionary<string, Sprite> map, string prefix, int[] indices)
    {
        List<Sprite> list = new List<Sprite>();
        foreach (int i in indices)
        {
            Sprite s = Get(map, prefix + i);
            if (s != null) list.Add(s);
        }
        return list.ToArray();
    }

    private static Sprite[] Frames(Dictionary<string, Sprite> map, string prefix, int count)
    {
        List<Sprite> list = new List<Sprite>();
        for (int i = 0; i < count; i++)
        {
            Sprite s = Get(map, prefix + "_" + i);
            if (s != null) list.Add(s);
        }
        return list.ToArray();
    }
}
