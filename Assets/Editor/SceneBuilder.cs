#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace ShuYu
{
    /// <summary>
    /// 场景化构建器：一键生成 Assets/Scenes/Main.unity（标题 + 静态框架 + GameManager），
    /// 让团队成员能在编辑器里直接改 UI，Play 前即可见。菜单：ShuYu/Build Main Scene。
    /// </summary>
    public static class SceneBuilder
    {
        const string SceneDir = "Assets/Scenes";
        const string ScenePath = SceneDir + "/Main.unity";
        const string FontDir = "Assets/Resources/Fonts";
        const string MonoFontPath = FontDir + "/ShuYuMono SDF.asset";
        const string CjkFontPath = FontDir + "/ShuYuCJK SDF.asset";
        const string UIDir = "Assets/Resources/UI";
        const string RoundedPngPath = UIDir + "/RoundedRect.png";
        const string AxisPngPath = UIDir + "/Axis.png";

        [MenuItem("ShuYu/Build Main Scene")]
        public static void BuildMainScene()
        {
            // 1. 持久化字体 / 圆角 / 坐标轴资产（否则场景保存后引用会丢）
            var mono = EnsurePersistentFont("Fonts/VT323-Regular", MonoFontPath);
            var cjk = EnsurePersistentFont("Fonts/ark-pixel-12px-monospaced-zh_hans", CjkFontPath);
            if (mono != null && cjk != null)
            {
                mono.fallbackFontAssetTable = new List<TMP_FontAsset> { cjk };
                EditorUtility.SetDirty(mono);
                // 预烘焙静态标题所需的 CJK 字形，避免场景保存后字形缺失
                cjk.TryAddCharacters("数域开始游戏退出", out _);
                EditorUtility.SetDirty(cjk);
            }

            var rounded = EnsurePersistentSprite(UIFactory.CreateRoundedTexture(), RoundedPngPath,
                new Vector4(18, 18, 18, 18), FilterMode.Bilinear);
            var axis = EnsurePersistentSprite(
                UIFactory.CreateAxisTexture(1920, 1080, new Vector2(150f, 150f), 100f, 100f, 14f, GameConfig.SubText),
                AxisPngPath, Vector4.zero, FilterMode.Point);

            // 2. 新建空场景
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 3. 让 UIFactory 用持久化资产构建
            UIFactory.SetFont(mono);
            if (rounded != null) UIFactory.SetRoundedSprite(rounded);

            // 相机：UI 走 ScreenSpaceCamera 渲染，靠它 + URP 后处理出 CRT 辉光
            var camGo = new GameObject("Main Camera", typeof(Camera));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = GameConfig.Background;
            var urp = camGo.AddComponent<UniversalAdditionalCameraData>();
            urp.renderPostProcessing = true;

            var canvas = UIFactory.EnsureCanvas();

            // 持久世界：背景 + 坐标轴
            var world = UIFactory.CreateRect("World", canvas.transform);
            UIFactory.Stretch(world);
            var bg = UIFactory.CreatePanel("WorldBG", world, GameConfig.Background);
            UIFactory.Stretch(bg.rectTransform);
            if (axis != null)
            {
                var axisImg = UIFactory.CreatePanel("WorldAxis", world, Color.white);
                axisImg.sprite = axis;
                axisImg.type = Image.Type.Simple;
                UIFactory.Stretch(axisImg.rectTransform);
            }

            // 静态标题
            var titleRoot = UIFactory.CreateRect("TitleRoot", canvas.transform);
            UIFactory.Stretch(titleRoot);
            BuildTitle(titleRoot, out Button startButton, out Button quitButton);

            // 选职 / 路线静态框架（默认隐藏，运行时激活；动态内容仍在运行时填）
            var classSelectRoot = ClassSelectScreen.BuildStaticFrame(canvas.transform);
            classSelectRoot.gameObject.SetActive(false);
            var routeRoot = RouteScreen.BuildStaticFrame(canvas.transform);
            routeRoot.gameObject.SetActive(false);

            // 4. GameManager + 绑定引用
            var gmGo = new GameObject("GameManager");
            var gm = gmGo.AddComponent<GameManager>();
            gm.BindScene(world.gameObject, titleRoot.gameObject, startButton, quitButton,
                classSelectRoot.gameObject, routeRoot.gameObject);
            gmGo.AddComponent<CRTPostFX>(); // CRT 后处理，参数在 Inspector 可调

            // 5. 保存场景 + 写入 Build Settings
            EnsureFolder(SceneDir);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBuilder] 已生成 " + ScenePath + "，并加入 Build Settings");
        }

        static void BuildTitle(Transform parent, out Button startButton, out Button quitButton)
        {
            const float OriginX = 150f, OriginY = 150f;
            const float width = 360f;

            var title = UIFactory.CreateText("Title", parent, "数域", 144, GameConfig.Accent);
            title.alignment = TextAlignmentOptions.Left;
            PlaceAbs(title.rectTransform, OriginX + 60, 480, OriginX + width, 680);

            var start = UIFactory.CreateButton("StartButton", parent, "开始游戏", 48, GameConfig.Accent, GameConfig.Background, null);
            PlaceAbs(start, OriginX + 60, 380, OriginX + width, 470);

            var quit = UIFactory.CreateButton("QuitButton", parent, "退出游戏", 36, GameConfig.CardBase, GameConfig.SubText, null);
            PlaceAbs(quit, OriginX + 60, 300, OriginX + width, 370);

            startButton = start.GetComponent<Button>();
            quitButton = quit.GetComponent<Button>();
        }

        static void PlaceAbs(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(right, top);
        }

        /// <summary>持久化 TMP 字体资产（含子资产：图集纹理 + 材质），避免场景保存后 font 引用丢失。</summary>
        static TMP_FontAsset EnsurePersistentFont(string sourceResource, string assetPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;

            var font = Resources.Load<Font>(sourceResource);
            if (font == null) { Debug.LogError("[SceneBuilder] 找不到字体 " + sourceResource); return null; }

            var asset = TMP_FontAsset.CreateFontAsset(font);
            asset.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(asset, assetPath);
            if (asset.atlasTextures != null && asset.atlasTextures.Length > 0 && asset.atlasTextures[0] != null)
                AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            if (asset.material != null)
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBuilder] 已持久化字体 " + assetPath);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        }

        /// <summary>把程序化纹理写成 PNG 资产并导入为 Sprite（走标准导入管线，团队可直接看到/编辑）。</summary>
        static Sprite EnsurePersistentSprite(Texture2D tex, string pngPath, Vector4 border, FilterMode filter)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
            if (existing != null) return existing;
            if (tex == null) return null;

            EnsureFolder(Path.GetDirectoryName(pngPath));
            File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(pngPath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(pngPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = border;
            importer.filterMode = filter;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
        }

        static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || parent == "Assets")
                AssetDatabase.CreateFolder("Assets", leaf);
            else
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }
    }
}
#endif
