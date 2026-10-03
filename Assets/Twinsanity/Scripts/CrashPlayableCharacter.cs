using UnityEngine;

[RequireComponent(typeof(CharacterController), typeof(CrashPlayerController))]
public class CrashPlayableCharacter : MonoBehaviour
{
    public float walkPlaybackSpeed = 1f;
    public float runPlaybackSpeed = 1f;
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
        ApplySettings();
    }

    private void ApplySettings()
    {
        if (animator == null) return;

        animator.walkPlaybackSpeed = walkPlaybackSpeed;
        animator.runPlaybackSpeed = runPlaybackSpeed;
        animator.blendSeconds = animationBlendSeconds;
        animator.previewClip = previewAnimation;
    }
}