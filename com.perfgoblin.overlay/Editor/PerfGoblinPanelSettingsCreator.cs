#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PerfGoblin.Editor
{
    [InitializeOnLoad]
    static class PerfGoblinPanelSettingsCreator
    {
        const string AssetPath = "Assets/Resources/PerfGoblinPanelSettings.asset";

        static PerfGoblinPanelSettingsCreator()
        {
            EditorApplication.delayCall += EnsurePanelSettings;
        }

        static void EnsurePanelSettings()
        {
            if (AssetDatabase.LoadAssetAtPath<PanelSettings>(AssetPath) != null)
                return;

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;

            const string themePath = "Assets/Resources/PerfGoblinTheme.asset";
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(themePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<ThemeStyleSheet>();
                AssetDatabase.CreateAsset(theme, themePath);
            }
            ps.themeStyleSheet = theme;

            AssetDatabase.CreateAsset(ps, AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[PerfGoblin] Created PanelSettings and ThemeStyleSheet in Assets/Resources/");
        }
    }
}
#endif
