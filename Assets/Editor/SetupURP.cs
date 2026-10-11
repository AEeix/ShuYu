#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShuYu
{
    /// <summary>一键切换渲染管线：创建 URP 资产并设为默认，支持一键回退到内置管线。</summary>
    public static class SetupURP
    {
        const string Dir = "Assets/Settings";
        const string RendererPath = Dir + "/UniversalRendererData.asset";
        const string AssetPath = Dir + "/UniversalRenderPipelineAsset.asset";

        [MenuItem("Tools/Setup URP")]
        public static void Setup()
        {
            if (!AssetDatabase.IsValidFolder(Dir))
                AssetDatabase.CreateFolder("Assets", "Settings");

            // 渲染器数据（默认 Forward Renderer，UI + 后处理够用）
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererPath);
            }

            // URP 管线资产
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetPath);
            if (asset == null)
            {
                asset = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(asset, AssetPath);
            }

            // 设为默认管线（Graphics + 所有 Quality 等级）
            GraphicsSettings.defaultRenderPipeline = asset;
            QualitySettings.renderPipeline = asset;

            AssetDatabase.SaveAssets();
            Debug.Log("[SetupURP] 已启用 URP：" + AssetPath);
        }

        [MenuItem("Tools/Revert to Built-in")]
        public static void Revert()
        {
            GraphicsSettings.defaultRenderPipeline = null;
            QualitySettings.renderPipeline = null;
            Debug.Log("[SetupURP] 已回退到内置管线");
        }
    }
}
#endif
