#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Control.Tools.BlenderTransformations
{
    public enum ControlTransformAxisMode
    {
        UnityAxis = 0,
        BlenderAxis = 1
    }

    internal enum ControlUnityAxis
    {
        X,
        Y,
        Z
    }

    internal enum ControlUserAxis
    {
        X,
        Y,
        Z
    }

    internal static class ControlTransformSettings
    {
        private const string AxisModeKey = "Control.TransformShortcut.AxisMode";
        private const string DefaultSettingsPath = "Assets/Control Tools/Blender Transformations/ControlTransformSettings.asset";

        private static ControlTransformSettingsAsset cachedAsset;

        [InitializeOnLoadMethod]
        private static void EnsureSettingsAssetOnLoad()
        {
            EditorApplication.delayCall += () => GetOrCreateAsset();
        }

        public static ControlTransformAxisMode AxisMode
        {
            get => GetOrCreateAsset().AxisMode;
            set
            {
                ControlTransformSettingsAsset asset = GetOrCreateAsset();
                if (asset.AxisMode == value)
                {
                    return;
                }

                Undo.RecordObject(asset, "Change Control Transform Axis Mode");
                asset.AxisMode = value;
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
            }
        }

        public static bool EnableSelectionTool => GetOrCreateAsset().EnableSelectionTool;

        public static bool EnableTransformOperations => GetOrCreateAsset().EnableTransformOperations;

        public static bool EnableResetAndVisibilityShortcuts => GetOrCreateAsset().EnableResetAndVisibilityShortcuts;

        public static bool EnableDeleteShortcut => GetOrCreateAsset().EnableDeleteShortcut;

        public static bool EnableGlobalLocalToggleShortcut => GetOrCreateAsset().EnableGlobalLocalToggleShortcut;

        public static ControlUnityAxis MapUserAxis(ControlUserAxis userAxis)
        {
            if (AxisMode == ControlTransformAxisMode.UnityAxis)
            {
                return (ControlUnityAxis)userAxis;
            }

            switch (userAxis)
            {
                case ControlUserAxis.X:
                    return ControlUnityAxis.X;
                case ControlUserAxis.Y:
                    return ControlUnityAxis.Z;
                default:
                    return ControlUnityAxis.Y;
            }
        }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            return new SettingsProvider("Project/Control Tools/Blender Transformations", SettingsScope.Project)
            {
                label = "Blender Transformations",
                keywords = new[] { "Control", "Transform", "Shortcut", "Blender", "Axis" },
                guiHandler = _ =>
                {
                    ControlTransformSettingsAsset asset = GetOrCreateAsset();
                    SerializedObject serializedAsset = new SerializedObject(asset);
                    SerializedProperty axisMode = serializedAsset.FindProperty("axisMode");
                    SerializedProperty enableSelectionTool = serializedAsset.FindProperty("enableSelectionTool");
                    SerializedProperty enableTransformOperations = serializedAsset.FindProperty("enableTransformOperations");
                    SerializedProperty enableResetAndVisibilityShortcuts = serializedAsset.FindProperty("enableResetAndVisibilityShortcuts");
                    SerializedProperty enableDeleteShortcut = serializedAsset.FindProperty("enableDeleteShortcut");
                    SerializedProperty enableGlobalLocalToggleShortcut = serializedAsset.FindProperty("enableGlobalLocalToggleShortcut");

                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(axisMode, new GUIContent("Axis Mode"));
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Shortcut Features", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(
                        enableSelectionTool,
                        new GUIContent(
                            "Selection Tool",
                            "Enable the custom Unity 6 Scene View selection tool and its V shortcut."));
                    EditorGUILayout.PropertyField(
                        enableTransformOperations,
                        new GUIContent(
                            "Grab/Rotate/Scale Operations",
                            "Enable the G, R, and S Scene View shortcuts for Control Move, Rotate, and Scale sessions."));
                    EditorGUILayout.PropertyField(
                        enableResetAndVisibilityShortcuts,
                        new GUIContent(
                            "Alt + G/R/S/H Reset Shortcuts",
                            "Enable Alt+G, Alt+R, Alt+S, H, and Alt+H for resetting local transforms, hiding the selection, and unhiding all Scene objects."));
                    EditorGUILayout.PropertyField(
                        enableDeleteShortcut,
                        new GUIContent(
                            "Delete With X Shortcut",
                            "Enable X to delete the current selection when no modal transform session is active."));
                    EditorGUILayout.PropertyField(
                        enableGlobalLocalToggleShortcut,
                        new GUIContent(
                            "Comma Global/Local Toggle",
                            "Enable comma to toggle Unity's pivot rotation between Global and Local."));
                    if (EditorGUI.EndChangeCheck())
                    {
                        serializedAsset.ApplyModifiedProperties();
                        EditorUtility.SetDirty(asset);
                        AssetDatabase.SaveAssets();
                        ControlTransformShortcutInstaller.ScheduleInstall();
                        SceneView.RepaintAll();
                    }

                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.ObjectField("Settings Asset", asset, typeof(ControlTransformSettingsAsset), false);
                    }

                    EditorGUILayout.Space();
                    EditorGUILayout.HelpBox(
                        "This is stored in a Unity asset so the axis mode can be moved with the plugin/project.\n\n" +
                        "Unity Axis: X=Unity X, Y=Unity Y, Z=Unity Z.\n" +
                        "Blender Axis: X=Unity X, Y=Unity Z, Z=Unity Y.",
                        MessageType.Info);
                }
            };
        }

        public static ControlTransformSettingsAsset GetOrCreateAsset()
        {
            if (cachedAsset != null)
            {
                return cachedAsset;
            }

            cachedAsset = AssetDatabase.LoadAssetAtPath<ControlTransformSettingsAsset>(DefaultSettingsPath);
            if (cachedAsset != null)
            {
                return cachedAsset;
            }

            string[] guids = AssetDatabase.FindAssets("t:ControlTransformSettingsAsset");
            if (guids.Length > 0)
            {
                cachedAsset = AssetDatabase.LoadAssetAtPath<ControlTransformSettingsAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
                if (cachedAsset != null)
                {
                    return cachedAsset;
                }
            }

            EnsureDefaultFolder();

            cachedAsset = ScriptableObject.CreateInstance<ControlTransformSettingsAsset>();

            // One-time migration from the first EditorPrefs implementation.
            cachedAsset.AxisMode = (ControlTransformAxisMode)EditorPrefs.GetInt(AxisModeKey, (int)ControlTransformAxisMode.UnityAxis);

            AssetDatabase.CreateAsset(cachedAsset, DefaultSettingsPath);
            AssetDatabase.SaveAssets();
            return cachedAsset;
        }

        private static void EnsureDefaultFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Control Tools"))
            {
                AssetDatabase.CreateFolder("Assets", "Control Tools");
            }

            if (!AssetDatabase.IsValidFolder("Assets/Control Tools/Blender Transformations"))
            {
                AssetDatabase.CreateFolder("Assets/Control Tools", "Blender Transformations");
            }
        }
    }
}
#endif




