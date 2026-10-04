using UnityEngine;
using UnityEngine.SceneManagement;

// Original script parameters; focused Unity mover, not a complete AgentLab VM.
[DefaultExecutionOrder(-25)]
[RequireComponent(typeof(HubActor))]
public sealed class HubSeagullFlight : MonoBehaviour
{
    [Tooltip("DEFAULT script condition517: full 3D squared distance <400. Native units.")]
    public float nativeTakeoffDistance = 20f;
    [SerializeField] private float playerDistanceFromHome = -1f;
    [SerializeField] private bool landingGateOpen;
    // New names avoid inheriting the old patch's incorrectly saved speed/height values.
    public float nativeMoveSpeed = 5f, takeoffRise = 8f, takeoffForward = 4f;
    public float chaseTurnSharePerSecond = 1f, landingMoveSpeed = 6.25f;
    public enum FlightState { Grounded, TakingOff, Flying, ApproachingLanding, Landing }
    [SerializeField] private FlightState state;
    private HubActor actor;
    private Transform flightPlayer;
    private Vector3 home, homeUp, direction, orbitTarget, takeoffTarget;
    private float stateTime, flightTime, decisionTime, idleTime, nextIdle, heldTime, takeoffLead;
    private int idleClip = 884, takeoffClip = 895, heldClip = -1;
    private bool decidingLanding;
    private const float FPS = 25f;
    public FlightState State => state;
    private bool CanAnimate => actor != null && actor.enableBehaviour && !actor.showBindPose &&
        actor.previewClip < 0 && !(BeachLevelRuntime.Current != null && BeachLevelRuntime.Current.IsPaused);

