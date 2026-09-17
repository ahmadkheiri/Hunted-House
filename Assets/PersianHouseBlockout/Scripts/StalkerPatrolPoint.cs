using UnityEngine;
public sealed class StalkerPatrolPoint : MonoBehaviour
{
    public enum Observation { Room, Courtyard, Balcony, Stairwell, Passage }
    public Observation observation;
    public Vector3 lookAt;
    [Range(.1f,4)] public float selectionWeight=1;
    void OnDrawGizmosSelected(){Gizmos.color=Color.cyan;Gizmos.DrawWireSphere(transform.position,.3f);Gizmos.DrawLine(transform.position+Vector3.up*1.8f,lookAt);}
}
