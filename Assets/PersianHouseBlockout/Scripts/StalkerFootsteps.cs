using UnityEngine;
using UnityEngine.AI;
[RequireComponent(typeof(AudioSource),typeof(NavMeshAgent))]
public sealed class StalkerFootsteps : MonoBehaviour
{
    public AudioClip[] footstepClips;
    [Min(.2f)] public float distancePerStep=.75f;
    [Range(0,1)] public float volume=.7f;
    public float audibleDistance=25;
    public int StepsPlayed {get;private set;}
    AudioSource source;AudioLowPassFilter lowPass;NavMeshAgent agent;
    Transform listener;Vector3 previous;float travelled,occlusionTimer;int lastClip=-1;
    void Awake()
    {
        source=GetComponent<AudioSource>();agent=GetComponent<NavMeshAgent>();
        source.playOnAwake=false;source.loop=false;source.spatialBlend=1;source.rolloffMode=AudioRolloffMode.Logarithmic;source.minDistance=2;source.maxDistance=audibleDistance;source.dopplerLevel=0;
        lowPass=GetComponent<AudioLowPassFilter>();if(lowPass==null)lowPass=gameObject.AddComponent<AudioLowPassFilter>();
        var player=FindFirstObjectByType<ChildFirstPersonController>();if(player!=null)listener=player.playerCamera.transform;
        previous=transform.position;
    }
    void OnEnable(){previous=transform.position;travelled=0;}
    void Update()
    {
        float moved=Vector3.Distance(previous,transform.position);previous=transform.position;
        if(moved>2){travelled=0;return;}
        if(agent.isOnNavMesh&&!agent.isStopped&&agent.velocity.sqrMagnitude>.02f)
        {
            travelled+=moved;
            if(travelled>=distancePerStep){travelled%=distancePerStep;PlayStep();}
        }
        else travelled=0;
        occlusionTimer-=Time.deltaTime;if(occlusionTimer>0)return;occlusionTimer=.2f;
        bool blocked=false;
        if(listener!=null)
        {
            Vector3 origin=transform.position+Vector3.up*.6f,delta=listener.position-origin;
            foreach(var hit in Physics.RaycastAll(origin,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(transform)&&!listener.IsChildOf(hit.transform)&&hit.transform.GetComponentInParent<ChildFirstPersonController>()==null){blocked=true;break;}
        }
        lowPass.cutoffFrequency=blocked?1100:18000;source.volume=volume*(blocked?.35f:1);source.maxDistance=audibleDistance;
    }
    void PlayStep()
    {
        if(footstepClips==null||footstepClips.Length==0)return;
        int index=Random.Range(0,footstepClips.Length);if(index==lastClip&&footstepClips.Length>1)index=(index+1)%footstepClips.Length;
        lastClip=index;source.pitch=Random.Range(.92f,1.04f);source.PlayOneShot(footstepClips[index]);StepsPlayed++;
    }
}
