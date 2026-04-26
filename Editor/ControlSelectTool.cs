#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace Control.Tools.BlenderTransformations
{
    [EditorTool("Select", null, toolPriority = int.MinValue)]
    internal sealed class ControlSelectTool : EditorTool
    {
        private const string IconPath = "Packages/com.control-tools.blender-transformations/Editor/Icons/Select.svg";

        private static GUIContent iconContent;
        private static Texture2D iconTexture;
        private static bool iconSkin;
        private int activeControlId;

        public override GUIContent toolbarIcon
        {
            get
            {
                if (iconContent == null || iconTexture == null || iconSkin != EditorGUIUtility.isProSkin)
                {
                    iconTexture = CreateToolbarIcon();
                    iconSkin = EditorGUIUtility.isProSkin;
                    iconContent = new GUIContent(iconTexture, "Select");
                }

                return iconContent;
            }
        }

        public override void OnToolGUI(EditorWindow window)
        {
            if (!(window is SceneView))
            {
                return;
            }

            Event current = Event.current;
            int controlId = GUIUtility.GetControlID("ControlSelectTool".GetHashCode(), FocusType.Passive);

            if (current.type == EventType.Layout)
            {
                if (!current.alt)
                {
                    HandleUtility.AddDefaultControl(controlId);
                }

                return;
            }

            if (current.type == EventType.MouseDown && current.button == 0 && !current.alt)
            {
                SelectAtMouse(current.mousePosition, current);
                activeControlId = controlId;
                GUIUtility.hotControl = controlId;
                current.Use();
                return;
            }

            if (activeControlId != 0 && GUIUtility.hotControl == activeControlId && current.type == EventType.MouseDrag)
            {
                // This tool is intentionally click-select only. Dragging should not move or marquee.
                current.Use();
                return;
            }

            if (activeControlId != 0 && GUIUtility.hotControl == activeControlId && current.type == EventType.MouseUp)
            {
                GUIUtility.hotControl = 0;
                activeControlId = 0;
                current.Use();
            }
        }

        public override void OnWillBeDeactivated()
        {
            if (activeControlId != 0 && GUIUtility.hotControl == activeControlId)
            {
                GUIUtility.hotControl = 0;
            }

            if (activeControlId != 0 && GUIUtility.keyboardControl == activeControlId)
            {
                GUIUtility.keyboardControl = 0;
            }

            activeControlId = 0;
        }

        private static void SelectAtMouse(Vector2 mousePosition, Event current)
        {
            GameObject picked = HandleUtility.PickGameObject(mousePosition, false);
            bool additive = current.shift;
            bool toggle = current.control || current.command;

            if (picked == null)
            {
                if (!additive && !toggle)
                {
                    Selection.objects = Array.Empty<UnityEngine.Object>();
                }

                return;
            }

            if (toggle)
            {
                ToggleSelection(picked);
                return;
            }

            if (additive)
            {
                AddSelection(picked);
                return;
            }

            Selection.objects = new UnityEngine.Object[] { picked };
            Selection.activeObject = picked;
        }

        private static void AddSelection(GameObject picked)
        {
            List<UnityEngine.Object> selection = new List<UnityEngine.Object>(Selection.objects);
            if (!selection.Contains(picked))
            {
                selection.Add(picked);
            }

            MoveToEnd(selection, picked);
            Selection.objects = selection.ToArray();
        }

        private static void ToggleSelection(GameObject picked)
        {
            List<UnityEngine.Object> selection = new List<UnityEngine.Object>(Selection.objects);
            if (selection.Contains(picked))
            {
                selection.Remove(picked);
                Selection.objects = selection.ToArray();
                return;
            }

            selection.Add(picked);
            MoveToEnd(selection, picked);
            Selection.objects = selection.ToArray();
        }

        private static void MoveToEnd(List<UnityEngine.Object> selection, UnityEngine.Object activeObject)
        {
            selection.Remove(activeObject);
            selection.Add(activeObject);
        }

        private static Texture2D CreateToolbarIcon()
        {
            // Unity's SVG importer may expose the source icon as a vector asset instead of a toolbar-ready texture.
            // A small generated texture keeps the tool readable in both light and dark editor themes.
            const int size = 24;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Control Select Icon",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color fill = EditorGUIUtility.isProSkin
                ? new Color(0.86f, 0.86f, 0.86f, 1f)
                : new Color(0.16f, 0.16f, 0.16f, 1f);

            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            texture.SetPixels(pixels);

            Vector2[] outer =
            {
                new Vector2(3f, 3f),
                new Vector2(20f, 10f),
                new Vector2(13f, 13f),
                new Vector2(10f, 21f)
            };

            Vector2[] inner =
            {
                new Vector2(10f, 19f),
                new Vector2(12f, 13f),
                new Vector2(13f, 12f),
                new Vector2(19f, 10f),
                new Vector2(4f, 4f)
            };

            FillPolygon(texture, outer, fill);
            FillPolygon(texture, inner, clear);
            DrawLine(texture, 3, 3, 20, 10, fill);
            DrawLine(texture, 20, 10, 13, 13, fill);
            DrawLine(texture, 13, 13, 10, 21, fill);
            DrawLine(texture, 10, 21, 3, 3, fill);
            texture.Apply(false, true);

            return texture;
        }

        private static void FillPolygon(Texture2D texture, Vector2[] points, Color color)
        {
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    if (ContainsPoint(points, new Vector2(x + 0.5f, y + 0.5f)))
                    {
                        texture.SetPixel(x, texture.height - 1 - y, color);
                    }
                }
            }
        }

        private static bool ContainsPoint(Vector2[] polygon, Vector2 point)
        {
            bool inside = false;
            int j = polygon.Length - 1;
            for (int i = 0; i < polygon.Length; i++)
            {
                bool crosses = polygon[i].y > point.y != polygon[j].y > point.y &&
                    point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) /
                    (polygon[j].y - polygon[i].y) + polygon[i].x;

                if (crosses)
                {
                    inside = !inside;
                }

                j = i;
            }

            return inside;
        }

        private static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color color)
        {
            int dx = Mathf.Abs(x1 - x0);
            int dy = -Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;

            while (true)
            {
                SetPixelGui(texture, x0, y0, color);
                if (x0 == x1 && y0 == y1)
                {
                    break;
                }

                int doubleError = 2 * error;
                if (doubleError >= dy)
                {
                    error += dy;
                    x0 += sx;
                }

                if (doubleError <= dx)
                {
                    error += dx;
                    y0 += sy;
                }
            }
        }

        private static void SetPixelGui(Texture2D texture, int x, int y, Color color)
        {
            if (x < 0 || x >= texture.width || y < 0 || y >= texture.height)
            {
                return;
            }

            texture.SetPixel(x, texture.height - 1 - y, color);
        }
    }
}
#endif



