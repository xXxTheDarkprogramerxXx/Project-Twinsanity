using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
namespace ProjectTwinsanity.Character
{
    public class EditableCrashCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 4.5f, height = 1.05f, yaw = 180f, pitch = 12f, sensitivity = .15f;
        private void LateUpdate()
        {
            if (target == null) return;
            Vector2 delta = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.rightButton.isPressed) delta = Mouse.current.delta.ReadValue();
#else
            if (Input.GetMouseButton(1)) delta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 12f;
#endif
            yaw += delta.x * sensitivity; pitch = Mathf.Clamp(pitch - delta.y * sensitivity, -5f, 65f);
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f); Vector3 focus = target.position + Vector3.up * height; Vector3 back = rotation * Vector3.back;
            float actualDistance = distance;
            if (Physics.SphereCast(focus, .16f, back, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(target)) actualDistance = Mathf.Max(.4f, hit.distance - .1f);
            transform.SetPositionAndRotation(focus + back * actualDistance, rotation);
        }
        private void OnGUI() { GUI.Label(new Rect(15, 15, 700, 55), "WASD: move | Alt: walk | Space: jump / double jump | J / click: spin\nC / Ctrl: crouch | Shift: slide | Hold right mouse: orbit camera"); }
    }
}
