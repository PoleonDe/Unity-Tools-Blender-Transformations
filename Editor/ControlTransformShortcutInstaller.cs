#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Control.Tools.BlenderTransformations
{
    [InitializeOnLoad]
    internal static class ControlTransformShortcutInstaller
    {
        private const string ManagedConflictIdsKey = "Control.TransformShortcut.ManagedConflictIds";

        private static readonly ShortcutBinding MoveBinding =
            new ShortcutBinding(new KeyCombination(KeyCode.G, ShortcutModifiers.None));

        private static readonly ShortcutBinding RotateBinding =
            new ShortcutBinding(new KeyCombination(KeyCode.R, ShortcutModifiers.None));

        private static readonly ShortcutBinding ScaleBinding =
            new ShortcutBinding(new KeyCombination(KeyCode.S, ShortcutModifiers.None));

        private static readonly ShortcutBinding ResetPositionBinding =
            new ShortcutBinding(new KeyCombination(KeyCode.G, ShortcutModifiers.Alt));

        private static readonly ShortcutBinding ResetRotationBinding =
            new ShortcutBinding(new KeyCombination(KeyCode.R, ShortcutModifiers.Alt));

        private static readonly ShortcutBinding ResetScaleBinding =
            new ShortcutBinding(new KeyCombination(KeyCode.S, ShortcutModifiers.Alt));

        private static readonly ShortcutBinding ToggleGlobalLocalBinding =
            new ShortcutBinding(new KeyCombination(KeyCode.Comma, ShortcutModifiers.None));

        private static readonly ShortcutBinding DeleteSelectionBinding =
            new ShortcutBinding(new KeyCombination(KeyCode.X, ShortcutModifiers.None));

        private static readonly ShortcutBinding HideSelectionBinding =
            new ShortcutBinding(new KeyCombination(KeyCode.H, ShortcutModifiers.None));

        private static readonly ShortcutBinding ShowAllHiddenBinding =
            new ShortcutBinding(new KeyCombination(KeyCode.H, ShortcutModifiers.Alt));

        private static readonly ShortcutBinding SelectToolBinding =
            new ShortcutBinding(new KeyCombination(KeyCode.V, ShortcutModifiers.None));

        static ControlTransformShortcutInstaller()
        {
            // Shortcut discovery finishes after domain reload. Installing one tick later avoids
            // Unity's conflict dialog because our shortcut attributes do not declare default keys.
            ScheduleInstall();
        }

        internal static void ScheduleInstall()
        {
            EditorApplication.delayCall -= Install;
            EditorApplication.delayCall += Install;
        }

        private static void Install()
        {
            try
            {
                RestoreManagedConflicts();
                RepairLegacyEmptyConflictOverrides();
                HashSet<string> managedConflictIds = ClearKnownSceneViewConflicts(GetEnabledBindings());
                BindControlShortcuts();
                SaveManagedConflictIds(managedConflictIds);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Control transform shortcut installation failed: " + exception.Message);
            }
        }

        private static HashSet<ShortcutBinding> GetEnabledBindings()
        {
            HashSet<ShortcutBinding> enabledBindings = new HashSet<ShortcutBinding>();

            if (ControlTransformSettings.EnableTransformOperations)
            {
                enabledBindings.Add(MoveBinding);
                enabledBindings.Add(RotateBinding);
                enabledBindings.Add(ScaleBinding);
            }

            if (ControlTransformSettings.EnableResetAndVisibilityShortcuts)
            {
                enabledBindings.Add(ResetPositionBinding);
                enabledBindings.Add(ResetRotationBinding);
                enabledBindings.Add(ResetScaleBinding);
                enabledBindings.Add(HideSelectionBinding);
                enabledBindings.Add(ShowAllHiddenBinding);
            }

            if (ControlTransformSettings.EnableGlobalLocalToggleShortcut)
            {
                enabledBindings.Add(ToggleGlobalLocalBinding);
            }

            if (ControlTransformSettings.EnableDeleteShortcut)
            {
                enabledBindings.Add(DeleteSelectionBinding);
            }

#if UNITY_6000_0_OR_NEWER
            if (ControlTransformSettings.EnableSelectionTool)
            {
                enabledBindings.Add(SelectToolBinding);
            }
#endif

            return enabledBindings;
        }

        private static HashSet<string> ClearKnownSceneViewConflicts(HashSet<ShortcutBinding> enabledBindings)
        {
            HashSet<string> managedConflictIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (string shortcutId in ShortcutManager.instance.GetAvailableShortcutIds())
            {
                if (IsControlShortcut(shortcutId) || !CanOwnShortcut(shortcutId))
                {
                    continue;
                }

                ShortcutBinding binding = ShortcutManager.instance.GetShortcutBinding(shortcutId);
                if (UsesControlBinding(binding, enabledBindings))
                {
                    ShortcutManager.instance.RebindShortcut(shortcutId, ShortcutBinding.empty);
                    managedConflictIds.Add(shortcutId);
                }
            }

            return managedConflictIds;
        }

        private static void BindControlShortcuts()
        {
            ApplyControlBinding(ControlTransformTool.MoveShortcutId, MoveBinding, ControlTransformSettings.EnableTransformOperations);
            ApplyControlBinding(ControlTransformTool.RotateShortcutId, RotateBinding, ControlTransformSettings.EnableTransformOperations);
            ApplyControlBinding(ControlTransformTool.ScaleShortcutId, ScaleBinding, ControlTransformSettings.EnableTransformOperations);
            ApplyControlBinding(ControlTransformTool.ResetPositionShortcutId, ResetPositionBinding, ControlTransformSettings.EnableResetAndVisibilityShortcuts);
            ApplyControlBinding(ControlTransformTool.ResetRotationShortcutId, ResetRotationBinding, ControlTransformSettings.EnableResetAndVisibilityShortcuts);
            ApplyControlBinding(ControlTransformTool.ResetScaleShortcutId, ResetScaleBinding, ControlTransformSettings.EnableResetAndVisibilityShortcuts);
            ApplyControlBinding(ControlTransformTool.ToggleGlobalLocalShortcutId, ToggleGlobalLocalBinding, ControlTransformSettings.EnableGlobalLocalToggleShortcut);
            ApplyControlBinding(ControlTransformTool.DeleteSelectionShortcutId, DeleteSelectionBinding, ControlTransformSettings.EnableDeleteShortcut);
            ApplyControlBinding(ControlTransformTool.HideSelectionShortcutId, HideSelectionBinding, ControlTransformSettings.EnableResetAndVisibilityShortcuts);
            ApplyControlBinding(ControlTransformTool.ShowAllHiddenShortcutId, ShowAllHiddenBinding, ControlTransformSettings.EnableResetAndVisibilityShortcuts);
#if UNITY_6000_0_OR_NEWER
            ApplyControlBinding(ControlTransformTool.SelectToolShortcutId, SelectToolBinding, ControlTransformSettings.EnableSelectionTool);
#endif
        }

        private static void ApplyControlBinding(string shortcutId, ShortcutBinding binding, bool enabled)
        {
            if (enabled)
            {
                ShortcutManager.instance.RebindShortcut(shortcutId, binding);
                return;
            }

            if (ShortcutManager.instance.IsShortcutOverridden(shortcutId))
            {
                ShortcutManager.instance.ClearShortcutOverride(shortcutId);
            }
        }

        private static bool IsControlShortcut(string shortcutId)
        {
            return shortcutId.StartsWith("Control/Transform/", StringComparison.Ordinal);
        }

        private static bool CanOwnShortcut(string shortcutId)
        {
            // Keep this intentionally narrow: only remove Unity editor bindings that can fire in
            // Scene View for tool/overlay behavior. Graph, Timeline, Tile Palette, and other window
            // shortcuts keep their bindings.
            return shortcutId.StartsWith("Tools/", StringComparison.Ordinal) ||
                   shortcutId.StartsWith("Overlays/", StringComparison.Ordinal) ||
                   shortcutId.StartsWith("Scene View/", StringComparison.Ordinal);
        }

        private static bool UsesControlBinding(ShortcutBinding binding, HashSet<ShortcutBinding> enabledBindings)
        {
            foreach (ShortcutBinding enabledBinding in enabledBindings)
            {
                if (BindingEquals(binding, enabledBinding))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool BindingEquals(ShortcutBinding left, ShortcutBinding right)
        {
            return left.Equals(right);
        }

        private static void RestoreManagedConflicts()
        {
            HashSet<string> availableShortcutIds = new HashSet<string>(
                ShortcutManager.instance.GetAvailableShortcutIds(),
                StringComparer.Ordinal);

            foreach (string shortcutId in LoadManagedConflictIds())
            {
                if (string.IsNullOrEmpty(shortcutId))
                {
                    continue;
                }

                if (!availableShortcutIds.Contains(shortcutId))
                {
                    continue;
                }

                if (ShortcutManager.instance.IsShortcutOverridden(shortcutId))
                {
                    ShortcutManager.instance.ClearShortcutOverride(shortcutId);
                }
            }

            SaveManagedConflictIds(Array.Empty<string>());
        }

        private static void RepairLegacyEmptyConflictOverrides()
        {
            foreach (string shortcutId in ShortcutManager.instance.GetAvailableShortcutIds())
            {
                if (!LooksLikeLegacyConflictCandidate(shortcutId) ||
                    !ShortcutManager.instance.IsShortcutOverridden(shortcutId))
                {
                    continue;
                }

                ShortcutBinding binding = ShortcutManager.instance.GetShortcutBinding(shortcutId);
                if (!binding.Equals(ShortcutBinding.empty))
                {
                    continue;
                }

                ShortcutManager.instance.ClearShortcutOverride(shortcutId);
            }
        }

        private static bool LooksLikeLegacyConflictCandidate(string shortcutId)
        {
            if (shortcutId.StartsWith("Tools/", StringComparison.Ordinal))
            {
                return shortcutId.IndexOf("Move", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       shortcutId.IndexOf("Rotate", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       shortcutId.IndexOf("Scale", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       shortcutId.IndexOf("Transform", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       shortcutId.IndexOf("Select", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            if (shortcutId.StartsWith("Scene View/", StringComparison.Ordinal))
            {
                return shortcutId.IndexOf("Global", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       shortcutId.IndexOf("Local", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return false;
        }

        private static IEnumerable<string> LoadManagedConflictIds()
        {
            string rawIds = EditorPrefs.GetString(ManagedConflictIdsKey, string.Empty);
            return string.IsNullOrEmpty(rawIds)
                ? Array.Empty<string>()
                : rawIds.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static void SaveManagedConflictIds(IEnumerable<string> shortcutIds)
        {
            EditorPrefs.SetString(ManagedConflictIdsKey, string.Join("\n", shortcutIds));
        }
    }
}
#endif


