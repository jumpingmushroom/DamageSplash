using UnityEngine;
using UnityEngine.UI;

namespace DamageSplash.Core
{
    /// <summary>
    /// A red bloom around the edges of the screen when you take a heavy hit. A number over your
    /// own head is easy to miss in the middle of a fight, and this is felt rather than read.
    /// Nothing is shipped for it: the vignette is a small texture generated at runtime.
    /// </summary>
    public static class EdgeFlash
    {
        private const int TextureSize = 128;

        private static Image _image;
        private static Sprite _sprite;
        private static Texture2D _texture;
        private static float _builtThickness = -1f;
        private static float _intensity;

        public static bool Ready => _image != null;

        public static void Trigger(float strength = 1f)
        {
            if (!PluginConfig.EdgeFlash.Value)
                return;
            if (!EnsureCreated())
                return;
            _intensity = Mathf.Max(_intensity, Mathf.Clamp01(strength));
            _image.gameObject.SetActive(true);
            Paint();
        }

        public static void Update(float dt)
        {
            if (_image == null || _intensity <= 0f)
                return;
            float fade = Mathf.Max(0.05f, PluginConfig.EdgeFlashFade.Value);
            _intensity -= dt / fade;
            if (_intensity <= 0f)
            {
                _intensity = 0f;
                _image.gameObject.SetActive(false);
                return;
            }
            Paint();
        }

        /// <summary>Config changed: the vignette may need rebuilding at a new thickness.</summary>
        public static void Invalidate()
        {
            if (_builtThickness >= 0f && !Mathf.Approximately(_builtThickness, PluginConfig.EdgeFlashThickness.Value))
                Rebuild();
        }

        public static void Destroy()
        {
            if (_image != null)
                Object.Destroy(_image.gameObject);
            _image = null;
            _intensity = 0f;
            DestroyTexture();
        }

        private static void Paint()
        {
            Color c = PluginConfig.EdgeFlashColor.Value;
            c.a *= _intensity;
            _image.color = c;
        }

        private static bool EnsureCreated()
        {
            if (_image != null && _image.gameObject != null)
                return true;

            Canvas canvas = FindCanvas();
            if (canvas == null)
                return false;

            var go = new GameObject("DamageSplashEdgeFlash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas.transform, worldPositionStays: false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.SetAsFirstSibling();          // behind the rest of the HUD, never tinting it

            _image = go.GetComponent<Image>();
            _image.raycastTarget = false;
            _image.sprite = BuildSprite();
            _image.type = Image.Type.Simple;
            go.SetActive(false);

            if (PluginConfig.Verbose.Value)
                DamageSplashPlugin.Log.LogDebug("edge flash attached to canvas " + canvas.name);
            return true;
        }

        private static Canvas FindCanvas()
        {
            if (DamageText.instance != null)
            {
                Canvas c = DamageText.instance.GetComponentInParent<Canvas>();
                if (c != null)
                    return c.rootCanvas != null ? c.rootCanvas : c;
            }
            if (Hud.instance != null)
            {
                Canvas c = Hud.instance.GetComponentInParent<Canvas>();
                if (c != null)
                    return c.rootCanvas != null ? c.rootCanvas : c;
            }
            return null;
        }

        private static void Rebuild()
        {
            if (_image == null)
                return;
            DestroyTexture();
            _image.sprite = BuildSprite();
        }

        /// <summary>
        /// White, transparent in the middle, opaque at the very edge. Distance is measured with
        /// the max of the two axes rather than a circle, so stretching it across a wide screen
        /// gives an even frame instead of an oval.
        /// </summary>
        private static Sprite BuildSprite()
        {
            float thickness = Mathf.Clamp(PluginConfig.EdgeFlashThickness.Value, 0.05f, 1f);
            _builtThickness = thickness;

            _texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, mipChain: false);
            _texture.wrapMode = TextureWrapMode.Clamp;
            _texture.filterMode = FilterMode.Bilinear;

            var pixels = new Color32[TextureSize * TextureSize];
            float inner = 1f - thickness;
            for (int y = 0; y < TextureSize; y++)
            {
                float v = Mathf.Abs((y + 0.5f) / TextureSize * 2f - 1f);
                for (int x = 0; x < TextureSize; x++)
                {
                    float u = Mathf.Abs((x + 0.5f) / TextureSize * 2f - 1f);
                    float d = Mathf.Max(u, v);
                    float a = inner >= 1f ? 0f : Mathf.Clamp01((d - inner) / (1f - inner));
                    a *= a;                                  // keep the middle clear
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            _texture.SetPixels32(pixels);
            _texture.Apply(updateMipmaps: false);

            _sprite = Sprite.Create(_texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f));
            return _sprite;
        }

        private static void DestroyTexture()
        {
            if (_sprite != null) Object.Destroy(_sprite);
            if (_texture != null) Object.Destroy(_texture);
            _sprite = null;
            _texture = null;
            _builtThickness = -1f;
        }
    }
}
