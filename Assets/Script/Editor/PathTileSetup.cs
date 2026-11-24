using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// Object_Ground_Path.png 의 길 스프라이트 8개로 일반 타일을 만들고 팔레트 Pal 에 넣는다.
// 시트 배치: 위줄 = 꺾임A(┌) 꺾임B(┐) 직선A 직선B / 아래줄 = 교차A 교차B 끝A 끝B (끝은 왼쪽으로 열림)
public static class PathTileSetup
{
    const string SheetPath = "Assets/Assets Folder/MHS Asset/MHS IMAGE/Object_Ground_Path.png";
    const string TileDir = "Assets/Assets Folder/MHS Asset/MHS Tile/Path";
    const string OldTileDir = "Assets/Assets Folder/MHS Asset/MHS Tile";
    const string PalettePath = "Assets/Assets Folder/Undead Survivor Asset/Tiles/Pal.prefab";
    const int TilePixels = 200; // 한 칸 = 1유닛

    [MenuItem("MonNyang/길 타일 다시 설정")]
    public static void Run()
    {
        // 1) 시트 임포트 설정 (픽셀아트, 칸 크기 1유닛)
        var importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
        if (importer == null) { Debug.LogError("[길 타일] 시트를 찾을 수 없음: " + SheetPath); return; }
        importer.spritePixelsPerUnit = TilePixels;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        var sprites = new Dictionary<string, Sprite>();
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(SheetPath))
            if (o is Sprite s) sprites[s.name] = s;

        // 2) 일반 타일 정의 (방향은 팔레트 회전 키 [ ] 로 맞춘다)
        // 칸 위치는 팔레트에서의 배치 (기준 행 아래로)
        const string P = "Object_Ground_Path_";
        var defs = new (string name, string sprite, Vector2Int cell)[]
        {
            ("길 직선 A", P + "2", new Vector2Int(0, 0)),
            ("길 직선 B", P + "3", new Vector2Int(1, 0)),
            ("길 교차 A", P + "5", new Vector2Int(2, 0)),
            ("길 교차 B", P + "4", new Vector2Int(3, 0)),
            ("길 꺾임 A", P + "0", new Vector2Int(0, -1)),
            ("길 꺾임 B", P + "1", new Vector2Int(1, -1)),
            ("길 끝 A", P + "6", new Vector2Int(2, -1)),
            ("길 끝 B", P + "7", new Vector2Int(3, -1)),
        };

        if (!AssetDatabase.IsValidFolder(TileDir))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(TileDir).Replace('\\', '/'), "Path");

        var tiles = new List<(Tile tile, Vector2Int cell)>();
        foreach (var d in defs)
        {
            if (!sprites.TryGetValue(d.sprite, out var s))
            { Debug.LogError("[길 타일] 스프라이트 없음: " + d.sprite); return; }

            string path = TileDir + "/" + d.name + ".asset";
            var t = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (t == null) { t = ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(t, path); }
            t.sprite = s;
            t.colliderType = Tile.ColliderType.None;
            t.flags = TileFlags.LockColor; // 회전은 허용
            // 작게 잘린 스프라이트도 한 칸(TilePixels)을 꽉 채우도록 크기 보정
            t.transform = Matrix4x4.Scale(new Vector3(TilePixels / s.rect.width, TilePixels / s.rect.height, 1f));
            EditorUtility.SetDirty(t);
            tiles.Add((t, d.cell));
        }
        AssetDatabase.SaveAssets();

        // 3) 팔레트: 깨진 이전 길 타일 칸 지우고 새 타일을 기존 칸 아래에 배치
        var root = PrefabUtility.LoadPrefabContents(PalettePath);
        try
        {
            var tm = root.GetComponentInChildren<Tilemap>();
            var keep = new BoundsInt();
            bool hasKeep = false;
            var bounds = tm.cellBounds;
            foreach (var pos in bounds.allPositionsWithin)
            {
                var tb = tm.GetTile(pos);
                if (tb == null) continue;
                string p = AssetDatabase.GetAssetPath(tb);
                bool old = p.StartsWith(OldTileDir + "/Path ") || p.StartsWith(OldTileDir + "/GroundPath_") || p.StartsWith(TileDir + "/");
                if (old) { tm.SetTile(pos, null); continue; }
                if (!hasKeep) { keep = new BoundsInt(pos, Vector3Int.one); hasKeep = true; }
                else
                {
                    keep.xMin = Mathf.Min(keep.xMin, pos.x); keep.yMin = Mathf.Min(keep.yMin, pos.y);
                    keep.xMax = Mathf.Max(keep.xMax, pos.x + 1); keep.yMax = Mathf.Max(keep.yMax, pos.y + 1);
                }
            }

            int baseX = hasKeep ? keep.xMin : 0;
            int baseY = hasKeep ? keep.yMin - 2 : 0;
            foreach (var (tile, cell) in tiles)
                tm.SetTile(new Vector3Int(baseX + cell.x, baseY + cell.y, 0), tile);
            tm.CompressBounds();

            PrefabUtility.SaveAsPrefabAsset(root, PalettePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        Debug.Log("[길 타일] 길 타일 " + tiles.Count + "개 생성, 팔레트 Pal 갱신 완료");
    }
}
