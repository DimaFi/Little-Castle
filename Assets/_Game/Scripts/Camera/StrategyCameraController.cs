using UnityEngine;

namespace LittleCastle.CameraSystem
{
    /// <summary>
    /// Ground-focused strategy camera for Little Castle.
    ///
    /// The camera orbits a ground focus point, so the player can inspect the
    /// settlement closely but can never tilt upward into a free-look camera.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class StrategyCameraController : MonoBehaviour
    {
        [Header("World integration")]
        [SerializeField] private Transform streamingFocus;
        [SerializeField] private LittleCastle.World.WorldStreamer worldStreamer;
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Movement")]
        [SerializeField] private bool enableEdgeScroll = true;
        [SerializeField, Min(0f)] private float edgeScrollPixels = 14f;
        [SerializeField, Min(0.1f)] private float closePanSpeed = 12f;
        [SerializeField, Min(0.1f)] private float farPanSpeed = 58f;
        [SerializeField, Min(0.1f)] private float panSmoothing = 10f;
        [SerializeField, Min(0.1f)] private float middleDragSpeed = 0.75f;

        [Header("Orbit")]
        [SerializeField] private bool allowPitchAdjustment = true;
        [SerializeField] private float minimumPitch = 6f;
        [SerializeField] private float maximumPitch = 72f;
        [SerializeField] private float initialPitch = 42f;
        [SerializeField] private float mouseRotationSensitivity = 3.2f;
        [SerializeField] private float keyboardRotationSpeed = 80f;

        [Header("Zoom")]
        [SerializeField, Min(1f)] private float minimumDistance = 11f;
        [SerializeField, Min(2f)] private float maximumDistance = 190f;
        [SerializeField, Min(0.1f)] private float initialDistance = 115f;
        [SerializeField, Min(0.1f)] private float zoomSensitivity = 2.4f;
        [SerializeField, Min(0.1f)] private float zoomSmoothing = 12f;

        [Header("Ground following")]
        [SerializeField] private bool followGroundHeight = true;
        [SerializeField, Min(10f)] private float groundProbeHeight = 1200f;
        [SerializeField, Min(0f)] private float focusHeightOffset = 0.25f;
        [SerializeField, Min(0.1f)] private float groundHeightSmoothing = 8f;

        [Header("Initialization")]
        [SerializeField] private bool initializeFromCurrentView = true;

        private Vector3 targetFocus;
        private Vector3 smoothedFocus;
        private Vector3 focusVelocity;

        private float targetDistance;
        private float smoothedDistance;
        private float distanceVelocity;

        private float yaw;
        private float pitch;
        private float currentGroundY;

        private void Start()
        {
            InitializeCameraState();
            ApplyImmediate();
        }

        private void Update()
        {
            float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);

            ReadRotationInput(dt);
            ReadZoomInput();
            ReadPanInput(dt);
            UpdateGroundHeight(dt);
            ClampFocusToWorld();
            SmoothState(dt);
            ApplyTransform();
            SyncStreamingFocus();
        }

