using System.Collections.Generic;
using UnityEngine;
public sealed class StalkerStairPassage : MonoBehaviour
{
    public static readonly List<StalkerStairPassage> Active=new List<StalkerStairPassage>();
    public Bounds traversalBounds;
    public Vector3 lowerExit,upperExit;
    void OnEnable(){if(!Active.Contains(this))Active.Add(this);}
    void OnDisable(){Active.Remove(this);}
    public bool Contains(Vector3 position)=>traversalBounds.Contains(position);
    public static StalkerStairPassage At(Vector3 position)
    {foreach(var stairs in Active)if(stairs!=null&&stairs.Contains(position))return stairs;return null;}
    void OnDrawGizmosSelected(){Gizmos.color=Color.yellow;Gizmos.DrawWireCube(traversalBounds.center,traversalBounds.size);Gizmos.color=Color.green;Gizmos.DrawWireSphere(lowerExit,.4f);Gizmos.DrawWireSphere(upperExit,.4f);}
}
