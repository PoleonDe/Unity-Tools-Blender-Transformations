#if UNITY_EDITOR
using UnityEngine;

namespace Control.Tools.BlenderTransformations
{
    [CreateAssetMenu(
        fileName = "ControlTransformSettings",
        menuName = "Control Tools/Blender Transformations Settings")]
    public sealed class ControlTransformSettingsAsset : ScriptableObject
    {
        [SerializeField]
        private ControlTransformAxisMode axisMode = ControlTransformAxisMode.UnityAxis;

        [SerializeField]
        private bool enableSelectionTool = true;

        [SerializeField]
        private bool enableTransformOperations = true;

        [SerializeField]
        private bool enableResetAndVisibilityShortcuts = true;

        [SerializeField]
        private bool enableDeleteShortcut = true;

        [SerializeField]
        private bool enableGlobalLocalToggleShortcut = true;

        public ControlTransformAxisMode AxisMode
        {
            get => axisMode;
            set => axisMode = value;
        }

        public bool EnableSelectionTool
        {
            get => enableSelectionTool;
            set => enableSelectionTool = value;
        }

        public bool EnableTransformOperations
        {
            get => enableTransformOperations;
            set => enableTransformOperations = value;
        }

        public bool EnableResetAndVisibilityShortcuts
        {
            get => enableResetAndVisibilityShortcuts;
            set => enableResetAndVisibilityShortcuts = value;
        }

        public bool EnableDeleteShortcut
        {
            get => enableDeleteShortcut;
            set => enableDeleteShortcut = value;
        }

        public bool EnableGlobalLocalToggleShortcut
        {
            get => enableGlobalLocalToggleShortcut;
            set => enableGlobalLocalToggleShortcut = value;
        }
    }
}
#endif



