#if UNITY_EDITOR
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace Control.Tools.BlenderTransformations
{
    internal readonly struct ControlCursorWrapResult
    {
        public ControlCursorWrapResult(Vector2 mousePosition, bool hasMotion)
        {
            MousePosition = mousePosition;
            HasMotion = hasMotion;
        }

        public Vector2 MousePosition { get; }
        public bool HasMotion { get; }
    }

    internal sealed class ControlCursorWrap
    {
        private const float WrapMargin = 3f;

        private static readonly PropertyInfo CameraViewportProperty = typeof(SceneView).GetProperty(
            "cameraViewport",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private SceneView activeSceneView;
        private Vector2 physicalMousePosition;
        private Vector2 stableMousePosition;
        private Vector2 previousScreenCursorPosition;
        private Rect cachedGuiBounds;
        private Rect cachedScreenBounds;
        private float cachedPixelsPerPoint;
        private bool hasCachedBounds;
        private bool hasPreviousScreenCursorPosition;

        public bool IsWrappingActive => activeSceneView != null;

        public void Begin(SceneView sceneView, Vector2 mousePosition)
        {
            activeSceneView = sceneView;
            physicalMousePosition = mousePosition;
            stableMousePosition = mousePosition;
            cachedGuiBounds = default;
            cachedScreenBounds = default;
            cachedPixelsPerPoint = 1f;
            hasCachedBounds = false;
            hasPreviousScreenCursorPosition = TryGetCursorScreenPosition(out previousScreenCursorPosition);

            if (activeSceneView != null)
            {
                activeSceneView.Focus();
            }
        }

        public ControlCursorWrapResult Update(Event current, SceneView sceneView)
        {
            if (activeSceneView == null || current == null || sceneView != activeSceneView)
            {
                return default;
            }

            if (EditorWindow.focusedWindow != sceneView)
            {
                sceneView.Focus();
            }

            CacheViewportBounds(sceneView);

            if (!ShouldEvaluateWarp(current.type))
            {
                return default;
            }

            if (!hasPreviousScreenCursorPosition)
            {
                hasPreviousScreenCursorPosition = TryGetCursorScreenPosition(out previousScreenCursorPosition);
            }

            return new ControlCursorWrapResult(stableMousePosition, false);
        }

        public bool TrySampleMouse(out Vector2 mousePosition)
        {
            return Poll(out mousePosition, allowInsideViewportNoMotion: true).HasMotion;
        }

        public void End()
        {
            activeSceneView = null;
            physicalMousePosition = Vector2.zero;
            stableMousePosition = Vector2.zero;
            previousScreenCursorPosition = Vector2.zero;
            cachedGuiBounds = default;
            cachedScreenBounds = default;
            cachedPixelsPerPoint = 1f;
            hasCachedBounds = false;
            hasPreviousScreenCursorPosition = false;
        }

        private static bool ShouldEvaluateWarp(EventType eventType)
        {
            switch (eventType)
            {
                case EventType.MouseMove:
                case EventType.MouseDrag:
                case EventType.MouseLeaveWindow:
                case EventType.Repaint:
                    return true;
                default:
                    return false;
            }
        }

        private ControlCursorWrapResult Poll(out Vector2 mousePosition, bool allowInsideViewportNoMotion)
        {
            mousePosition = stableMousePosition;

            if (activeSceneView == null)
            {
                return default;
            }

            if (!hasPreviousScreenCursorPosition)
            {
                hasPreviousScreenCursorPosition = TryGetCursorScreenPosition(out previousScreenCursorPosition);
                return new ControlCursorWrapResult(stableMousePosition, false);
            }

            if (!TryGetCursorScreenPosition(out Vector2 currentScreenCursorPosition))
            {
                return new ControlCursorWrapResult(stableMousePosition, false);
            }

            float pixelsPerPoint = Mathf.Max(cachedPixelsPerPoint, 0.0001f);
            Vector2 guiDelta = (currentScreenCursorPosition - previousScreenCursorPosition) / pixelsPerPoint;
            bool cursorOutsideBounds = hasCachedBounds && !ContainsScreenPoint(cachedScreenBounds, currentScreenCursorPosition);

            if (guiDelta.sqrMagnitude <= 0f && (!allowInsideViewportNoMotion || !cursorOutsideBounds))
            {
                return new ControlCursorWrapResult(stableMousePosition, false);
            }

            stableMousePosition += guiDelta;
            physicalMousePosition += guiDelta;
            previousScreenCursorPosition = currentScreenCursorPosition;

            if (hasCachedBounds &&
                (cursorOutsideBounds || TryWrapMousePosition(physicalMousePosition, cachedGuiBounds, out _)) &&
                TryWrapMousePosition(physicalMousePosition, cachedGuiBounds, out Vector2 wrappedGuiPosition))
            {
                Vector2 wrappedScreenCursorPosition = currentScreenCursorPosition +
                    ((wrappedGuiPosition - physicalMousePosition) * pixelsPerPoint);

                if (TrySetCursorScreenPosition(wrappedScreenCursorPosition))
                {
                    physicalMousePosition = wrappedGuiPosition;
                    previousScreenCursorPosition = wrappedScreenCursorPosition;
                }
            }

            mousePosition = stableMousePosition;
            return new ControlCursorWrapResult(stableMousePosition, guiDelta.sqrMagnitude > 0f);
        }

        private void CacheViewportBounds(SceneView sceneView)
        {
            if (!TryGetSceneViewGuiRect(sceneView, out Rect guiBounds))
            {
                hasCachedBounds = false;
                return;
            }

            cachedGuiBounds = guiBounds;
            cachedPixelsPerPoint = Mathf.Max(EditorGUIUtility.pixelsPerPoint, 0.0001f);
            cachedScreenBounds = GetScreenRect(guiBounds);
            hasCachedBounds = cachedScreenBounds.width > 0f && cachedScreenBounds.height > 0f;
        }

        private static bool TryGetSceneViewGuiRect(SceneView sceneView, out Rect rect)
        {
            rect = default;
            if (sceneView == null)
            {
                return false;
            }

            if (CameraViewportProperty != null)
            {
                object value = CameraViewportProperty.GetValue(sceneView, null);
                if (value is Rect viewport && viewport.width > WrapMargin * 2f && viewport.height > WrapMargin * 2f)
                {
                    rect = viewport;
                    return true;
                }
            }

            if (sceneView.camera != null)
            {
                Rect pixelRect = sceneView.camera.pixelRect;
                if (pixelRect.width > WrapMargin * 2f && pixelRect.height > WrapMargin * 2f)
                {
                    float pixelsPerPoint = Mathf.Max(EditorGUIUtility.pixelsPerPoint, 0.0001f);
                    rect = new Rect(
                        pixelRect.x / pixelsPerPoint,
                        sceneView.position.height - (pixelRect.yMax / pixelsPerPoint),
                        pixelRect.width / pixelsPerPoint,
                        pixelRect.height / pixelsPerPoint);
                    return true;
                }
            }

            if (sceneView.position.width <= WrapMargin * 2f || sceneView.position.height <= WrapMargin * 2f)
            {
                return false;
            }

            rect = new Rect(0f, 0f, sceneView.position.width, sceneView.position.height);
            return true;
        }

        private static bool TryWrapMousePosition(Vector2 mousePosition, Rect bounds, out Vector2 wrappedGuiPosition)
        {
            wrappedGuiPosition = mousePosition;

            float left = bounds.xMin + WrapMargin;
            float right = bounds.xMax - WrapMargin;
            float top = bounds.yMin + WrapMargin;
            float bottom = bounds.yMax - WrapMargin;

            bool wrapped = false;

            if (mousePosition.x >= right)
            {
                wrappedGuiPosition.x = left;
                wrapped = true;
            }
            else if (mousePosition.x <= left)
            {
                wrappedGuiPosition.x = right;
                wrapped = true;
            }

            if (mousePosition.y >= bottom)
            {
                wrappedGuiPosition.y = top;
                wrapped = true;
            }
            else if (mousePosition.y <= top)
            {
                wrappedGuiPosition.y = bottom;
                wrapped = true;
            }

            return wrapped;
        }

        private static Rect GetScreenRect(Rect guiRect)
        {
            Vector2 topLeft = GUIUtility.GUIToScreenPoint(new Vector2(guiRect.xMin, guiRect.yMin));
            Vector2 bottomRight = GUIUtility.GUIToScreenPoint(new Vector2(guiRect.xMax, guiRect.yMax));
            float xMin = Mathf.Min(topLeft.x, bottomRight.x);
            float xMax = Mathf.Max(topLeft.x, bottomRight.x);
            float yMin = Mathf.Min(topLeft.y, bottomRight.y);
            float yMax = Mathf.Max(topLeft.y, bottomRight.y);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static bool ContainsScreenPoint(Rect rect, Vector2 point)
        {
            return point.x >= rect.xMin &&
                point.x <= rect.xMax &&
                point.y >= rect.yMin &&
                point.y <= rect.yMax;
        }

        private static bool TryGetCursorScreenPosition(out Vector2 screenPoint)
        {
#if UNITY_EDITOR_WIN
            if (GetCursorPos(out POINT point))
            {
                screenPoint = new Vector2(point.X, point.Y);
                return true;
            }
#endif

            screenPoint = Vector2.zero;
            return false;
        }

        private static bool TrySetCursorScreenPosition(Vector2 screenPoint)
        {
#if UNITY_EDITOR_WIN
            return SetCursorPos(Mathf.RoundToInt(screenPoint.x), Mathf.RoundToInt(screenPoint.y));
#else
            _ = screenPoint;
            return false;
#endif
        }

#if UNITY_EDITOR_WIN
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT point);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);
#endif
    }
}
#endif


