using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProjectTwinsanity.Character
{
    [RequireComponent(typeof(CharacterController))]
    public class EditableCrashPlayer : MonoBehaviour
    {
        public Animator animator;
        public Transform cameraTransform;
        public AudioSource effects;
        public AudioClip spinSound, jumpSound, doubleJumpSound, landSound;
        public float walkSpeed = 2.5f, runSpeed = 6f, jumpSpeed = 7f, doubleJumpSpeed = 6.5f, gravity = 22f;
        public UnityEvent onSpin = new UnityEvent(), onJump = new UnityEvent(), onDoubleJump = new UnityEvent(), onLand = new UnityEvent();
        public bool IsAttacking { get { return spinTime > 0f || slideTime > 0f; } }
        private CharacterController motor;
        private float vertical, spinTime, slideTime;
        private int jumps;
        private Vector3 spawn;
        private Vector2 controls;
        private bool walking, crouching;

        private void Awake() { motor = GetComponent<CharacterController>(); spawn = transform.position; if (animator == null) animator = GetComponentInChildren<Animator>(); }
        private void Play(AudioClip clip) { if (clip != null && effects != null) effects.PlayOneShot(clip); }
        private void Update()
        {
            if (animator == null) return;
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            bool jump, spin, slide;
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current; Mouse ms = Mouse.current;
            controls = kb == null ? Vector2.zero : new Vector2((kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f), (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f));
            jump = kb != null && kb.spaceKey.wasPressedThisFrame; spin = (kb != null && kb.jKey.wasPressedThisFrame) || (ms != null && ms.leftButton.wasPressedThisFrame);
            slide = kb != null && kb.leftShiftKey.wasPressedThisFrame; walking = kb != null && kb.leftAltKey.isPressed; crouching = kb != null && (kb.cKey.isPressed || kb.leftCtrlKey.isPressed);
#else
            controls = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")); jump = Input.GetKeyDown(KeyCode.Space); spin = Input.GetKeyDown(KeyCode.J) || Input.GetMouseButtonDown(0); slide = Input.GetKeyDown(KeyCode.LeftShift); walking = Input.GetKey(KeyCode.LeftAlt); crouching = Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl);
#endif
            bool groundedBefore = motor.isGrounded;
            if (groundedBefore && vertical <= 0f) { jumps = 0; vertical = -2f; }
            // Walking off a ledge consumes the first jump so only one air jump remains.
            if (!groundedBefore && jumps == 0 && vertical < -2f) jumps = 1;
            if (jump && jumps < 2)
            {
                if (jumps == 0) { vertical = jumpSpeed; animator.SetTrigger("Jump"); onJump.Invoke(); Play(jumpSound); }
                else { vertical = doubleJumpSpeed; animator.SetTrigger("DoubleJump"); onDoubleJump.Invoke(); Play(doubleJumpSound); }
                jumps++;
            }
            if (spin && spinTime <= 0f) { spinTime = .5f; animator.SetTrigger("Spin"); onSpin.Invoke(); Play(spinSound); }
            if (slide && groundedBefore && slideTime <= 0f) { slideTime = .55f; animator.SetTrigger("Slide"); }
            spinTime = Mathf.Max(0f, spinTime - Time.deltaTime); slideTime = Mathf.Max(0f, slideTime - Time.deltaTime);
            Vector3 forward = cameraTransform == null ? Vector3.forward : Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward); Vector3 direction = Vector3.ClampMagnitude(forward * controls.y + right * controls.x, 1f);
            float speed = crouching ? walkSpeed * .45f : walking ? walkSpeed : runSpeed;
            if (slideTime > 0f) { direction = transform.forward; speed = runSpeed * 1.25f; }
            else if (direction.sqrMagnitude > .01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 720f * Time.deltaTime);
            vertical -= gravity * Time.deltaTime;
            CollisionFlags flags = motor.Move((direction * speed + Vector3.up * vertical) * Time.deltaTime);
            bool groundedNow = (flags & CollisionFlags.Below) != 0;
            if ((flags & CollisionFlags.Above) != 0 && vertical > 0f) vertical = 0f;
            if (groundedNow && vertical < 0f) { if (!groundedBefore) { onLand.Invoke(); Play(landSound); } vertical = -2f; jumps = 0; }
            animator.SetFloat("Speed", direction.magnitude * speed, .08f, Time.deltaTime); animator.SetFloat("Vertical", vertical); animator.SetBool("Grounded", groundedNow); animator.SetBool("Crouching", crouching);
            if (transform.position.y < -25f) { motor.enabled = false; transform.position = spawn; motor.enabled = true; vertical = 0f; jumps = 0; }
        }
    }
}