    private void Start()
    {
        actor = GetComponent<HubActor>();
        if (actor.definition == null || !actor.definition.resource.EndsWith("_685")) { enabled = false; return; }
        home = transform.position; homeUp = WorldAxis(Vector3.up);
        direction = WorldAxis(Vector3.forward); direction.y = 0f; direction.Normalize();
        state = FlightState.Grounded; nextIdle = Random.Range(.5f, 2.5f);
    }
    private void Update()
    {
        if (!CanAnimate) return;
        float dt = Time.deltaTime * Mathf.Max(0f, actor.playbackSpeed);
        if (dt <= 0f) return;
        stateTime += dt; heldTime += dt;
        Transform player = BeachLevelRuntime.Current == null ? null : BeachLevelRuntime.Current.PlayerTransform;
        playerDistanceFromHome = flightPlayer == null ? -1f : Mathf.Sqrt(NativeSquaredDistance(flightPlayer.position, home));
        landingGateOpen = flightPlayer == null || NativeSquaredDistance(flightPlayer.position, home) > 900f;
        switch (state)
        {
            case FlightState.Grounded:
                if (player != null && NativeSquaredDistance(player.position, transform.position) < nativeTakeoffDistance * nativeTakeoffDistance)
                { flightPlayer = player; BeginTakeoff(true); break; }
                idleTime += dt;
                if (idleTime >= nextIdle)
                {
                    idleTime = 0f; idleClip = Random.value < .5f ? 885 : 886;
                    nextIdle = ClipDuration(idleClip) + Random.Range(.5f, 2.5f); actor.Play(idleClip, true);
                }
                if (idleTime >= ClipDuration(idleClip)) idleClip = 884;
                actor.Play(idleClip); break;
            case FlightState.TakingOff:
                if (stateTime >= takeoffLead)
                    transform.position = Vector3.MoveTowards(transform.position, takeoffTarget, nativeMoveSpeed * dt);
                if (stateTime >= takeoffLead && (transform.position - takeoffTarget).sqrMagnitude < .0001f &&
                    (heldClip < 0 || heldTime >= ClipDuration(takeoffClip)))
                {
                    ChangeState(FlightState.Flying); flightTime = 0f; decidingLanding = false;
                    heldClip = -1; actor.Play(882, true);
                }
                break;
            case FlightState.Flying:
                flightTime += dt; actor.Play(Mathf.Repeat(flightTime, 5f) < 3f ? 882 : 883);
                ChaseStoredPosition(dt);
                // DEFAULT 7: FocusToAgentRef1DistanceSquared > 900, or no AgentRef1.
                if (!decidingLanding && landingGateOpen)
                { decidingLanding = true; decisionTime = 0f; }
                if (decidingLanding)
                {
                    decisionTime -= dt;
                    if (decisionTime <= 0f)
                    {
                        // Conditions pass ABOVE their threshold: Random > .75 = 25% landing chance.
                        if (Random.value > .75f) ChangeState(FlightState.ApproachingLanding);
                        else decisionTime = 5f;
                    }
                }
                break;
            case FlightState.ApproachingLanding:
                actor.Play(882);
                Vector3 aboveHome = home + homeUp;
                Face(aboveHome - transform.position, 90f * dt);
                transform.position = Vector3.MoveTowards(transform.position, aboveHome, nativeMoveSpeed * dt);
                if ((transform.position - aboveHome).sqrMagnitude < .0001f)
                { ChangeState(FlightState.Landing); HoldClip(896); }
                break;
            case FlightState.Landing:
                // LAND: .7s lead, then INITIAL_SPACE without offset at speed 6.25.
                if (stateTime >= .7f)
                    transform.position = Vector3.MoveTowards(transform.position, home, landingMoveSpeed * dt);
                if (heldTime >= ClipDuration(896) && (transform.position - home).sqrMagnitude < .0001f)
                {
                    transform.position = home;
                    if (flightPlayer != null && NativeSquaredDistance(flightPlayer.position, home) < 400f)
                    { BeginTakeoff(false); break; }
                    heldClip = -1; ChangeState(FlightState.Grounded); idleTime = 0f; idleClip = 884;
                    nextIdle = Random.Range(.5f, 2.5f); actor.Play(884, true);
                }
                break;
        }
    }
    private void LateUpdate()
    {
        // HubActor.Play normally loops: clamp these native one-shots while translation finishes.
        if (CanAnimate && heldClip >= 0)
            actor.PreviewNativeFrame(heldClip, Mathf.Min(actor.NativeFrameCount(heldClip) - 1, Mathf.FloorToInt(heldTime * FPS)));
    }
    private void BeginTakeoff(bool nativeClip)
    {
        if (nativeClip)
        {
            takeoffClip = Random.value < .5f ? 895 : 898;
            // DEFAULT focus commands: home + noise spread 5 (1,.5,1) +10Y;
            // copy focus position to stored-position designator 246.
            orbitTarget = home + new Vector3(Random.Range(-5f, 5f), 10f + Random.Range(-2.5f, 2.5f), Random.Range(-5f, 5f));
        }
        direction = WorldAxis(Vector3.forward); direction.y = 0f; direction.Normalize();
        if (direction.sqrMagnitude < .001f) direction = Vector3.forward;
        takeoffTarget = transform.position + WorldAxis(Vector3.up) * takeoffRise + direction * takeoffForward;
        ChangeState(FlightState.TakingOff);
        takeoffLead = nativeClip ? (takeoffClip == 898 ? .62f + .52f : 1.12f) : 0f;
        if (nativeClip)
        {
            HoldClip(takeoffClip);
            if (takeoffClip == 898) heldTime = -.62f;
        }
        else { heldClip = -1; actor.Play(882, true); }
    }
    private void ChaseStoredPosition(float dt)
    {
        Vector3 toward = orbitTarget - transform.position; toward.y = 0f;
        if (toward.sqrMagnitude > .000001f && Vector3.Dot(direction, toward.normalized) <= .99999f)
            // StepGroundChase uses SteerTowards(Duration*elapsed), then facing*speed*elapsed.
            // Retail TurnToward remains unported; Unity Slerp here is NOT verified identical.
            direction = Vector3.Slerp(direction, toward.normalized, Mathf.Clamp01(chaseTurnSharePerSecond * dt)).normalized;
        Face(direction, 360f); transform.position += direction * nativeMoveSpeed * dt;
    }
    // Game conditions use native placement units, before any Unity parent scaling/reflection.
    private float NativeSquaredDistance(Vector3 a, Vector3 b)
    {
        Transform basis = transform.parent;
        return basis == null ? (a - b).sqrMagnitude :
            (basis.InverseTransformPoint(a) - basis.InverseTransformPoint(b)).sqrMagnitude;
    }
    private void HoldClip(int clip) { heldClip = clip; heldTime = 0f; actor.Play(clip, true); }
    private void ChangeState(FlightState next) { state = next; stateTime = 0f; }
    private float ClipDuration(int id) => Mathf.Max(.1f, (actor.NativeFrameCount(id) - 1) / FPS);
    private Vector3 WorldAxis(Vector3 axis)
    {
        Vector3 local = transform.localRotation * axis;
        return (transform.parent == null ? local : transform.parent.TransformVector(local)).normalized;
    }
    private void Face(Vector3 worldDirection, float angle)
    {
        worldDirection.y = 0f; if (worldDirection.sqrMagnitude < .0001f) return;
        Vector3 local = transform.parent == null ? worldDirection : transform.parent.InverseTransformVector(worldDirection);
        transform.localRotation = Quaternion.RotateTowards(transform.localRotation, Quaternion.LookRotation(local.normalized, Vector3.up), angle);
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, nativeTakeoffDistance *
            (transform.parent == null ? 1f : transform.parent.TransformVector(Vector3.right).magnitude));
        if (!Application.isPlaying) return;
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(home, .4f); Gizmos.DrawLine(transform.position, home);
        Gizmos.color = Color.magenta; Gizmos.DrawWireSphere(orbitTarget, .3f);
    }
}

public static class HubSeagullFlightInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    { SceneManager.sceneLoaded -= OnSceneLoaded; SceneManager.sceneLoaded += OnSceneLoaded; }
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (HubActor actor in root.GetComponentsInChildren<HubActor>(true))
            if (actor.definition != null && actor.definition.resource.EndsWith("_685") && actor.GetComponent<HubSeagullFlight>() == null)
                actor.gameObject.AddComponent<HubSeagullFlight>();
    }
}
