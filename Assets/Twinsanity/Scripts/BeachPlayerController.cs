using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class BeachPlayerController : MonoBehaviour
{
    public Transform cameraTransform;
    public Transform model;
    public float initialCameraYaw = 180f;
    public float walkSpeed = 4.8f;
    public float runSpeed = 7.2f;
    public float jumpSpeed = 8.2f;
    public float doubleJumpSpeed = 7.2f;
    public float gravity = 22f;
    public bool attacking { get; private set; }

    private CharacterController controller;
    private Vector3 spawn;
    private Quaternion spawnRotation;
    private float verticalSpeed;
    private float cameraYaw;
    private float cameraPitch = 12f;
    private int cameraStartFrames;
    private float attackTimer;
    private float slideTimer;
    private float stepTime;
    private bool slamming;
    private float invulnerable;
    private int jumpsUsed;
    private float doubleJumpTime = -1f;
    private CrashRigAnimator rig;
    private float doubleJumpDuration = 0.48f;
    public float spinDuration = 0.32f;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        if (model != null) rig = model.GetComponentInChildren<CrashRigAnimator>();
        cameraYaw = initialCameraYaw;
        transform.rotation = Quaternion.Euler(0, 90f, 0);
        spawn = transform.position;
        spawnRotation = transform.rotation;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        PositionCamera(true);
    }

    private void OnDestroy()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        if (BeachLevelRuntime.Current != null && BeachLevelRuntime.Current.IsPaused)
            return;

        invulnerable = Mathf.Max(0f, invulnerable - Time.deltaTime);
        if (rig == null && model != null) rig = model.GetComponentInChildren<CrashRigAnimator>();

        if (cameraStartFrames++ > 2)
        {
            cameraYaw += Input.GetAxis("Mouse X") * 2.4f;
            cameraPitch = Mathf.Clamp(cameraPitch - Input.GetAxis("Mouse Y") * 1.7f, -12f, 50f);
        }

        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.J))
        {
            attackTimer = Mathf.Max(0.05f, spinDuration);
            if (BeachLevelRuntime.Current != null) BeachLevelRuntime.Current.PlayEffect("spin", transform.position);
        }

        if (Input.GetKeyDown(KeyCode.LeftShift) && controller.isGrounded)
            slideTimer = 0.65f;

        if (Input.GetKeyDown(KeyCode.E) && !controller.isGrounded)
        {
            slamming = true;
            verticalSpeed = -17f;
        }

        attackTimer = Mathf.Max(0f, attackTimer - Time.deltaTime);
        slideTimer = Mathf.Max(0f, slideTimer - Time.deltaTime);
        attacking = attackTimer > 0f || slideTimer > 0f || slamming;

        Vector3 forward = Quaternion.Euler(0, cameraYaw, 0) * Vector3.forward;
        Vector3 right = Quaternion.Euler(0, cameraYaw, 0) * Vector3.right;
        Vector3 input = Vector3.ClampMagnitude(forward * Input.GetAxisRaw("Vertical") + right * Input.GetAxisRaw("Horizontal"), 1f);

        bool crouch = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
        float speed = slideTimer > 0f ? runSpeed * 1.45f : crouch ? walkSpeed * 0.48f : Input.GetKey(KeyCode.LeftAlt) ? walkSpeed : runSpeed;

        Vector3 move = input * speed;

        if (slideTimer > 0f && input.sqrMagnitude < 0.1f)
            move = transform.forward * runSpeed * 1.45f;

        bool jumpPressed = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0);
        if (controller.isGrounded && verticalSpeed <= 0f)
        {
            jumpsUsed = 0;
            doubleJumpTime = -1f;
            if (verticalSpeed < 0f)
            {
                verticalSpeed = -1.5f;
                slamming = false;
            }

        }

        if (jumpPressed && jumpsUsed < 2)
        {
            bool secondJump = jumpsUsed == 1;
            verticalSpeed = secondJump ? doubleJumpSpeed : jumpSpeed;
            if (rig != null) rig.NotifyJump(secondJump);
            if (secondJump)
            {
                doubleJumpTime = 0f;
                doubleJumpDuration = rig != null ? rig.GetClipDuration(rig.doubleJumpClip) : 0.48f;
            }
            jumpsUsed++;
            slamming = false;
        }

        if (doubleJumpTime >= 0f)
        {
            doubleJumpTime += Time.deltaTime;
            if (doubleJumpTime >= doubleJumpDuration)
                doubleJumpTime = -1f;
        }

        verticalSpeed -= gravity * Time.deltaTime;
        CollisionFlags collisions = controller.Move((move + Vector3.up * verticalSpeed) * Time.deltaTime);
        if ((collisions & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
        bool animationGrounded = (controller.isGrounded || (collisions & CollisionFlags.Below) != 0) && verticalSpeed <= 0f;
        if (animationGrounded) doubleJumpTime = -1f;

        if (input.sqrMagnitude > 0.02f && slideTimer <= 0f)
        {
            Quaternion target = Quaternion.LookRotation(input, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * 12f);
        }

        if (attacking)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position + Vector3.up * 0.8f, slideTimer > 0 ? 1.25f : 1.4f, ~0, QueryTriggerInteraction.Collide);

            foreach (Collider hit in hits)
            {
                BeachItem item = hit.GetComponentInParent<BeachItem>();
                if (item != null) item.Hit();
            }
        }

        if (transform.position.y < -10f)
            TakeDamage(true);

        if (model != null)
        {
            stepTime += move.magnitude * Time.deltaTime;

            if (rig != null)
                rig.SetMovement(move.magnitude / Mathf.Max(0.01f, runSpeed), animationGrounded, attackTimer > 0f, 1f - attackTimer / Mathf.Max(0.05f, spinDuration), crouch, slideTimer > 0f, verticalSpeed, doubleJumpTime < 0f ? -1f : doubleJumpTime / Mathf.Max(0.01f, doubleJumpDuration));

            model.localPosition = new Vector3(0, 0.48f, 0);
            model.localScale = Vector3.one;
            model.localRotation = Quaternion.identity;
        }
    }

    public void SetCheckpoint(Vector3 position)
    {
        Vector3 rayOrigin = position + Vector3.up * 4f;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
            spawn = hit.point + Vector3.up * 0.12f;
        else
            spawn = position + Vector3.up * 1.2f;

        spawnRotation = transform.rotation;
    }

    public void RespawnAtCheckpoint(bool loseLife)
    {
        if (BeachLevelRuntime.Current != null && loseLife)
            BeachLevelRuntime.Current.lives = Mathf.Max(0, BeachLevelRuntime.Current.lives - 1);

        attackTimer = 0f;
        slideTimer = 0f;
        slamming = false;
        attacking = false;
        verticalSpeed = 0f;
        jumpsUsed = 0;
        doubleJumpTime = -1f;
        invulnerable = 2f;

        controller.enabled = false;
        transform.position = spawn;
        transform.rotation = spawnRotation;
        controller.enabled = true;

        Physics.SyncTransforms();
        if (rig != null) rig.ResetActionState();
        PositionCamera(true);
    }

    public void TakeDamage(bool ignoreProtection = false)
    {
        if (invulnerable > 0f && !ignoreProtection)
            return;

        if (!ignoreProtection && BeachLevelRuntime.Current != null && BeachLevelRuntime.Current.ConsumeMask())
        {
            invulnerable = 1.5f;
            return;
        }

        RespawnAtCheckpoint(true);
    }

    private void LateUpdate()
    {
        if (BeachLevelRuntime.Current != null && BeachLevelRuntime.Current.IsPaused)
            return;

        PositionCamera(false);
    }

    private void PositionCamera(bool immediate)
    {
        if (cameraTransform == null)
            return;

        Vector3 target = transform.position + Vector3.up * 1.25f;
        Quaternion orbit = Quaternion.Euler(cameraPitch, cameraYaw, 0);
        Vector3 desired = target + orbit * new Vector3(0, 1.1f, -5.8f);
        Vector3 direction = desired - target;

        if (Physics.SphereCast(target, 0.22f, direction.normalized, out RaycastHit hit, direction.magnitude, ~0, QueryTriggerInteraction.Ignore))
            desired = target + direction.normalized * Mathf.Max(0.5f, hit.distance - 0.25f);

        cameraTransform.position = immediate ? desired : Vector3.Lerp(cameraTransform.position, desired, Mathf.Clamp01(Time.deltaTime * 10f));
        cameraTransform.LookAt(target);
    }
}
