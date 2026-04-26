#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace Control.Tools.BlenderTransformations
{
    [InitializeOnLoad]
    internal static class ControlSelectToolToolbarOrder
    {
        private const double ReorderInterval = 0.5d;

        private static double nextReorderTime;

        static ControlSelectToolToolbarOrder()
        {
            EditorApplication.delayCall += ReorderAllSceneViewToolbars;
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            if (EditorApplication.timeSinceStartup < nextReorderTime)
            {
                return;
            }

            nextReorderTime = EditorApplication.timeSinceStartup + ReorderInterval;
            ReorderAllSceneViewToolbars();
        }

        private static void ReorderAllSceneViewToolbars()
        {
            foreach (SceneView sceneView in SceneView.sceneViews)
            {
                if (sceneView != null)
                {
                    MoveSelectBeforeHand(sceneView.rootVisualElement);
                }
            }
        }

        private static void MoveSelectBeforeHand(VisualElement root)
        {
            if (root == null)
            {
                return;
            }

            VisualElement select = FindElement(root, IsSelectToolElement);
            VisualElement hand = FindElement(root, IsHandToolElement);
            if (select == null || hand == null || select == hand)
            {
                return;
            }

            VisualElement ancestor = FindToolbarAncestor(select, hand);
            if (ancestor == null)
            {
                return;
            }

            VisualElement selectBranch = GetDirectChildUnder(ancestor, select);
            VisualElement handBranch = GetDirectChildUnder(ancestor, hand);
            if (selectBranch == null || handBranch == null || selectBranch == handBranch)
            {
                return;
            }

            int selectIndex = ChildIndex(ancestor, selectBranch);
            int handIndex = ChildIndex(ancestor, handBranch);
            if (selectIndex < 0 || handIndex < 0 || selectIndex < handIndex)
            {
                return;
            }

            selectBranch.RemoveFromHierarchy();
            ancestor.Insert(handIndex, selectBranch);
        }

        private static VisualElement FindToolbarAncestor(VisualElement first, VisualElement second)
        {
            for (VisualElement ancestor = first.parent; ancestor != null; ancestor = ancestor.parent)
            {
                if (!ContainsElement(ancestor, second))
                {
                    continue;
                }

                if (LooksLikeToolbarContainer(ancestor))
                {
                    return ancestor;
                }
            }

            return first.parent == second.parent ? first.parent : null;
        }

        private static bool LooksLikeToolbarContainer(VisualElement element)
        {
            string typeName = element.GetType().Name;
            string name = element.name ?? string.Empty;
            if (ContainsIgnoreCase(typeName, "Toolbar") ||
                ContainsIgnoreCase(typeName, "Overlay") ||
                ContainsIgnoreCase(name, "Toolbar") ||
                ContainsIgnoreCase(name, "Overlay"))
            {
                return true;
            }

            foreach (string className in element.GetClasses())
            {
                if (ContainsIgnoreCase(className, "toolbar") || ContainsIgnoreCase(className, "overlay"))
                {
                    return true;
                }
            }

            return false;
        }

        private static VisualElement GetDirectChildUnder(VisualElement ancestor, VisualElement descendant)
        {
            VisualElement current = descendant;
            while (current != null && current.parent != ancestor)
            {
                current = current.parent;
            }

            return current != null && current.parent == ancestor ? current : null;
        }

        private static VisualElement FindElement(VisualElement root, Func<VisualElement, bool> predicate)
        {
            if (predicate(root))
            {
                return root;
            }

            foreach (VisualElement child in root.Children())
            {
                VisualElement found = FindElement(child, predicate);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static bool ContainsElement(VisualElement root, VisualElement target)
        {
            if (root == target)
            {
                return true;
            }

            foreach (VisualElement child in root.Children())
            {
                if (ContainsElement(child, target))
                {
                    return true;
                }
            }

            return false;
        }

        private static int ChildIndex(VisualElement parent, VisualElement child)
        {
            int index = 0;
            foreach (VisualElement current in parent.Children())
            {
                if (current == child)
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        private static bool IsSelectToolElement(VisualElement element)
        {
            return TextEquals(element.tooltip, "Select") ||
                ContainsIgnoreCase(element.name, "ControlSelectTool");
        }

        private static bool IsHandToolElement(VisualElement element)
        {
            return ContainsIgnoreCase(element.tooltip, "Hand") ||
                ContainsIgnoreCase(element.tooltip, "View Tool");
        }

        private static bool TextEquals(string value, string expected)
        {
            return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsIgnoreCase(string value, string expected)
        {
            return !string.IsNullOrEmpty(value) &&
                value.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
#endif


