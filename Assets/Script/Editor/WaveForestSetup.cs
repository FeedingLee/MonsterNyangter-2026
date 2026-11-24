using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Wave_forest.png 프레임 정렬 (에디터 전용, 메뉴 MonNyang > 숲 입구 프레임 정렬)
// 시트의 그림 간격과 잘라 둔 칸 간격이 달라 칸마다 그림이 밀려 있으면 재생할 때 꿀렁거림.
// 칸 크기·이름은 그대로 두고, 각 칸을 그 안 그림의 중심이 칸 가운데에 오도록 옮긴다 (spriteID 유지 → 참조 안 깨짐).
public static class WaveForestSetup
{
    private const string SheetPath = "Assets/Assets Folder/MHS Asset/MHS IMAGE/Wave_forest.png";
    private const int AlphaThreshold = 128;
    private const int Passes = 3;              // 옮긴 뒤 다시 재서 맞추는 횟수

    // ---------------------------------------------------------------
    // 숲 입구 붉은 눈: Enemy_Eye.png (8열 x 4줄 = 32프레임)를 같은 크기 칸으로 자르고,
    // Wave Spawner/Wave_forest_0 (정지된 숲) 의 어두운 가운데 위에 자식 "Enemy_Eye" 로 얹어 반복 재생
    // 위치·크기·속도는 만든 뒤 인스펙터(Transform, SpriteLoopAnimator)에서 조정
    // ---------------------------------------------------------------
    private const string EyePath = "Assets/Assets Folder/MHS Asset/MHS IMAGE/Enemy_Eye.png";
    private const int EyeCols = 8, EyeRows = 4;
    private static readonly Vector3 EyeLocalPos = new Vector3(0f, -0.15f, 0f);   // 숲 그림 가운데 기준 (어두운 굴 안)
    private const float EyeLocalScale = 0.5f;
    private const int EyeAlphaThreshold = 20;

    [MenuItem("MonNyang/숲 입구 눈 애니메이션 만들기")]
    public static void SetupEyes()
    {
        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(EyePath);
        if (ti == null) { Debug.LogError("[숲 입구] 시트 없음: " + EyePath); return; }

        // 1) 격자 자르기 (가로 2048 넘는 시트가 줄어들지 않게 최대 크기도 올림)
        Texture2D tex = new Texture2D(2, 2);
        tex.LoadImage(System.IO.File.ReadAllBytes(EyePath));
        float cw = tex.width / (float)EyeCols, ch = tex.height / (float)EyeRows;
        Object.DestroyImmediate(tex);

        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.maxTextureSize = 4096;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
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
        for (int r = 0; r < EyeRows; r++)
            for (int c = 0; c < EyeCols; c++)
            {
                string name = "Enemy_Eye_" + rects.Count;
                SpriteRect sr = new SpriteRect
                {
                    name = name,
                    rect = new Rect(Mathf.Round(c * cw), Mathf.Round((EyeRows - 1 - r) * ch), Mathf.Floor(cw), Mathf.Floor(ch)),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    spriteID = ids.TryGetValue(name, out GUID id) ? id : GUID.Generate(),
                };
                rects.Add(sr);
                pairs.Add(new SpriteNameFileIdPair(name, sr.spriteID));
            }
        dp.SetSpriteRects(rects.ToArray());
        dp.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteNameFileIdDataProvider>()?.SetNameFileIdPairs(pairs);
        dp.Apply();
        ti.SaveAndReimport();
        AlignSheet(EyePath, EyeAlphaThreshold);                  // 칸마다 눈 위치가 달라 꿀렁거리지 않게 그림 중심을 맞춤

        List<Sprite> frames = new List<Sprite>();
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(EyePath))
            if (o is Sprite s) frames.Add(s);
        frames.Sort((a, b) => int.Parse(a.name.Substring(a.name.LastIndexOf('_') + 1)).CompareTo(int.Parse(b.name.Substring(b.name.LastIndexOf('_') + 1))));

        // 2) 숲 오브젝트 찾기
        GameObject forest = null;
        foreach (Transform t in Object.FindObjectsOfType<Transform>(true))
            if (t.name == "Wave_forest_0" && t.parent != null && t.parent.name == "Wave Spawner") forest = t.gameObject;
        if (forest == null) { Debug.LogError("[숲 입구] Wave Spawner/Wave_forest_0 없음"); return; }
        SpriteRenderer forestSr = forest.GetComponent<SpriteRenderer>();

        // 숲은 정지 (예전 반복 애니메이션이 남아 있으면 제거)
        SpriteLoopAnimator oldAnim = forest.GetComponent<SpriteLoopAnimator>();
        if (oldAnim != null) Undo.DestroyObjectImmediate(oldAnim);

