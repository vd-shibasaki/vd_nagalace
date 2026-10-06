using UnityEngine;

namespace NagaRace.Presentation.App
{
    /// <summary>
    /// UI 用のスプライトをコードで描く（角丸パネル・円・顔つきの葉っぱ・星）。
    /// 正式な UI アート（M9）が入るまでの仮素材。Resources に依存しない。
    /// </summary>
    public static class UiSprites
    {
        private static Sprite _rounded;
        private static Sprite _roundedSmall;
        private static Sprite _circle;
        private static Sprite _leaf;
        private static Sprite _leafFace;
        private static Sprite _star;

        /// <summary>角丸（9スライス）。パネル・カード・ボタンに使う。</summary>
        public static Sprite Rounded => _rounded ?? (_rounded = CreateRounded(96, 30));

        /// <summary>小さめの角丸（タグ・バッジ）。</summary>
        public static Sprite RoundedSmall => _roundedSmall ?? (_roundedSmall = CreateRounded(48, 14));

        public static Sprite Circle => _circle ?? (_circle = CreateShape(64, (x, y) => Inside(x, y, 0.5f, 0.5f, 0.48f) ? 1f : 0f));

        /// <summary>葉っぱ（タップエフェクト用）。</summary>
        public static Sprite Leaf => _leaf ?? (_leaf = CreateShape(64, LeafAlpha));

        /// <summary>顔つきの葉っぱ（ちびキャラの代わりのアイコン）。体の色は Image.color で系統色に染める。</summary>
        public static Sprite LeafFace => _leafFace ?? (_leafFace = CreateLeafFace(128));

        public static Sprite Star => _star ?? (_star = CreateShape(64, StarAlpha));

        private static Sprite CreateRounded(int size, int radius)
        {
            var texture = NewTexture(size);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                    float dy = Mathf.Max(0, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    byte alpha = (byte)(255 * Mathf.Clamp01(radius - distance + 0.5f));
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            var border = new Vector4(radius, radius, radius, radius);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        private static Sprite CreateShape(int size, System.Func<float, float, float> alpha)
        {
            var texture = NewTexture(size);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float a = 0;
                    // 4点サンプリングで縁をなめらかに
                    for (int s = 0; s < 4; s++)
                    {
                        float u = (x + 0.25f + 0.5f * (s % 2)) / size;
                        float v = (y + 0.25f + 0.5f * (s / 2)) / size;
                        a += alpha(u, v);
                    }

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255 * a / 4f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateLeafFace(int size)
        {
            var texture = NewTexture(size);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float v = (y + 0.5f) / size;
                    float body = LeafAlpha(u, v);
                    Color color = Color.white;
                    // 目（黒い楕円）とほっぺ。体の色は Image.color で乗算されるため、目は黒、ほっぺは薄い赤
                    if (Inside((u - 0.40f) * 1.6f + 0.40f, v, 0.40f, 0.50f, 0.055f) || Inside((u - 0.60f) * 1.6f + 0.60f, v, 0.60f, 0.50f, 0.055f))
                    {
                        color = new Color(0.12f, 0.12f, 0.12f);
                    }
                    else if (Inside(u, v, 0.31f, 0.40f, 0.05f) || Inside(u, v, 0.69f, 0.40f, 0.05f))
                    {
                        color = new Color(1f, 0.75f, 0.75f);
                    }
                    else if (Mathf.Abs(u - 0.5f) < 0.012f && v > 0.62f)
                    {
                        // 葉脈
                        color = new Color(0.82f, 0.82f, 0.82f);
                    }

                    pixels[y * size + x] = new Color(color.r, color.g, color.b, body);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>丸みのある葉の形（上が尖る）。</summary>
        private static float LeafAlpha(float u, float v)
        {
            float x = (u - 0.5f) * 2f;
            float y = (v - 0.45f) * 2f;
            // 下は丸く、上ほど細くなる
            float width = y < 0 ? Mathf.Sqrt(Mathf.Max(0, 1 - y * y * 1.2f)) * 0.85f : (1 - y / 1.05f) * 0.85f;
            return Mathf.Abs(x) <= width && y > -0.9f && y < 1.05f ? 1f : 0f;
        }

        private static float StarAlpha(float u, float v)
        {
            float x = Mathf.Abs(u - 0.5f) * 2f;
            float y = Mathf.Abs(v - 0.5f) * 2f;
            // 4方向に伸びるキラキラ
            return Mathf.Pow(x, 0.5f) + Mathf.Pow(y, 0.5f) <= 1.05f ? 1f : 0f;
        }

        private static bool Inside(float u, float v, float cx, float cy, float r)
        {
            float dx = u - cx;
            float dy = v - cy;
            return dx * dx + dy * dy <= r * r;
        }

        private static Texture2D NewTexture(int size)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
        }
    }
}
