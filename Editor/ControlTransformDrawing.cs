#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Control.Tools.BlenderTransformations
{
    internal static class ControlTransformDrawing
    {
        public static void Draw(ControlTransformSession session, SceneView sceneView)
        {
            if (session == null)
            {
                return;
            }

            CompareFunction previousZTest = Handles.zTest;
            Handles.zTest = CompareFunction.Always;

            float size = HandleUtility.GetHandleSize(session.Pivot);
            DrawPivot(session.Pivot, size);

            ControlAxisGuide[] guides = session.GetActiveGuides(sceneView);
            if (session.Mode == ControlTransformMode.Rotate)
            {
                DrawRotationGuide(session, guides, size);
            }
            else
            {
                for (int i = 0; i < guides.Length; i++)
                {
                    DrawAxis(session.Pivot, guides[i], size * 2.2f);
                }
            }

            Handles.zTest = previousZTest;

            if (session.NumericInputActive)
            {
                DrawNumericInputOverlay(session, sceneView);
            }
        }

        private static void DrawPivot(Vector3 pivot, float size)
        {
            Handles.color = new Color(1f, 1f, 1f, 0.85f);
            Handles.SphereHandleCap(0, pivot, Quaternion.identity, size * 0.08f, EventType.Repaint);
        }

        private static void DrawAxis(Vector3 pivot, ControlAxisGuide guide, float length)
        {
            Vector3 axis = guide.WorldAxis.normalized;
            Color color = AxisColor(guide.UserAxis);
            Handles.color = color;
            Handles.DrawAAPolyLine(5f, pivot - axis * length, pivot + axis * length);
        }

        private static void DrawRotationGuide(ControlTransformSession session, ControlAxisGuide[] guides, float size)
        {
            ControlAxisGuide guide = guides.Length > 0
                ? guides[0]
                : new ControlAxisGuide(ControlUserAxis.Z, Vector3.forward);

            Handles.color = session.ConstraintKind == ControlConstraintKind.Axis || session.Is2DMode
                ? AxisColor(guide.UserAxis)
                : new Color(1f, 0.85f, 0.2f, 0.95f);

            Handles.DrawWireDisc(session.Pivot, guide.WorldAxis.normalized, size * 1.35f);
        }

        private static Color AxisColor(ControlUserAxis axis)
        {
            switch (axis)
            {
                case ControlUserAxis.X:
                    return new Color(0.95f, 0.15f, 0.12f, 0.95f);
                case ControlUserAxis.Y:
                    return new Color(0.25f, 0.85f, 0.18f, 0.95f);
                default:
                    return new Color(0.2f, 0.45f, 1f, 0.95f);
            }
        }

        private static void DrawNumericInputOverlay(ControlTransformSession session, SceneView sceneView)
        {
            string text = session.NumericDisplayText;
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            Handles.BeginGUI();

            GUIStyle centered = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                normal = { textColor = Color.white }
            };

            GUIStyle small = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 12,
                normal = { textColor = Color.white }
            };

            Rect viewRect = sceneView.position;
            Rect centerRect = new Rect(
                (viewRect.width - 360f) * 0.5f,
                viewRect.height * 0.8f - 20f,
                360f,
                40f);

            DrawShadowLabel(centerRect, text, centered);

            Rect smallRect = new Rect(12f, 12f, 280f, 24f);
            DrawShadowLabel(smallRect, text, small);

            Handles.EndGUI();
        }

        private static void DrawShadowLabel(Rect rect, string text, GUIStyle style)
        {
            Color previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, style);
            GUI.color = Color.white;
            GUI.Label(rect, text, style);
            GUI.color = previousColor;
        }
    }
}
#endif