        // 3) 눈 레이어 (이미 있으면 프레임만 갱신, 위치·크기는 사용자가 조정한 값 유지)
        Transform eye = forest.transform.Find("Enemy_Eye");
        bool created = eye == null;
        if (created)
        {
            GameObject go = new GameObject("Enemy_Eye");
            Undo.RegisterCreatedObjectUndo(go, "숲 입구 눈");
            go.transform.SetParent(forest.transform, false);
            go.transform.localPosition = EyeLocalPos;
            go.transform.localScale = Vector3.one * EyeLocalScale;
            eye = go.transform;
        }
        SpriteRenderer eyeSr = eye.GetComponent<SpriteRenderer>();
        if (eyeSr == null) eyeSr = Undo.AddComponent<SpriteRenderer>(eye.gameObject);
        eyeSr.sprite = frames.Count > 0 ? frames[0] : null;
        if (forestSr != null)
        {
            eyeSr.sortingLayerID = forestSr.sortingLayerID;
            eyeSr.sortingOrder = forestSr.sortingOrder + 1;          // 숲 그림 바로 위
            eyeSr.sharedMaterial = forestSr.sharedMaterial;
        }
        SpriteLoopAnimator anim = eye.GetComponent<SpriteLoopAnimator>();
        if (anim == null) anim = Undo.AddComponent<SpriteLoopAnimator>(eye.gameObject);
        anim.frames = frames.ToArray();
        if (created)
        {
            anim.fps = 10f;
            anim.loopDelay = 2f;                                     // 눈을 감고 잠시 쉬었다가 다시 뜸
        }
        EditorUtility.SetDirty(anim);
        EditorUtility.SetDirty(eyeSr);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(forest.scene);
        Debug.Log("[숲 입구] 눈 프레임 " + frames.Count + "개, " + (created ? "새로 만듦" : "프레임 갱신"));
    }

    [MenuItem("MonNyang/숲 입구 프레임 정렬")]
    public static void Run()
    {
        AlignSheet(SheetPath, AlphaThreshold);
    }

    // 시트의 모든 칸을 '칸 안 그림 중심'이 같은 자리에 오도록 옮김 (칸 크기·이름·spriteID 유지)
    // threshold: 이 알파보다 진한 픽셀을 그림으로 봄. 중심은 알파 가중 (흐린 빛 번짐이 커져도 가운데는 그대로)
    public static void AlignSheet(string sheetPath, int threshold)
    {
        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(sheetPath);
        if (ti == null) { Debug.LogError("[프레임 정렬] 시트 없음: " + sheetPath); return; }

        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(System.IO.File.ReadAllBytes(sheetPath));
        int W = tex.width, H = tex.height;
        Color32[] px = tex.GetPixels32();
        Object.DestroyImmediate(tex);

        var factory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
        factory.Init();
        var dp = factory.GetSpriteEditorDataProviderFromObject(ti);
        dp.InitSpriteEditorDataProvider();
        SpriteRect[] rects = dp.GetSpriteRects();

        // 공통 기준점: 칸은 시트 밖으로 못 나가므로, 각 칸이 옮겨서 맞출 수 있는 '칸 안 그림 중심' 범위를 구하고
        // 모든 칸이 맞출 수 있는 범위 안에서 칸 가운데에 가장 가까운 값을 고름 (시트 가장자리에 붙은 칸도 같이 맞춰짐)
        Vector2 lo = new Vector2(float.MinValue, float.MinValue), hi = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 cellCenter = Vector2.zero;
        foreach (SpriteRect r in rects)
        {
            if (!Centroid(px, W, r.rect, threshold, out Vector2 c0))
                continue;
            Vector2 abs = c0 + r.rect.position;                  // 시트 기준 그림 중심
            // 칸 위치 x ∈ [0, W - w] → 칸 안 중심 = abs - x ∈ [abs - (W - w), abs]
            lo = Vector2.Max(lo, new Vector2(abs.x - (W - r.rect.width), abs.y - (H - r.rect.height)));
            hi = Vector2.Min(hi, abs);
            cellCenter = r.rect.size * 0.5f;
        }
        Vector2 target = new Vector2(
            lo.x <= hi.x ? Mathf.Clamp(cellCenter.x, lo.x, hi.x) : (lo.x + hi.x) * 0.5f,
            lo.y <= hi.y ? Mathf.Clamp(cellCenter.y, lo.y, hi.y) : (lo.y + hi.y) * 0.5f);

        int moved = 0;
        foreach (SpriteRect r in rects)
        {
            Rect rect = r.rect;
            for (int pass = 0; pass < Passes; pass++)
            {
                if (!Centroid(px, W, rect, threshold, out Vector2 c))
                    break;
                Vector2 offset = new Vector2(Mathf.Round(c.x - target.x), Mathf.Round(c.y - target.y));
                if (offset == Vector2.zero)
                    break;
                rect.x = Mathf.Clamp(rect.x + offset.x, 0f, W - rect.width);
                rect.y = Mathf.Clamp(rect.y + offset.y, 0f, H - rect.height);
            }
            if (rect != r.rect) moved++;
            r.rect = rect;
            r.alignment = SpriteAlignment.Center;
            r.pivot = new Vector2(0.5f, 0.5f);
        }

        dp.SetSpriteRects(rects);
        dp.Apply();
        ti.SaveAndReimport();
        Debug.Log("[프레임 정렬] " + System.IO.Path.GetFileName(sheetPath) + " 프레임 " + rects.Length + "개 중 " + moved + "개 위치 맞춤");
    }

    // 칸 안 불투명 픽셀의 무게중심 (칸 왼쪽 아래 기준)
    private static bool Centroid(Color32[] px, int W, Rect rect, int threshold, out Vector2 c)
    {
        int x0 = (int)rect.x, y0 = (int)rect.y, w = (int)rect.width, h = (int)rect.height;
        double sx = 0, sy = 0, n = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                byte a = px[(y0 + y) * W + x0 + x].a;
                if (a <= threshold) continue;
                sx += (x + 0.5) * a; sy += (y + 0.5) * a; n += a;
            }
        c = n > 0 ? new Vector2((float)(sx / n), (float)(sy / n)) : Vector2.zero;
        return n > 0;
    }
}
