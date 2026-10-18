using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace ShuYu
{
    /// <summary>
    /// CRT 后处理：URP Bloom 辉光 + 暗角 + 色差 + 扫描线。
    /// 挂在 GameManager 上即生效（运行时由 GameManager.Start 幂等添加）。参数在 Inspector 可调。
    /// </summary>
    public class CRTPostFX : MonoBehaviour
    {
        [Header("Bloom 辉光")]
        [SerializeField] float bloomThreshold = 0.6f;
        [SerializeField] float bloomIntensity = 0.3f;
        [SerializeField] float bloomScatter = 0.6f;

        [Header("暗角 / 色差")]
        [SerializeField] float vignetteIntensity = 0.35f;
        [SerializeField] float vignetteSmoothness = 0.4f;
        [SerializeField] float chromaticAberration = 0.12f;

        [Header("扫描线")]
        [SerializeField] float scanlineAlpha = 0.18f;
        [SerializeField] int scanlineSpacing = 3;

        void Awake()
        {
            BuildVolume();
            BuildScanlines();
        }

        /// <summary>全局 Volume：Bloom + Vignette + ChromaticAberration。</summary>
        void BuildVolume()
        {
            var go = new GameObject("CRTVolume");
            go.transform.SetParent(transform, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            var bloom = profile.Add<Bloom>(false);
            bloom.active = true;
            bloom.threshold.Override(bloomThreshold);
            bloom.intensity.Override(bloomIntensity);
            bloom.scatter.Override(bloomScatter);

            var vignette = profile.Add<Vignette>(false);
            vignette.active = true;
            vignette.intensity.Override(vignetteIntensity);
            vignette.smoothness.Override(vignetteSmoothness);

            var ca = profile.Add<ChromaticAberration>(false);
            ca.active = true;
            ca.intensity.Override(chromaticAberration);

            volume.profile = profile;
        }

        /// <summary>扫描线：独立 Overlay Canvas（永远在相机 Canvas 之上），不挡点击。</summary>
        void BuildScanlines()
        {
            var tex = UIFactory.CreateScanlineTexture(1920, 1080, scanlineSpacing, new Color(0, 0, 0, scanlineAlpha));

            var go = new GameObject("Scanlines", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            // 不加 GraphicRaycaster → 不拦截点击

            var imgGo = new GameObject("ScanlineImage", typeof(RectTransform));
            var rt = imgGo.GetComponent<RectTransform>();
            rt.SetParent(go.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = imgGo.AddComponent<Image>();
            img.sprite = Sprite.Create(tex, new Rect(0, 0, 1920, 1080), new Vector2(0.5f, 0.5f), 100f);
            img.type = Image.Type.Simple;
            img.color = Color.white;
            img.raycastTarget = false;
        }
    }
}
