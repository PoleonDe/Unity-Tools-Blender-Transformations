#if UNITY_EDITOR
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace Control.Tools.BlenderTransformations
{
    internal enum ControlTransformMode
    {
        Move,
        Rotate,
        Scale
    }

    internal enum ControlConstraintKind
    {
        None,
        Axis,
        Plane
    }

    internal readonly struct ControlAxisGuide
    {
        public ControlAxisGuide(ControlUserAxis userAxis, Vector3 worldAxis)
        {
            UserAxis = userAxis;
            WorldAxis = worldAxis;
        }

        public ControlUserAxis UserAxis { get; }
        public Vector3 WorldAxis { get; }
    }

    internal sealed class ControlTransformSession
    {
        private const int AxisCycleStateNone = 0;
        private const int AxisCycleStateConstrained = 1;
        private const int AxisCycleStateToggled = 2;
        private readonly struct TransformSnapshot
        {
            public TransformSnapshot(Transform transform)
            {
                Transform = transform;
                Position = transform.position;
                Rotation = transform.rotation;
                LocalPosition = transform.localPosition;
                LocalRotation = transform.localRotation;
                LocalScale = transform.localScale;
            }

            public readonly Transform Transform;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly Vector3 LocalPosition;
            public readonly Quaternion LocalRotation;
            public readonly Vector3 LocalScale;
        }

        private const float PrecisionFactor = 0.1f;

        private readonly TransformSnapshot[] originalSnapshots;
        private readonly Quaternion localReferenceRotation;
        private readonly PivotRotation originalPivotRotation;
        private readonly bool in2DMode;

        private TransformSnapshot[] interactionSnapshots;
        private ControlConstraintKind constraintKind;
        private ControlConstraintKind cycledConstraintKind;
        private ControlUserAxis constrainedAxis;
        private ControlUserAxis activeAxis;
        private PivotRotation currentPivotRotation;
        private Vector2 mouseStartPosition;
        private Vector2 previousRawMousePosition;
        private Vector2 logicalMousePosition;
        private Vector2 virtualMousePosition;
        private Vector2 previousRotationMousePosition;
        private float accumulatedRotationAngle;
        private bool rotationPrecisionActive;
        private int axisCycleState;
        private string numericInput = string.Empty;

        public ControlTransformSession(
            ControlTransformMode mode,
            Transform[] selection,
            Transform activeTransform,
            SceneView sceneView,
            Vector2 mousePosition)
        {
            Mode = mode;
            ActiveTransform = activeTransform != null ? activeTransform : selection[0];
            Pivot = CalculatePivot(selection, ActiveTransform);
            mouseStartPosition = mousePosition;
            previousRawMousePosition = mousePosition;
            logicalMousePosition = mousePosition;
            virtualMousePosition = mousePosition;
            previousRotationMousePosition = mousePosition;
            originalPivotRotation = UnityEditor.Tools.pivotRotation;
            currentPivotRotation = originalPivotRotation;
            in2DMode = sceneView.in2DMode;
            AxisMode = ControlTransformSettings.AxisMode;

            localReferenceRotation = ActiveTransform != null
                ? ActiveTransform.rotation
                : Quaternion.identity;

            originalSnapshots = new TransformSnapshot[selection.Length];
            for (int i = 0; i < selection.Length; i++)
            {
                originalSnapshots[i] = new TransformSnapshot(selection[i]);
            }

            interactionSnapshots = CloneSnapshots(originalSnapshots);
            BeginUndo(mode);
        }

        public ControlTransformMode Mode { get; }
        public Transform ActiveTransform { get; }
        public Vector3 Pivot { get; }
        public ControlTransformAxisMode AxisMode { get; }
        public bool Is2DMode => in2DMode;
        public ControlConstraintKind ConstraintKind => constraintKind;
        public ControlUserAxis ConstrainedAxis => constrainedAxis;
        public bool NumericInputActive => numericInput.Length > 0;
        public string NumericDisplayText => NumericInputActive ? BuildNumericDisplayText() : string.Empty;

        public void HandleAxisKey(
            ControlUserAxis userAxis,
            bool planar,
            SceneView sceneView,
            Vector2 mousePosition,
            bool precision)
        {
            if (in2DMode)
            {
                if (Mode == ControlTransformMode.Rotate)
                {
                    constraintKind = ControlConstraintKind.Axis;
                    constrainedAxis = ControlUserAxis.Z;
                    RefreshAfterStateChange(sceneView, mousePosition, precision);
                    return;
                }

                ControlUnityAxis unityAxis = MapUserAxis(userAxis);
                if (unityAxis == ControlUnityAxis.Z)
                {
                    return;
                }

                // 2D workflows treat Shift+axis as another way to stay in the XY tool plane.
                if (planar)
                {
                    SetPlanarConstraint2D();
                }
                else
                {
                    AdvanceConstraintCycle(userAxis, ControlConstraintKind.Axis);
                }

                RefreshAfterStateChange(sceneView, mousePosition, precision);
                return;
            }

            if (planar && Mode != ControlTransformMode.Rotate)
            {
                AdvanceConstraintCycle(userAxis, ControlConstraintKind.Plane);
            }
            else
            {
                AdvanceConstraintCycle(userAxis, ControlConstraintKind.Axis);
            }

            RefreshAfterStateChange(sceneView, mousePosition, precision);
        }

        public void ClearConstraint(SceneView sceneView, Vector2 mousePosition, bool precision)
        {
            constraintKind = ControlConstraintKind.None;
            ResetAxisCycle();
            SetCurrentPivotRotation(originalPivotRotation);
            RefreshAfterStateChange(sceneView, mousePosition, precision);
        }

        public void Update(SceneView sceneView, Vector2 mousePosition, bool precision)
        {
            if (NumericInputActive)
            {
                ApplyNumeric(sceneView);
                return;
            }

            switch (Mode)
            {
                case ControlTransformMode.Move:
                    UpdateMousePositions(mousePosition, precision);
                    ApplyMove(sceneView, virtualMousePosition);
                    break;
                case ControlTransformMode.Rotate:
                    UpdateMousePositions(mousePosition, false);
                    ApplyRotate(sceneView, logicalMousePosition, precision);
                    break;
                case ControlTransformMode.Scale:
                    UpdateMousePositions(mousePosition, precision);
                    ApplyScale(virtualMousePosition);
                    break;
            }
        }

        public bool HandleNumericKey(Event current, SceneView sceneView, Vector2 mousePosition, bool precision)
        {
            if (current == null || current.type != EventType.KeyDown)
            {
                return false;
            }

            if (!IsNumericEditingKey(current))
            {
                return false;
            }

            // Numeric-key shortcuts stay reserved for the full active transform session.
            if (current.alt || current.control || current.command)
            {
                return true;
            }

            if (current.keyCode == KeyCode.Backspace)
            {
                if (numericInput.Length > 0)
                {
                    numericInput = numericInput.Substring(0, numericInput.Length - 1);
                    if (numericInput.Length == 0)
                    {
                        CaptureCurrentInteractionBaseline(mousePosition, precision);
                    }
                    else
                    {
                        ApplyNumeric(sceneView);
                    }
                }

                return true;
            }

            if (!TryGetNumericCharacter(current, out char character))
            {
                return false;
            }

            if (character == '-')
            {
                numericInput = ToggleNumericSign(numericInput);
                if (numericInput.Length == 0)
                {
                    CaptureCurrentInteractionBaseline(mousePosition, precision);
                }
                else
                {
                    ApplyNumeric(sceneView);
                }

                return true;
            }

            if (!CanAppendNumericCharacter(character))
            {
                return true;
            }

            numericInput += character;
            ApplyNumeric(sceneView);
            return true;
        }

        public void SynchronizeWrappedMousePosition(Vector2 mousePosition)
        {
            previousRawMousePosition = mousePosition;
        }

        private void UpdateMousePositions(Vector2 mousePosition, bool precision)
        {
            Vector2 rawDelta = mousePosition - previousRawMousePosition;
            logicalMousePosition += rawDelta;
            virtualMousePosition += rawDelta * (precision ? PrecisionFactor : 1f);
            previousRawMousePosition = mousePosition;
        }

        public void Cancel()
        {
            foreach (TransformSnapshot snapshot in originalSnapshots)
            {
                if (snapshot.Transform == null)
                {
                    continue;
                }

                snapshot.Transform.localPosition = snapshot.LocalPosition;
                snapshot.Transform.localRotation = snapshot.LocalRotation;
                snapshot.Transform.localScale = snapshot.LocalScale;
            }
        }

        public ControlAxisGuide[] GetActiveGuides(SceneView sceneView)
        {
            if (Mode == ControlTransformMode.Rotate)
            {
                if (in2DMode)
                {
                    return new[] { new ControlAxisGuide(ControlUserAxis.Z, Vector3.forward) };
                }

                if (constraintKind == ControlConstraintKind.Axis)
                {
                    return new[] { new ControlAxisGuide(constrainedAxis, GetAxisVector(constrainedAxis)) };
                }

                return new[] { new ControlAxisGuide(ControlUserAxis.Z, GetViewAxis(sceneView)) };
            }

            if (constraintKind == ControlConstraintKind.Axis)
            {
                return new[] { new ControlAxisGuide(constrainedAxis, GetAxisVector(constrainedAxis)) };
            }

            if (constraintKind == ControlConstraintKind.Plane)
            {
                ControlUserAxis first;
                ControlUserAxis second;
                GetPlaneUserAxes(constrainedAxis, out first, out second);
                return new[]
                {
                    new ControlAxisGuide(first, GetAxisVector(first)),
                    new ControlAxisGuide(second, GetAxisVector(second))
                };
            }

            if (in2DMode)
            {
                return Get2DPlaneGuides();
            }

            return new ControlAxisGuide[0];
        }

        private ControlAxisGuide[] Get2DPlaneGuides()
        {
            ControlAxisGuide[] guides = new ControlAxisGuide[2];
            int count = 0;

            Add2DGuide(ControlUserAxis.X, guides, ref count);
            Add2DGuide(ControlUserAxis.Y, guides, ref count);
            Add2DGuide(ControlUserAxis.Z, guides, ref count);

            if (count == guides.Length)
            {
                return guides;
            }

            ControlAxisGuide[] trimmed = new ControlAxisGuide[count];
            for (int i = 0; i < count; i++)
            {
                trimmed[i] = guides[i];
            }

            return trimmed;
        }

        private void Add2DGuide(ControlUserAxis userAxis, ControlAxisGuide[] guides, ref int count)
        {
            ControlUnityAxis unityAxis = MapUserAxis(userAxis);
            if (unityAxis == ControlUnityAxis.Z || count >= guides.Length)
            {
                return;
            }

            guides[count] = new ControlAxisGuide(userAxis, GetUnityAxisVector(unityAxis));
            count++;
        }

        private void ApplyMove(SceneView sceneView, Vector2 mousePosition)
        {
            Vector3 delta = CalculateMoveDelta(sceneView, mousePosition);
            foreach (TransformSnapshot snapshot in interactionSnapshots)
            {
                if (snapshot.Transform != null)
                {
                    snapshot.Transform.position = snapshot.Position + delta;
                }
            }
        }

        private Vector3 CalculateMoveDelta(SceneView sceneView, Vector2 mousePosition)
        {
            if (constraintKind == ControlConstraintKind.Axis)
            {
                return CalculateAxisMoveDelta(sceneView, GetAxisVector(constrainedAxis), mousePosition);
            }

            if (constraintKind == ControlConstraintKind.Plane)
            {
                Vector3 normal = GetPlaneNormal(constrainedAxis);
                Plane plane = new Plane(normal, Pivot);
                if (!TryGetMousePointOnPlane(mouseStartPosition, plane, out Vector3 start) ||
                    !TryGetMousePointOnPlane(mousePosition, plane, out Vector3 current))
                {
                    return Vector3.zero;
                }

                return Vector3.ProjectOnPlane(current - start, normal);
            }

            Vector3 freeNormal = in2DMode ? Vector3.forward : GetViewAxis(sceneView);
            Plane freePlane = new Plane(freeNormal, Pivot);
            if (!TryGetMousePointOnPlane(mouseStartPosition, freePlane, out Vector3 freeStart) ||
                !TryGetMousePointOnPlane(mousePosition, freePlane, out Vector3 freeCurrent))
            {
                return Vector3.zero;
            }

            return in2DMode
                ? Vector3.ProjectOnPlane(freeCurrent - freeStart, Vector3.forward)
                : freeCurrent - freeStart;
        }

        private void ApplyRotate(SceneView sceneView, Vector2 mousePosition, bool precision)
        {
            Vector3 axis = in2DMode ? Vector3.forward :
                constraintKind == ControlConstraintKind.Axis ? GetAxisVector(constrainedAxis) : GetViewAxis(sceneView);

            if (rotationPrecisionActive != precision)
            {
                // Shift precision is segment-based: changing sensitivity starts a new segment
                // from the already-applied angle so pressing/releasing Shift cannot snap.
                rotationPrecisionActive = precision;
                previousRotationMousePosition = mousePosition;
            }
            else
            {
                float precisionFactor = precision ? PrecisionFactor : 1f;
                accumulatedRotationAngle += CalculateMouseAngle(previousRotationMousePosition, mousePosition) *
                    GetRotationViewSign(sceneView, axis) *
                    precisionFactor;
                previousRotationMousePosition = mousePosition;
            }

            Quaternion deltaRotation = Quaternion.AngleAxis(accumulatedRotationAngle, axis.normalized);

            foreach (TransformSnapshot snapshot in interactionSnapshots)
            {
                if (snapshot.Transform == null)
                {
                    continue;
                }

                snapshot.Transform.position = Pivot + deltaRotation * (snapshot.Position - Pivot);
                snapshot.Transform.rotation = deltaRotation * snapshot.Rotation;
            }
        }

        private void ApplyScale(Vector2 mousePosition)
        {
            float factor = CalculateScaleFactor(mousePosition);
            Vector3 scaleFactors = GetScaleFactors(factor);
            Vector3 basisX = GetUnityAxisVector(ControlUnityAxis.X);
            Vector3 basisY = GetUnityAxisVector(ControlUnityAxis.Y);
            Vector3 basisZ = GetUnityAxisVector(ControlUnityAxis.Z);

            foreach (TransformSnapshot snapshot in interactionSnapshots)
            {
                if (snapshot.Transform == null)
                {
                    continue;
                }

                snapshot.Transform.position = ScalePosition(snapshot.Position, basisX, basisY, basisZ, scaleFactors);
                snapshot.Transform.localScale = new Vector3(
                    snapshot.LocalScale.x * scaleFactors.x,
                    snapshot.LocalScale.y * scaleFactors.y,
                    snapshot.LocalScale.z * scaleFactors.z);
            }
        }

        private void ApplyNumeric(SceneView sceneView)
        {
            if (!TryGetNumericValue(out float value))
            {
                return;
            }

            switch (Mode)
            {
                case ControlTransformMode.Move:
                    ApplyNumericMove(sceneView, value);
                    break;
                case ControlTransformMode.Rotate:
                    ApplyNumericRotate(sceneView, value);
                    break;
                case ControlTransformMode.Scale:
                    ApplyNumericScale(value);
                    break;
            }
        }

        private void ApplyNumericMove(SceneView sceneView, float value)
        {
            Vector3 delta = GetNumericMoveDirection(sceneView) * value;
            foreach (TransformSnapshot snapshot in originalSnapshots)
            {
                if (snapshot.Transform != null)
                {
                    snapshot.Transform.position = snapshot.Position + delta;
                }
            }
        }

        private void ApplyNumericRotate(SceneView sceneView, float degrees)
        {
            Vector3 axis = GetRotationAxis(sceneView);
            Quaternion deltaRotation = Quaternion.AngleAxis(degrees, axis.normalized);

            foreach (TransformSnapshot snapshot in originalSnapshots)
            {
                if (snapshot.Transform == null)
                {
                    continue;
                }

                snapshot.Transform.position = Pivot + deltaRotation * (snapshot.Position - Pivot);
                snapshot.Transform.rotation = deltaRotation * snapshot.Rotation;
            }
        }

        private void ApplyNumericScale(float value)
        {
            float factor = Mathf.Approximately(value, 0f) ? 0.001f : value;
            Vector3 scaleFactors = GetScaleFactors(factor);
            Vector3 basisX = GetUnityAxisVector(ControlUnityAxis.X);
            Vector3 basisY = GetUnityAxisVector(ControlUnityAxis.Y);
            Vector3 basisZ = GetUnityAxisVector(ControlUnityAxis.Z);

            foreach (TransformSnapshot snapshot in originalSnapshots)
            {
                if (snapshot.Transform == null)
                {
                    continue;
                }

                snapshot.Transform.position = ScalePosition(snapshot.Position, basisX, basisY, basisZ, scaleFactors);
                snapshot.Transform.localScale = new Vector3(
                    snapshot.LocalScale.x * scaleFactors.x,
                    snapshot.LocalScale.y * scaleFactors.y,
                    snapshot.LocalScale.z * scaleFactors.z);
            }
        }

        private Vector3 GetNumericMoveDirection(SceneView sceneView)
        {
            if (constraintKind == ControlConstraintKind.Axis)
            {
                return GetAxisVector(constrainedAxis).normalized;
            }

            Vector3 mouseDelta = CalculateMoveDelta(sceneView, virtualMousePosition);
            if (mouseDelta.sqrMagnitude > 0.000001f)
            {
                return mouseDelta.normalized;
            }

            if (constraintKind == ControlConstraintKind.Plane)
            {
                Vector3 normal = GetPlaneNormal(constrainedAxis);
                Vector3 fallback = Vector3.ProjectOnPlane(GetSceneViewRight(sceneView), normal);
                return fallback.sqrMagnitude > 0.000001f ? fallback.normalized : GetPlaneFallbackAxis(constrainedAxis);
            }

            if (in2DMode)
            {
                return Vector3.right;
            }

            return GetSceneViewRight(sceneView);
        }

        private Vector3 GetScaleFactors(float factor)
        {
            if (in2DMode)
            {
                if (constraintKind == ControlConstraintKind.Axis)
                {
                    ControlUnityAxis axis = MapUserAxis(constrainedAxis);
                    return new Vector3(
                        axis == ControlUnityAxis.X ? factor : 1f,
                        axis == ControlUnityAxis.Y ? factor : 1f,
                        1f);
                }

                return new Vector3(factor, factor, 1f);
            }

            if (constraintKind == ControlConstraintKind.Axis)
            {
                ControlUnityAxis axis = MapUserAxis(constrainedAxis);
                return new Vector3(
                    axis == ControlUnityAxis.X ? factor : 1f,
                    axis == ControlUnityAxis.Y ? factor : 1f,
                    axis == ControlUnityAxis.Z ? factor : 1f);
            }

            if (constraintKind == ControlConstraintKind.Plane)
            {
                ControlUnityAxis excluded = MapUserAxis(constrainedAxis);
                return new Vector3(
                    excluded == ControlUnityAxis.X ? 1f : factor,
                    excluded == ControlUnityAxis.Y ? 1f : factor,
                    excluded == ControlUnityAxis.Z ? 1f : factor);
            }

            return new Vector3(factor, factor, factor);
        }

        private Vector3 ScalePosition(
            Vector3 originalPosition,
            Vector3 basisX,
            Vector3 basisY,
            Vector3 basisZ,
            Vector3 scaleFactors)
        {
            Vector3 offset = originalPosition - Pivot;
            float x = Vector3.Dot(offset, basisX);
            float y = Vector3.Dot(offset, basisY);
            float z = Vector3.Dot(offset, basisZ);

            return Pivot +
                basisX * (x * scaleFactors.x) +
                basisY * (y * scaleFactors.y) +
                basisZ * (z * scaleFactors.z);
        }

        private Vector3 CalculateAxisMoveDelta(SceneView sceneView, Vector3 axis, Vector2 mousePosition)
        {
            Vector3 normalizedAxis = axis.normalized;
            Vector2 pivotGui = HandleUtility.WorldToGUIPoint(Pivot);
            Vector2 axisGui = HandleUtility.WorldToGUIPoint(Pivot + normalizedAxis);
            Vector2 screenAxis = axisGui - pivotGui;

            if (screenAxis.sqrMagnitude < 4f)
            {
                return CalculateAxisMoveDeltaOnViewPlane(sceneView, normalizedAxis, mousePosition);
            }

            Vector2 screenDirection = screenAxis.normalized;
            float mouseDistanceOnAxis = Vector2.Dot(mousePosition - mouseStartPosition, screenDirection);
            float worldDistance = mouseDistanceOnAxis / screenAxis.magnitude;
            return normalizedAxis * worldDistance;
        }

        private Vector3 CalculateAxisMoveDeltaOnViewPlane(SceneView sceneView, Vector3 axis, Vector2 mousePosition)
        {
            Plane fallbackPlane = new Plane(GetViewAxis(sceneView), Pivot);
            if (!TryGetMousePointOnPlane(mouseStartPosition, fallbackPlane, out Vector3 start) ||
                !TryGetMousePointOnPlane(mousePosition, fallbackPlane, out Vector3 current))
            {
                return Vector3.zero;
            }

            return Vector3.Project(current - start, axis);
        }

        private float CalculateMouseAngle(Vector2 previousMousePosition, Vector2 mousePosition)
        {
            Vector2 pivotGui = HandleUtility.WorldToGUIPoint(Pivot);
            Vector2 start = previousMousePosition - pivotGui;
            Vector2 current = mousePosition - pivotGui;

            if (start.sqrMagnitude < 16f || current.sqrMagnitude < 16f)
            {
                return 0f;
            }

            return -Vector2.SignedAngle(start, current);
        }

        private static float GetRotationViewSign(SceneView sceneView, Vector3 axis)
        {
            float facing = Vector3.Dot(axis.normalized, GetViewAxis(sceneView));
            return facing < -0.0001f ? -1f : 1f;
        }

        private float CalculateScaleFactor(Vector2 mousePosition)
        {
            Vector2 pivotGui = HandleUtility.WorldToGUIPoint(Pivot);
            Vector2 start = mouseStartPosition - pivotGui;
            Vector2 current = mousePosition - pivotGui;

            float factor;
            if (start.sqrMagnitude < 64f || current.sqrMagnitude < 1f)
            {
                factor = 1f + ((mouseStartPosition.y - mousePosition.y) * 0.01f);
            }
            else
            {
                factor = current.magnitude / Mathf.Max(start.magnitude, 0.001f);
            }

            return Mathf.Max(0.01f, factor);
        }

        private Vector3 GetPlaneNormal(ControlUserAxis excludedAxis)
        {
            ControlUserAxis first;
            ControlUserAxis second;
            GetPlaneUserAxes(excludedAxis, out first, out second);
            Vector3 normal = Vector3.Cross(GetAxisVector(first), GetAxisVector(second));
            return normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.forward;
        }

        private static void GetPlaneUserAxes(ControlUserAxis excluded, out ControlUserAxis first, out ControlUserAxis second)
        {
            switch (excluded)
            {
                case ControlUserAxis.X:
                    first = ControlUserAxis.Y;
                    second = ControlUserAxis.Z;
                    break;
                case ControlUserAxis.Y:
                    first = ControlUserAxis.X;
                    second = ControlUserAxis.Z;
                    break;
                default:
                    first = ControlUserAxis.X;
                    second = ControlUserAxis.Y;
                    break;
            }
        }

        private Vector3 GetAxisVector(ControlUserAxis userAxis)
        {
            return GetUnityAxisVector(MapUserAxis(userAxis));
        }

        private Vector3 GetRotationAxis(SceneView sceneView)
        {
            if (in2DMode)
            {
                return Vector3.forward;
            }

            return constraintKind == ControlConstraintKind.Axis
                ? GetAxisVector(constrainedAxis)
                : GetViewAxis(sceneView);
        }

        private Vector3 GetUnityAxisVector(ControlUnityAxis unityAxis)
        {
            Vector3 axis;
            switch (unityAxis)
            {
                case ControlUnityAxis.X:
                    axis = Vector3.right;
                    break;
                case ControlUnityAxis.Y:
                    axis = Vector3.up;
                    break;
                default:
                    axis = Vector3.forward;
                    break;
            }

            return UsesLocalOrientation ? localReferenceRotation * axis : axis;
        }

        private ControlUnityAxis MapUserAxis(ControlUserAxis userAxis)
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

        private static Vector3 GetAxisDragPlaneNormal(SceneView sceneView, Vector3 axis)
        {
            Vector3 viewAxis = GetViewAxis(sceneView);
            Vector3 normal = Vector3.ProjectOnPlane(viewAxis, axis);

            if (normal.sqrMagnitude < 0.0001f && sceneView.camera != null)
            {
                normal = Vector3.ProjectOnPlane(sceneView.camera.transform.up, axis);
            }

            if (normal.sqrMagnitude < 0.0001f)
            {
                normal = Vector3.Cross(axis, Vector3.right);
                if (normal.sqrMagnitude < 0.0001f)
                {
                    normal = Vector3.Cross(axis, Vector3.up);
                }
            }

            return normal.normalized;
        }

        private static Vector3 GetViewAxis(SceneView sceneView)
        {
            return sceneView.camera != null ? sceneView.camera.transform.forward.normalized : Vector3.forward;
        }

        private static Vector3 GetSceneViewRight(SceneView sceneView)
        {
            return sceneView.camera != null ? sceneView.camera.transform.right.normalized : Vector3.right;
        }

        private Vector3 GetPlaneFallbackAxis(ControlUserAxis excludedAxis)
        {
            ControlUserAxis first;
            ControlUserAxis second;
            GetPlaneUserAxes(excludedAxis, out first, out second);
            Vector3 axis = GetAxisVector(first);
            return axis.sqrMagnitude > 0.000001f ? axis.normalized : GetAxisVector(second).normalized;
        }

        private static bool TryGetMousePointOnPlane(Vector2 mousePosition, Plane plane, out Vector3 point)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            if (plane.Raycast(ray, out float enter))
            {
                point = ray.GetPoint(enter);
                return true;
            }

            point = Vector3.zero;
            return false;
        }

        private static Vector3 CalculatePivot(Transform[] selection, Transform activeTransform)
        {
            if (selection.Length == 1)
            {
                return selection[0].position;
            }

            if (UnityEditor.Tools.pivotMode == PivotMode.Pivot && activeTransform != null)
            {
                return activeTransform.position;
            }

            Vector3 sum = Vector3.zero;
            for (int i = 0; i < selection.Length; i++)
            {
                sum += selection[i].position;
            }

            return sum / selection.Length;
        }

        private void BeginUndo(ControlTransformMode mode)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Control " + mode);
            Undo.RegisterCompleteObjectUndo(ToObjectArray(), "Control " + mode);
        }

        private Object[] ToObjectArray()
        {
            Object[] objects = new Object[originalSnapshots.Length];
            for (int i = 0; i < originalSnapshots.Length; i++)
            {
                objects[i] = originalSnapshots[i].Transform;
            }

            return objects;
        }

        public void RestorePivotRotation()
        {
            SetCurrentPivotRotation(originalPivotRotation);
        }

        private bool TryGetNumericValue(out float value)
        {
            return float.TryParse(numericInput, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryGetNumericCharacter(Event current, out char character)
        {
            character = current.character;
            if (character >= '0' && character <= '9')
            {
                return true;
            }

            if (character == '.' || character == '-')
            {
                return true;
            }

            switch (current.keyCode)
            {
                case KeyCode.Keypad0:
                    character = '0';
                    return true;
                case KeyCode.Keypad1:
                    character = '1';
                    return true;
                case KeyCode.Keypad2:
                    character = '2';
                    return true;
                case KeyCode.Keypad3:
                    character = '3';
                    return true;
                case KeyCode.Keypad4:
                    character = '4';
                    return true;
                case KeyCode.Keypad5:
                    character = '5';
                    return true;
                case KeyCode.Keypad6:
                    character = '6';
                    return true;
                case KeyCode.Keypad7:
                    character = '7';
                    return true;
                case KeyCode.Keypad8:
                    character = '8';
                    return true;
                case KeyCode.Keypad9:
                    character = '9';
                    return true;
                case KeyCode.KeypadPeriod:
                    character = '.';
                    return true;
                case KeyCode.KeypadMinus:
                    character = '-';
                    return true;
                default:
                    break;
            }

            return false;
        }

        private bool CanAppendNumericCharacter(char character)
        {
            if (character == '.')
            {
                return numericInput.IndexOf('.') < 0;
            }

            return true;
        }

        public bool ShouldBlockNumericShortcut(Event current)
        {
            if (current == null)
            {
                return false;
            }

            if (current.type != EventType.KeyDown && current.type != EventType.KeyUp)
            {
                return false;
            }

            if (current.keyCode == KeyCode.Backspace)
            {
                return NumericInputActive;
            }

            return IsNumericShortcutKeyCode(current.keyCode) || TryGetNumericCharacter(current, out _);
        }

        private bool UsesLocalOrientation => currentPivotRotation == PivotRotation.Local;

        private void AdvanceConstraintCycle(ControlUserAxis userAxis, ControlConstraintKind nextConstraintKind)
        {
            constrainedAxis = userAxis;

            if (axisCycleState == AxisCycleStateNone ||
                activeAxis != userAxis ||
                cycledConstraintKind != nextConstraintKind)
            {
                activeAxis = userAxis;
                cycledConstraintKind = nextConstraintKind;
                axisCycleState = AxisCycleStateConstrained;
                constraintKind = nextConstraintKind;
                return;
            }

            if (axisCycleState == AxisCycleStateConstrained)
            {
                axisCycleState = AxisCycleStateToggled;
                constraintKind = nextConstraintKind;
                SetCurrentPivotRotation(TogglePivotRotation(currentPivotRotation));
                return;
            }

            constraintKind = ControlConstraintKind.None;
            ResetAxisCycle();
            SetCurrentPivotRotation(originalPivotRotation);
        }

        private void SetPlanarConstraint2D()
        {
            constraintKind = ControlConstraintKind.None;
            ResetAxisCycle();
        }

        private void ResetAxisCycle()
        {
            activeAxis = default;
            cycledConstraintKind = ControlConstraintKind.None;
            axisCycleState = AxisCycleStateNone;
        }

        private void SetCurrentPivotRotation(PivotRotation pivotRotation)
        {
            currentPivotRotation = pivotRotation;
            if (UnityEditor.Tools.pivotRotation != pivotRotation)
            {
                UnityEditor.Tools.pivotRotation = pivotRotation;
            }
        }

        private void RefreshAfterStateChange(SceneView sceneView, Vector2 mousePosition, bool precision)
        {
            if (NumericInputActive)
            {
                Update(sceneView, mousePosition, precision);
                return;
            }

            CaptureCurrentInteractionBaseline(mousePosition, precision);
            Update(sceneView, mousePosition, precision);
        }

        private void CaptureCurrentInteractionBaseline(Vector2 mousePosition, bool precision)
        {
            interactionSnapshots = CaptureCurrentSnapshots();
            mouseStartPosition = mousePosition;
            previousRawMousePosition = mousePosition;
            logicalMousePosition = mousePosition;
            virtualMousePosition = mousePosition;
            previousRotationMousePosition = mousePosition;
            accumulatedRotationAngle = 0f;
            rotationPrecisionActive = precision;
        }

        private TransformSnapshot[] CaptureCurrentSnapshots()
        {
            TransformSnapshot[] snapshots = new TransformSnapshot[originalSnapshots.Length];
            for (int i = 0; i < originalSnapshots.Length; i++)
            {
                Transform transform = originalSnapshots[i].Transform;
                snapshots[i] = transform != null
                    ? new TransformSnapshot(transform)
                    : originalSnapshots[i];
            }

            return snapshots;
        }

        private static TransformSnapshot[] CloneSnapshots(TransformSnapshot[] snapshots)
        {
            TransformSnapshot[] clone = new TransformSnapshot[snapshots.Length];
            for (int i = 0; i < snapshots.Length; i++)
            {
                clone[i] = snapshots[i];
            }

            return clone;
        }

        private static PivotRotation TogglePivotRotation(PivotRotation pivotRotation)
        {
            return pivotRotation == PivotRotation.Global
                ? PivotRotation.Local
                : PivotRotation.Global;
        }

        private static string ToggleNumericSign(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return "-";
            }

            return input[0] == '-'
                ? input.Substring(1)
                : "-" + input;
        }

        private static bool IsNumericEditingKey(Event current)
        {
            if (current == null)
            {
                return false;
            }

            if (IsNumericShortcutKeyCode(current.keyCode))
            {
                return true;
            }

            return TryGetNumericCharacter(current, out _);
        }

        private static bool IsNumericShortcutKeyCode(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.Backspace:
                case KeyCode.Alpha0:
                case KeyCode.Alpha1:
                case KeyCode.Alpha2:
                case KeyCode.Alpha3:
                case KeyCode.Alpha4:
                case KeyCode.Alpha5:
                case KeyCode.Alpha6:
                case KeyCode.Alpha7:
                case KeyCode.Alpha8:
                case KeyCode.Alpha9:
                case KeyCode.Keypad0:
                case KeyCode.Keypad1:
                case KeyCode.Keypad2:
                case KeyCode.Keypad3:
                case KeyCode.Keypad4:
                case KeyCode.Keypad5:
                case KeyCode.Keypad6:
                case KeyCode.Keypad7:
                case KeyCode.Keypad8:
                case KeyCode.Keypad9:
                case KeyCode.Period:
                case KeyCode.KeypadPeriod:
                case KeyCode.Minus:
                case KeyCode.KeypadMinus:
                    return true;
                default:
                    return false;
            }
        }

        private string BuildNumericDisplayText()
        {
            string prefix;
            switch (Mode)
            {
                case ControlTransformMode.Move:
                    prefix = "Move " + GetConstraintLabel(false);
                    return prefix + ": " + numericInput + " units";
                case ControlTransformMode.Rotate:
                    prefix = "Rotate " + GetRotationLabel();
                    return prefix + ": " + numericInput + "\u00B0";
                default:
                    prefix = "Scale " + GetConstraintLabel(true);
                    return prefix + ": " + numericInput + "x";
            }
        }

        private string GetRotationLabel()
        {
            if (in2DMode)
            {
                return "Z";
            }

            return constraintKind == ControlConstraintKind.Axis
                ? constrainedAxis.ToString()
                : "View";
        }

        private string GetConstraintLabel(bool scale)
        {
            if (constraintKind == ControlConstraintKind.Axis)
            {
                return constrainedAxis.ToString();
            }

            if (constraintKind == ControlConstraintKind.Plane || in2DMode)
            {
                ControlUserAxis first;
                ControlUserAxis second;
                if (in2DMode && constraintKind != ControlConstraintKind.Plane)
                {
                    first = ControlUserAxis.X;
                    second = ControlUserAxis.Y;
                }
                else
                {
                    GetPlaneUserAxes(constrainedAxis, out first, out second);
                }

                return first.ToString() + second + " Plane";
            }

            return scale ? "Uniform" : "Free";
        }
    }
}
#endif


