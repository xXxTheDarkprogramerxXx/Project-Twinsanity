using UnityEngine;

// Keeps each RM2 placement identifiable after it has been moved in the Unity editor.
public class BeachSourceInstance : MonoBehaviour
{
    public string chunk;
    public int instanceId;
    public int objectId;
    public bool hasGameplay;

    private void OnDrawGizmosSelected()
    {
        if (hasGameplay) return;
        Gizmos.color = new Color(1f, 0.7f, 0.1f);
        Gizmos.DrawWireSphere(transform.position, 0.6f);
    }
}