        private void InitializeCameraState()
        {
            targetDistance = Mathf.Clamp(initialDistance, minimumDistance, maximumDistance);
            pitch = Mathf.Clamp(initialPitch, minimumPitch, maximumPitch);

            Vector3 fallbackFocus = streamingFocus != null
                ? streamingFocus.position
                : Vector3.zero;

            targetFocus = fallbackFocus;

            if (initializeFromCurrentView)
            {
                Vector3 forward = transform.forward;
                Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);

                if (flatForward.sqrMagnitude > 0.0001f)
                    yaw = Mathf.Atan2(flatForward.x, flatForward.z) * Mathf.Rad2Deg;

                float currentPitch = NormalizePitch(transform.eulerAngles.x);
                pitch = Mathf.Clamp(currentPitch, minimumPitch, maximumPitch);

                Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, fallbackFocus.y, 0f));
                Ray lookRay = new Ray(transform.position, transform.forward);

                if (groundPlane.Raycast(lookRay, out float enter) && enter > 0f)
                {
                    targetFocus = lookRay.GetPoint(enter);
                    targetDistance = Mathf.Clamp(enter, minimumDistance, maximumDistance);
                }
            }

            if (Mathf.Abs(yaw) < 0.001f && !initializeFromCurrentView)
                yaw = 0f;

            currentGroundY = targetFocus.y;
            smoothedFocus = targetFocus;
            smoothedDistance = targetDistance;
        }

        private void ReadRotationInput(float dt)
        {
            if (Input.GetMouseButton(1))
            {
                yaw += Input.GetAxisRaw("Mouse X") * mouseRotationSensitivity;

                if (allowPitchAdjustment)
                {
                    pitch -= Input.GetAxisRaw("Mouse Y") * mouseRotationSensitivity;
                    pitch = Mathf.Clamp(pitch, minimumPitch, maximumPitch);
                }
            }

            float keyboardRotate = 0f;

            if (Input.GetKey(KeyCode.Q))
                keyboardRotate -= 1f;

            if (Input.GetKey(KeyCode.E))
                keyboardRotate += 1f;

            yaw += keyboardRotate * keyboardRotationSpeed * dt;
            yaw = Mathf.Repeat(yaw, 360f);
        }

        private void ReadZoomInput()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");

            if (Mathf.Abs(scroll) < 0.0001f)
                return;

            targetDistance *= Mathf.Exp(-scroll * zoomSensitivity);
            targetDistance = Mathf.Clamp(targetDistance, minimumDistance, maximumDistance);
        }

        private void ReadPanInput(float dt)
        {
            Vector2 input = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical"));

            if (enableEdgeScroll && Application.isFocused)
                input += ReadEdgeInput();

            input = Vector2.ClampMagnitude(input, 1f);

            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;

            float zoom01 = Mathf.InverseLerp(minimumDistance, maximumDistance, targetDistance);
            float panSpeed = Mathf.Lerp(closePanSpeed, farPanSpeed, zoom01);

            targetFocus += (right * input.x + forward * input.y) * panSpeed * dt;

            if (Input.GetMouseButton(2))
            {
                float dragX = Input.GetAxisRaw("Mouse X");
                float dragY = Input.GetAxisRaw("Mouse Y");

                float dragScale = middleDragSpeed * Mathf.Lerp(0.6f, 2.5f, zoom01);
                targetFocus -= right * dragX * dragScale;
                targetFocus -= forward * dragY * dragScale;
            }
        }

        private Vector2 ReadEdgeInput()
        {
            Vector3 mouse = Input.mousePosition;

            if (mouse.x < 0f || mouse.y < 0f ||
                mouse.x > Screen.width || mouse.y > Screen.height)
            {
                return Vector2.zero;
            }

            Vector2 edge = Vector2.zero;

            if (mouse.x <= edgeScrollPixels)
                edge.x -= 1f;
            else if (mouse.x >= Screen.width - edgeScrollPixels)
                edge.x += 1f;

            if (mouse.y <= edgeScrollPixels)
                edge.y -= 1f;
            else if (mouse.y >= Screen.height - edgeScrollPixels)
                edge.y += 1f;

            return edge;
        }

        private void UpdateGroundHeight(float dt)
        {
            if (!followGroundHeight)
                return;

            Vector3 origin = new Vector3(
                targetFocus.x,
                groundProbeHeight,
                targetFocus.z);

            if (!Physics.Raycast(
                    origin,
                    Vector3.down,
                    out RaycastHit hit,
                    groundProbeHeight * 2f,
                    groundMask,
                    QueryTriggerInteraction.Ignore))
            {
                return;
            }

            float desiredY = hit.point.y + focusHeightOffset;
            float lerp = 1f - Mathf.Exp(-groundHeightSmoothing * dt);
            currentGroundY = Mathf.Lerp(currentGroundY, desiredY, lerp);
            targetFocus.y = currentGroundY;
        }

        private void ClampFocusToWorld()
        {
            if (worldStreamer == null)
                return;

            Vector3 clamped = targetFocus;

            if (worldStreamer.TryClampToPlayableBounds(ref clamped, 1f))
            {
                clamped.y = targetFocus.y;
                targetFocus = clamped;
            }
        }

        private void SmoothState(float dt)
        {
            float panSmoothTime = 1f / Mathf.Max(0.1f, panSmoothing);
            float zoomSmoothTime = 1f / Mathf.Max(0.1f, zoomSmoothing);

            smoothedFocus = Vector3.SmoothDamp(
                smoothedFocus,
                targetFocus,
                ref focusVelocity,
                panSmoothTime,
                Mathf.Infinity,
                dt);

            smoothedDistance = Mathf.SmoothDamp(
                smoothedDistance,
                targetDistance,
                ref distanceVelocity,
                zoomSmoothTime,
                Mathf.Infinity,
                dt);
        }

        private void ApplyTransform()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 forward = rotation * Vector3.forward;

            transform.SetPositionAndRotation(
                smoothedFocus - forward * smoothedDistance,
                rotation);
        }

        private void ApplyImmediate()
        {
            smoothedFocus = targetFocus;
            smoothedDistance = targetDistance;
            ApplyTransform();
            SyncStreamingFocus();
        }

        private void SyncStreamingFocus()
        {
            if (streamingFocus == null)
                return;

            streamingFocus.position = smoothedFocus;
        }

        private static float NormalizePitch(float eulerX)
        {
            if (eulerX > 180f)
                eulerX -= 360f;

            return eulerX;
        }

        private void OnValidate()
        {
            edgeScrollPixels = Mathf.Max(0f, edgeScrollPixels);
            closePanSpeed = Mathf.Max(0.1f, closePanSpeed);
            farPanSpeed = Mathf.Max(closePanSpeed, farPanSpeed);

            minimumPitch = Mathf.Clamp(minimumPitch, 3f, 80f);
            maximumPitch = Mathf.Clamp(maximumPitch, minimumPitch, 85f);
            initialPitch = Mathf.Clamp(initialPitch, minimumPitch, maximumPitch);

            minimumDistance = Mathf.Max(1f, minimumDistance);
            maximumDistance = Mathf.Max(minimumDistance + 1f, maximumDistance);
            initialDistance = Mathf.Clamp(initialDistance, minimumDistance, maximumDistance);
        }
    }
}
