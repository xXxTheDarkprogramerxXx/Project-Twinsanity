using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class CrashPlayableCharacter : MonoBehaviour
{
    public float walkPlaybackSpeed = 1f;
    public float runPlaybackSpeed = 1f;
    public float animationBlendSeconds = 0.12f;
    [Tooltip("-1 follows movement; 0 idle, 1 walk, 2 run, 3 original spin.")]
    public int previewAnimation = -1;

    private CrashRigAnimator animator;

    private void Awake()
    {
        animator = GetComponentInChildren<CrashRigAnimator>(true);
        if (animator == null)
        {
            GameObject model = TwinsanityCharacterRuntime.Create("Crash", transform);
            model.transform.localPosition = new Vector3(0f, 0.48f, 0f);
            animator = model.GetComponent<CrashRigAnimator>();
        }

        BeachPlayerController beach = GetComponent<BeachPlayerController>();
        CrashPlayerController crash = GetComponent<CrashPlayerController>();
        if (beach != null)
        {
            beach.model = animator.transform;
            if (crash != null) crash.enabled = false;
        }
        else
        {
            if (crash == null) crash = gameObject.AddComponent<CrashPlayerController>();
            crash.model = animator.transform;
        }
        ApplySettings();
    }

    private void Update() { ApplySettings(); }

    private void ApplySettings()
    {
        if (animator == null) return;
        animator.walkPlaybackSpeed = walkPlaybackSpeed;
        animator.runPlaybackSpeed = runPlaybackSpeed;
        animator.blendSeconds = animationBlendSeconds;
        animator.previewClip = -1;
    }
}
