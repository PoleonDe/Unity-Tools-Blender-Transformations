#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Control.Tools.BlenderTransformations
{
    [InitializeOnLoad]
    internal static class ControlTransformTool
    {
        internal const string MoveShortcutId = "Control/Transform/Move";
        internal const string RotateShortcutId = "Control/Transform/Rotate";
        internal const string ScaleShortcutId = "Control/Transform/Scale";
        internal const string ResetPositionShortcutId = "Control/Transform/Reset Local Position";
        internal const string ResetRotationShortcutId = "Control/Transform/Reset Local Rotation";
        internal const string ResetScaleShortcutId = "Control/Transform/Reset Local Scale";
        internal const string ToggleGlobalLocalShortcutId = "Control/Transform/Toggle Global Local";
        internal const string DeleteSelectionShortcutId = "Control/Transform/Delete Selection";
        internal const string HideSelectionShortcutId = "Control/Transform/Hide Selection";
        internal const string ShowAllHiddenShortcutId = "Control/Transform/Show All Hidden";
        internal const string SelectToolShortcutId = "Control/Transform/Select Tool";

        private static readonly ControlCursorWrap cursorWrap = new ControlCursorWrap();
        private static ControlTransformSession activeSession;
        private static SceneView activeSceneView;
        private static Vector2 lastSceneMousePosition;
        private static bool activePrecision;
        private static bool previousSceneWantsMouseMove;
        private static bool hasPreviousSceneWantsMouseMove;
        private static bool hasPendingSampledMousePosition;
        private static bool previousToolsHidden;
        private static bool hasPreviousToolsHidden;

        static ControlTransformTool()
        {
            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            EditorApplication.quitting += HandleEditorQuitting;
            EditorApplication.update += MonitorActiveSession;
            Selection.selectionChanged += HandleSelectionChanged;

            SceneView.duringSceneGui -= OnSceneGui;
            SceneView.duringSceneGui += OnSceneGui;
        }

        [Shortcut(MoveShortcutId, typeof(SceneView))]
        private static void MoveShortcut(ShortcutArguments arguments)
        {
            if (!ControlTransformSettings.EnableTransformOperations)
            {
                return;
            }

            BeginShortcutSession(arguments, ControlTransformMode.Move);
        }

        [Shortcut(RotateShortcutId, typeof(SceneView))]
        private static void RotateShortcut(ShortcutArguments arguments)
        {
            if (!ControlTransformSettings.EnableTransformOperations)
            {
                return;
            }

            BeginShortcutSession(arguments, ControlTransformMode.Rotate);
        }

        [Shortcut(ScaleShortcutId, typeof(SceneView))]
        private static void ScaleShortcut(ShortcutArguments arguments)
        {
            if (!ControlTransformSettings.EnableTransformOperations)
            {
                return;
            }

            BeginShortcutSession(arguments, ControlTransformMode.Scale);
        }

        [Shortcut(ResetPositionShortcutId, typeof(SceneView))]
        private static void ResetPositionShortcut(ShortcutArguments arguments)
        {
            if (!ControlTransformSettings.EnableResetAndVisibilityShortcuts)
            {
                return;
            }

            if (CanHandleShortcut(ResolveSceneView(arguments)))
            {
                ResetLocalPosition();
                UseCurrentEvent();
            }
        }

        [Shortcut(ResetRotationShortcutId, typeof(SceneView))]
        private static void ResetRotationShortcut(ShortcutArguments arguments)
        {
            if (!ControlTransformSettings.EnableResetAndVisibilityShortcuts)
            {
                return;
            }

            if (CanHandleShortcut(ResolveSceneView(arguments)))
            {
                ResetLocalRotation();
                UseCurrentEvent();
            }
        }

        [Shortcut(ResetScaleShortcutId, typeof(SceneView))]
        private static void ResetScaleShortcut(ShortcutArguments arguments)
        {
            if (!ControlTransformSettings.EnableResetAndVisibilityShortcuts)
            {
                return;
            }

            if (CanHandleShortcut(ResolveSceneView(arguments)))
            {
                ResetLocalScale();
                UseCurrentEvent();
            }
        }

        [Shortcut(ToggleGlobalLocalShortcutId, typeof(SceneView))]
        private static void ToggleGlobalLocalShortcut(ShortcutArguments arguments)
        {
            if (!ControlTransformSettings.EnableGlobalLocalToggleShortcut)
            {
                return;
            }

            if (CanHandleSceneShortcut(ResolveSceneView(arguments)))
            {
                TogglePivotRotation();
                UseCurrentEvent();
            }
        }

        [Shortcut(DeleteSelectionShortcutId, typeof(SceneView))]
        private static void DeleteSelectionShortcut(ShortcutArguments arguments)
        {
            if (!ControlTransformSettings.EnableDeleteShortcut)
            {
                return;
            }

            if (CanHandleShortcut(ResolveSceneView(arguments)))
            {
                DeleteSelectedObjects();
                UseCurrentEvent();
            }
        }

        [Shortcut(HideSelectionShortcutId, typeof(SceneView))]
        private static void HideSelectionShortcut(ShortcutArguments arguments)
        {
            if (!ControlTransformSettings.EnableResetAndVisibilityShortcuts)
            {
                return;
            }

            if (CanHandleShortcut(ResolveSceneView(arguments)))
            {
                HideSelectedObjects();
                UseCurrentEvent();
            }
        }

        [Shortcut(ShowAllHiddenShortcutId, typeof(SceneView))]
        private static void ShowAllHiddenShortcut(ShortcutArguments arguments)
        {
            if (!ControlTransformSettings.EnableResetAndVisibilityShortcuts)
            {
                return;
            }

            if (CanHandleSceneShortcut(ResolveSceneView(arguments)))
            {
                ShowAllHiddenObjects();
                UseCurrentEvent();
            }
        }

#if UNITY_6000_0_OR_NEWER
        [Shortcut(SelectToolShortcutId, typeof(SceneView))]
        private static void SelectToolShortcut(ShortcutArguments arguments)
        {
            if (!ControlTransformSettings.EnableSelectionTool)
            {
                return;
            }

            SceneView sceneView = ResolveSceneView(arguments);
            if (CanReceiveSceneKeyEvent(sceneView))
            {
                ToolManager.SetActiveTool<ControlSelectTool>();
                UseCurrentEvent();
            }
        }
#endif

        private static void BeginShortcutSession(ShortcutArguments arguments, ControlTransformMode mode)
        {
            SceneView sceneView = ResolveSceneView(arguments);
            if (CanHandleShortcut(sceneView))
            {
                Vector2 mousePosition = Event.current != null
                    ? Event.current.mousePosition
                    : lastSceneMousePosition;

                BeginSession(mode, sceneView, mousePosition);
            }
        }

        private static SceneView ResolveSceneView(ShortcutArguments arguments)
        {
            return arguments.context as SceneView ?? EditorWindow.focusedWindow as SceneView;
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            Event current = Event.current;
            if (current == null)
            {
                return;
            }

            if (current.isMouse && activeSession == null)
            {
                lastSceneMousePosition = current.mousePosition;
            }

            if (activeSession == null && HandleAltResetOrMenuSuppression(sceneView, current))
            {
                return;
            }

            int controlId = GUIUtility.GetControlID("ControlTransformTool".GetHashCode(), FocusType.Passive);

            if (activeSession != null)
            {
                if (sceneView != activeSceneView)
                {
                    return;
                }

                HandleActiveSession(sceneView, current, controlId);
                return;
            }

            if (current.type == EventType.Repaint)
            {
                return;
            }

            if (!CanStartFromSceneView(sceneView, current))
            {
                return;
            }

            // Fallback for Unity versions/layouts where Scene GUI receives the key first.
            HandleIdleShortcut(sceneView, current);
        }

        private static void HandleActiveSession(SceneView sceneView, Event current, int controlId)
        {
            cursorWrap.Update(current, sceneView);

            if (hasPendingSampledMousePosition && current.type == EventType.Layout)
            {
                activeSession.Update(sceneView, lastSceneMousePosition, activePrecision);
                hasPendingSampledMousePosition = false;
            }

            if (current.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(controlId);
            }

            if (current.type == EventType.Repaint)
            {
                ControlTransformDrawing.Draw(activeSession, sceneView);
            }

            if (current.type == EventType.MouseMove || current.type == EventType.MouseDrag)
            {
                activePrecision = current.shift;
                Vector2 mousePosition = current.mousePosition;
                if (cursorWrap.TrySampleMouse(out Vector2 sampledMousePosition))
                {
                    mousePosition = sampledMousePosition;
                }

                lastSceneMousePosition = mousePosition;
                activeSession.Update(sceneView, mousePosition, activePrecision);
                sceneView.Repaint();
                current.Use();
                return;
            }

            if (current.type == EventType.MouseDown && current.button == 0)
            {
                ConfirmSession();
                current.Use();
                return;
            }

            if (current.type == EventType.MouseDown && current.button == 1)
            {
                CancelSession();
                current.Use();
                return;
            }

            if (current.isMouse ||
                current.type == EventType.ScrollWheel ||
                current.type == EventType.ContextClick)
            {
                sceneView.Repaint();
                current.Use();
                return;
            }

            if (current.type != EventType.KeyDown)
            {
                if (current.type == EventType.KeyUp &&
                    (current.keyCode == KeyCode.LeftShift || current.keyCode == KeyCode.RightShift))
                {
                    activePrecision = false;
                    activeSession.Update(sceneView, lastSceneMousePosition, activePrecision);
                    sceneView.Repaint();
                    current.Use();
                }

                if (activeSession.ShouldBlockNumericShortcut(current))
                {
                    sceneView.Repaint();
                    current.Use();
                    return;
                }

                sceneView.Repaint();
                return;
            }

            if (current.keyCode == KeyCode.LeftShift || current.keyCode == KeyCode.RightShift)
            {
                activePrecision = true;
                activeSession.Update(sceneView, lastSceneMousePosition, activePrecision);
                sceneView.Repaint();
                current.Use();
                return;
            }

            if (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter)
            {
                ConfirmSession();
                current.Use();
                return;
            }

            if (current.keyCode == KeyCode.Escape)
            {
                CancelSession();
                current.Use();
                return;
            }

            if (activeSession.HandleNumericKey(current, sceneView, lastSceneMousePosition, activePrecision))
            {
                sceneView.Repaint();
                current.Use();
                return;
            }

            if (activeSession.ShouldBlockNumericShortcut(current))
            {
                sceneView.Repaint();
                current.Use();
                return;
            }

            if (TryGetAxisKey(current.keyCode, out ControlUserAxis axis))
            {
                activeSession.HandleAxisKey(axis, current.shift, sceneView, lastSceneMousePosition, activePrecision);
                sceneView.Repaint();
                current.Use();
                return;
            }

            if (current.keyCode == KeyCode.Delete)
            {
                activeSession.ClearConstraint(sceneView, lastSceneMousePosition, activePrecision);
                sceneView.Repaint();
                current.Use();
            }
        }

        private static void HandleIdleShortcut(SceneView sceneView, Event current)
        {
            if (current.type != EventType.KeyDown)
            {
                return;
            }

#if UNITY_6000_0_OR_NEWER
            if (ControlTransformSettings.EnableSelectionTool &&
                !current.alt &&
                !current.control &&
                !current.command &&
                current.keyCode == KeyCode.V)
            {
                ToolManager.SetActiveTool<ControlSelectTool>();
                current.Use();
                return;
            }
#endif

            if (ControlTransformSettings.EnableGlobalLocalToggleShortcut &&
                !current.alt &&
                !current.control &&
                !current.command &&
                current.keyCode == KeyCode.Comma)
            {
                TogglePivotRotation();
                current.Use();
                return;
            }

            if (Selection.transforms == null || Selection.transforms.Length == 0)
            {
                if (ControlTransformSettings.EnableResetAndVisibilityShortcuts &&
                    current.alt &&
                    current.keyCode == KeyCode.H)
                {
                    ShowAllHiddenObjects();
                    current.Use();
                }

                return;
            }

            if (ControlTransformSettings.EnableResetAndVisibilityShortcuts && current.alt)
            {
                if (current.keyCode == KeyCode.G)
                {
                    ResetLocalPosition();
                    current.Use();
                    return;
                }

                if (current.keyCode == KeyCode.R)
                {
                    ResetLocalRotation();
                    current.Use();
                    return;
                }

                if (current.keyCode == KeyCode.S)
                {
                    ResetLocalScale();
                    current.Use();
                    return;
                }

                if (current.keyCode == KeyCode.H)
                {
                    ShowAllHiddenObjects();
                    current.Use();
                }

                return;
            }

            if (current.alt)
            {
                return;
            }

            if (current.control || current.command)
            {
                return;
            }

            if (ControlTransformSettings.EnableDeleteShortcut && current.keyCode == KeyCode.X)
            {
                DeleteSelectedObjects();
                current.Use();
                return;
            }

            if (ControlTransformSettings.EnableResetAndVisibilityShortcuts && current.keyCode == KeyCode.H)
            {
                HideSelectedObjects();
                current.Use();
                return;
            }

            if (!ControlTransformSettings.EnableTransformOperations)
            {
                return;
            }

            if (current.keyCode == KeyCode.G)
            {
                BeginSession(ControlTransformMode.Move, sceneView, current.mousePosition);
                current.Use();
                return;
            }

            if (current.keyCode == KeyCode.R)
            {
                BeginSession(ControlTransformMode.Rotate, sceneView, current.mousePosition);
                current.Use();
                return;
            }

            if (current.keyCode == KeyCode.S)
            {
                BeginSession(ControlTransformMode.Scale, sceneView, current.mousePosition);
                current.Use();
            }
        }

        private static bool HandleAltResetOrMenuSuppression(SceneView sceneView, Event current)
        {
            if (!ControlTransformSettings.EnableResetAndVisibilityShortcuts || !CanReceiveSceneKeyEvent(sceneView))
            {
                return false;
            }

            if (current.type != EventType.KeyDown && current.type != EventType.KeyUp)
            {
                return false;
            }

            if (current.keyCode == KeyCode.LeftAlt || current.keyCode == KeyCode.RightAlt)
            {
                current.Use();
                return true;
            }

            if (!current.alt)
            {
                return false;
            }

            bool hasSelection = Selection.transforms != null && Selection.transforms.Length > 0;

            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.G)
            {
                if (hasSelection)
                {
                    ResetLocalPosition();
                    current.Use();
                    return true;
                }

                return false;
            }

            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.R)
            {
                if (hasSelection)
                {
                    ResetLocalRotation();
                    current.Use();
                    return true;
                }

                return false;
            }

            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.S)
            {
                if (hasSelection)
                {
                    ResetLocalScale();
                    current.Use();
                    return true;
                }

                return false;
            }

            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.H)
            {
                ShowAllHiddenObjects();
                current.Use();
                return true;
            }

            return false;
        }

        private static void BeginSession(ControlTransformMode mode, SceneView sceneView, Vector2 mousePosition)
        {
            Transform[] transforms = Selection.transforms;
            if (transforms == null || transforms.Length == 0)
            {
                return;
            }

            activeSession = new ControlTransformSession(
                mode,
                transforms,
                Selection.activeTransform,
                sceneView,
                mousePosition);

            activeSceneView = sceneView;
            activeSceneView.Focus();
            previousSceneWantsMouseMove = sceneView.wantsMouseMove;
            hasPreviousSceneWantsMouseMove = true;
            previousToolsHidden = UnityEditor.Tools.hidden;
            hasPreviousToolsHidden = true;
            UnityEditor.Tools.hidden = true;
            sceneView.wantsMouseMove = true;
            activePrecision = false;
            cursorWrap.Begin(sceneView, mousePosition);
            activeSession.Update(sceneView, mousePosition, false);
            sceneView.Repaint();
        }

        private static void ConfirmSession()
        {
            EndSession(false);
        }

        private static void CancelSession()
        {
            EndSession(true);
        }

        private static void TogglePivotRotation()
        {
            UnityEditor.Tools.pivotRotation = UnityEditor.Tools.pivotRotation == PivotRotation.Global
                ? PivotRotation.Local
                : PivotRotation.Global;

            SceneView.RepaintAll();
        }

        private static void ResetLocalPosition()
        {
            Transform[] transforms = Selection.transforms;
            Undo.RecordObjects(transforms, "Reset Local Position");
            for (int i = 0; i < transforms.Length; i++)
            {
                transforms[i].localPosition = Vector3.zero;
            }

            SceneView.RepaintAll();
        }

        private static void ResetLocalRotation()
        {
            Transform[] transforms = Selection.transforms;
            Undo.RecordObjects(transforms, "Reset Local Rotation");
            for (int i = 0; i < transforms.Length; i++)
            {
                transforms[i].localRotation = Quaternion.identity;
            }

            SceneView.RepaintAll();
        }

        private static void ResetLocalScale()
        {
            Transform[] transforms = Selection.transforms;
            Undo.RecordObjects(transforms, "Reset Local Scale");
            for (int i = 0; i < transforms.Length; i++)
            {
                transforms[i].localScale = Vector3.one;
            }

            SceneView.RepaintAll();
        }

        private static void HideSelectedObjects()
        {
            GameObject[] gameObjects = Selection.gameObjects;
            if (gameObjects == null || gameObjects.Length == 0)
            {
                return;
            }

            SceneVisibilityManager visibilityManager = SceneVisibilityManager.instance;
            for (int i = 0; i < gameObjects.Length; i++)
            {
                if (gameObjects[i] != null)
                {
                    visibilityManager.Hide(gameObjects[i], true);
                }
            }

            SceneView.RepaintAll();
        }

        private static void ShowAllHiddenObjects()
        {
            SceneVisibilityManager.instance.ShowAll();
            SceneView.RepaintAll();
        }

        private static void DeleteSelectedObjects()
        {
            GameObject[] gameObjects = Selection.gameObjects;
            if (gameObjects == null || gameObjects.Length == 0)
            {
                return;
            }

            List<GameObject> deleteTargets = new List<GameObject>(gameObjects.Length);
            HashSet<GameObject> seenObjects = new HashSet<GameObject>();

            for (int i = 0; i < gameObjects.Length; i++)
            {
                GameObject gameObject = gameObjects[i];
                if (gameObject != null && seenObjects.Add(gameObject))
                {
                    deleteTargets.Add(gameObject);
                }
            }

            deleteTargets.Sort((left, right) => GetHierarchyDepth(right.transform).CompareTo(GetHierarchyDepth(left.transform)));

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Delete Selected Objects");

            for (int i = 0; i < deleteTargets.Count; i++)
            {
                if (deleteTargets[i] != null)
                {
                    Undo.DestroyObjectImmediate(deleteTargets[i]);
                }
            }

            SceneView.RepaintAll();
        }

        private static bool CanStartFromSceneView(SceneView sceneView, Event current)
        {
            if (!CanHandleSceneShortcut(sceneView))
            {
                return false;
            }

            if (current.type == EventType.Used || current.type == EventType.Ignore)
            {
                return false;
            }

            return true;
        }

        private static bool CanHandleShortcut(SceneView sceneView)
        {
            return CanHandleSceneShortcut(sceneView) &&
                Selection.transforms != null &&
                Selection.transforms.Length > 0;
        }

        private static bool CanHandleSceneShortcut(SceneView sceneView)
        {
            if (!CanReceiveSceneKeyEvent(sceneView))
            {
                return false;
            }

            return activeSession == null;
        }

        private static bool CanReceiveSceneKeyEvent(SceneView sceneView)
        {
            if (sceneView == null || EditorWindow.focusedWindow != sceneView)
            {
                return false;
            }

            if (GUIUtility.hotControl != 0 || EditorGUIUtility.editingTextField)
            {
                return false;
            }

            if (UnityEditor.Tools.viewToolActive)
            {
                return false;
            }

            return true;
        }

        private static void UseCurrentEvent()
        {
            Event current = Event.current;
            if (current != null &&
                current.type != EventType.Used &&
                (current.type == EventType.KeyDown || current.type == EventType.KeyUp))
            {
                current.Use();
            }
        }

        private static void RestoreSceneMouseMove()
        {
            if (activeSceneView != null)
            {
                activeSceneView.wantsMouseMove = hasPreviousSceneWantsMouseMove
                    ? previousSceneWantsMouseMove
                    : false;
                activeSceneView = null;
            }

            hasPreviousSceneWantsMouseMove = false;
            previousSceneWantsMouseMove = false;
            hasPendingSampledMousePosition = false;
        }

        private static void RestoreUnityToolVisibility()
        {
            if (hasPreviousToolsHidden)
            {
                UnityEditor.Tools.hidden = previousToolsHidden;
            }

            hasPreviousToolsHidden = false;
            previousToolsHidden = false;
        }

        private static void EndSession(bool cancel)
        {
            if (cancel)
            {
                activeSession?.Cancel();
            }

            activeSession?.RestorePivotRotation();
            activeSession = null;
            cursorWrap.End();
            RestoreUnityToolVisibility();
            RestoreSceneMouseMove();
            SceneView.RepaintAll();
        }

        private static void MonitorActiveSession()
        {
            if (activeSession == null)
            {
                return;
            }

            if (activeSceneView == null || EditorWindow.focusedWindow != activeSceneView)
            {
                CancelSession();
                return;
            }

            if (cursorWrap.TrySampleMouse(out Vector2 mousePosition))
            {
                lastSceneMousePosition = mousePosition;
                hasPendingSampledMousePosition = true;
                activeSceneView.Repaint();
            }
        }

        private static void HandleSelectionChanged()
        {
            if (activeSession != null)
            {
                CancelSession();
            }
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (activeSession != null)
            {
                CancelSession();
            }
        }

        private static void HandleBeforeAssemblyReload()
        {
            if (activeSession != null)
            {
                CancelSession();
            }
        }

        private static void HandleEditorQuitting()
        {
            if (activeSession != null)
            {
                CancelSession();
            }
        }

        private static bool TryGetAxisKey(KeyCode keyCode, out ControlUserAxis axis)
        {
            switch (keyCode)
            {
                case KeyCode.X:
                    axis = ControlUserAxis.X;
                    return true;
                case KeyCode.Y:
                    axis = ControlUserAxis.Y;
                    return true;
                case KeyCode.Z:
                    axis = ControlUserAxis.Z;
                    return true;
                default:
                    axis = ControlUserAxis.X;
                    return false;
            }
        }

        private static int GetHierarchyDepth(Transform transform)
        {
            int depth = 0;
            while (transform != null)
            {
                depth++;
                transform = transform.parent;
            }

            return depth;
        }
    }
}
#endif


