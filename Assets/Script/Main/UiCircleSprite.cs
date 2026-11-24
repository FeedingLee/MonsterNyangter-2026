using UnityEngine;

// UI에서 별도 이미지 없이 쓸 수 있는, 가장자리가 부드러운 흰색 원 스프라이트를 생성
public static class UiCircleSprite
{
    private const int TextureSize = 256;

    public static Sprite Create()
    {
        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        float radius = TextureSize * 0.5f;
        Vector2 center = new Vector2(radius, radius);
        Color[] pixels = new Color[TextureSize * TextureSize];

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                pixels[y * TextureSize + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(radius - distance));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, TextureSize, TextureSize),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);
    }

    public static void Release(Sprite sprite)
    {
        if (sprite == null)
            return;

        Object.Destroy(sprite.texture);
        Object.Destroy(sprite);
    }
}
