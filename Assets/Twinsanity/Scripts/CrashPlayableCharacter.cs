using UnityEngine;

[RequireComponent(typeof(CharacterController), typeof(CrashPlayerController))]
public class CrashPlayableCharacter : MonoBehaviour
{
    public float walkPlaybackSpeed = 1f;
    public float runPlaybackSpeed = 1f;
    [Range(0f, 1f)] public float walkPoseWeight = 0.8f;
    [Range(0f, 1f)] public float runPoseWeight = 0.4f;
    public bool calibrateToIdle = true;
    public bool autoGroundRun = true;
    public float runGroundOffset;
    public float animationBlendSeconds = 0.12f;
    [Tooltip("-1 follows movement; 0 idle, 1 walk, 2 run.")]
    public int previewAnimation = -1;

    private CrashPlayerController controller;
    private CrashRigAnimator animator;

    private void Awake()
    {
        controller = GetComponent<CrashPlayerController>();
        animator = GetComponentInChildren<CrashRigAnimator>();
        if (animator == null)
        {
            GameObject model = TwinsanityCharacterRuntime.Create("Crash", transform);
            model.transform.localPosition = new Vector3(0f, 0.48f, 0f);
            animator = model.GetComponent<CrashRigAnimator>();
        }
        controller.model = animator.transform;
        ApplySettings();
    }

    private void Update()
    {
        // Inspector changes can be previewed immediately while the level runs.
        ApplySettings();
    }

    private void ApplySettings()
    {
        if (animator == null) return;
        animator.walkPlaybackSpeed = walkPlaybackSpeed;
        animator.runPlaybackSpeed = runPlaybackSpeed;
        animator.walkPoseWeight = walkPoseWeight;
        animator.runPoseWeight = runPoseWeight;
        animator.calibrateToIdle = calibrateToIdle;
        animator.autoGroundRun = autoGroundRun;
        animator.runGroundOffset = runGroundOffset;
        animator.blendSeconds = animationBlendSeconds;
        animator.previewClip = previewAnimation;
    }
}
