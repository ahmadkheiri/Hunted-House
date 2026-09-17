using UnityEngine;
using UnityEngine.AI;
[DefaultExecutionOrder(-200)]
public sealed class MansionNavigation : MonoBehaviour
{
    public NavMeshData navigationData;
    NavMeshDataInstance instance;
    void OnEnable(){if(navigationData!=null)instance=NavMesh.AddNavMeshData(navigationData);}
    void OnDisable(){if(instance.valid)instance.Remove();}
}
