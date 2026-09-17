using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public sealed class MansionStalker : MonoBehaviour
{
    public enum BehaviourState { Roaming, Alert, Chase }
    [Header("References")]
    public ChildFirstPersonController player;
    public LanternController lantern;
    public Transform eyes;
    public StalkerPatrolPoint[] patrolPoints;
    [Header("Movement (metres per second)")]
    [Min(.1f)] public float roamingSpeed=1.15f;
    [Min(.1f)] public float alertSpeed=1.7f;
    [Min(.1f)] public float chaseSpeed=2.35f;
    [Header("Detection (metres; walls always block sight)")]
    [Min(0)] public float playerDetectionDistance=20;
    [Min(0)] public float lanternDetectionDistance=28;
    [Range(30,180)] public float fieldOfView=110;
    [Min(.1f)] public float nearRecognitionDistance=2.5f;
    [UnityEngine.Serialization.FormerlySerializedAs("brightChaseDistance")]
    [Min(.1f)] public float brightAlertDistance=18;
    [Min(1)] public float fullBrightnessLumens=200;
    [Min(.01f)] public float fullMoonBrightnessLux=.3f;
    [Tooltip("Minimum estimated illumination of a visible lantern-lit surface to investigate it.")]
    [Min(.001f)] public float lightPatchDetectionLux=.12f;
    public LayerMask sightMask=~0;
    [Header("Alert and losing the player")]
    [Min(1)] public float searchDuration=14;
    [Min(.5f)] public float searchRadius=5;
    [Min(.05f)] public float recognitionSeconds=.45f;
    [Min(.1f)] public float minimumAlertSeconds=1.2f;
    [Min(.1f)] public float loseSightSeconds=1.4f;
    [Min(2)] public float investigationTravelTimeout=30;
    [Header("Natural roaming and scanning")]
    public Vector2 scanSeconds=new Vector2(2.5f,5.5f);
    public Vector2 walkingSecondsBeforePause=new Vector2(9,16);
    [Range(0,1)] public float stopToScanChance=.65f;
    [Range(0,120)] public float scanAngle=65;
    [Min(.1f)] public float turnSpeed=95;
    public bool showDebugState=false;
    public BehaviourState State {get;private set;}=BehaviourState.Roaming;
    public Vector3 LastKnownPosition {get;private set;}
    public float CurrentChaseDistance {get;private set;}
    public float CurrentAlertDistance {get;private set;}
    public bool IsTraversingStairs => stairPassage!=null;
    public bool IsScanning => scanRemaining>0;
    public bool IsWalkingToStairLookout => postStairWalk;
    public bool HasVisualContact {get;private set;}
    public bool ReachedPlayer {get;private set;}
    public bool Autonomous=true;
    public NavMeshAgent Agent => agent;
    NavMeshAgent agent;
    NavMeshPath path;
    readonly Queue<StalkerPatrolPoint> recent=new Queue<StalkerPatrolPoint>();
    StalkerPatrolPoint destination;
    Vector3 scanDirection;
    float recognition,lostSight,alertTravel,searchRemaining,scanRemaining,scanTotal,walkRemaining,repath;
    bool searching,wasScanning;
    float perceptionClock;
    float alertAge;
    StalkerStairPassage stairPassage;
    Vector3 stairExit,searchCenter;
    bool stairAscending,postStairWalk;
    public struct Perception
    {public bool playerVisible,lightVisible;public float distance,lightDistance,brightness;public Vector3 playerPosition,lightPosition;}
    void Awake(){agent=GetComponent<NavMeshAgent>();path=new NavMeshPath();}
    void Start()
    {
        if(player==null)player=FindFirstObjectByType<ChildFirstPersonController>();
        if(lantern==null&&player!=null)lantern=player.GetComponent<LanternController>();
        if(patrolPoints==null||patrolPoints.Length==0||!System.Array.Exists(patrolPoints,p=>p!=null))patrolPoints=FindObjectsByType<StalkerPatrolPoint>(FindObjectsSortMode.None);
        agent.updateRotation=false;agent.stoppingDistance=.35f;walkRemaining=Random.Range(walkingSecondsBeforePause.x,walkingSecondsBeforePause.y);
    }
    void Update()
    {
        if(!Autonomous||player==null||!agent.isOnNavMesh)return;
        UpdateStairCommitment();
        float dt=Time.deltaTime;perceptionClock+=dt;
        if(perceptionClock>=.1f){var elapsed=perceptionClock;perceptionClock=0;ProcessPerception(Sense(),elapsed);}
        agent.speed=State==BehaviourState.Chase?chaseSpeed:State==BehaviourState.Alert?alertSpeed:roamingSpeed;
        repath-=dt;
        // Perception can change state on stairs, but movement must finish the flight first.
        if(stairPassage!=null){scanRemaining=0;agent.isStopped=false;FaceMovement(dt);return;}
        if(State==BehaviourState.Chase)
        {
            if(repath<=0){GoTo(LastKnownPosition);repath=.25f;}
            ReachedPlayer=HasVisualContact&&Vector3.Distance(transform.position,player.transform.position)<.9f;
            agent.isStopped=ReachedPlayer;FaceMovement(dt);return;
        }
        ReachedPlayer=false;
        if(State==BehaviourState.Alert)
        {
            alertTravel+=dt;
            if(!searching&&(Arrived()||(!postStairWalk&&alertTravel>=investigationTravelTimeout)))
            {postStairWalk=false;searching=true;searchRemaining=searchDuration;StartScan(2,LastKnownPosition+Vector3.up);}
            if(searching)
            {
                searchRemaining-=dt;if(searchRemaining<=0){ReturnToRoaming();return;}
                if(Scan(dt))return;
                if(Arrived())
                {
                    if(!wasScanning){StartScan(Random.Range(1.5f,3),LastKnownPosition+Random.insideUnitSphere*2);wasScanning=true;return;}
                    wasScanning=false;SearchNearby();
                }
            }
            FaceMovement(dt);return;
        }
        if(Scan(dt))return;
        walkRemaining-=dt;
        if(Arrived())
        {
            if(postStairWalk){postStairWalk=false;wasScanning=true;StartScan(Random.Range(scanSeconds.x,scanSeconds.y),destination.lookAt);return;}
            if(destination!=null&&!wasScanning&&Random.value<stopToScanChance)
            {wasScanning=true;StartScan(Random.Range(scanSeconds.x,scanSeconds.y),destination.lookAt);return;}
            wasScanning=false;ChoosePatrol();
        }
        else if(walkRemaining<=0&&!postStairWalk)
        {walkRemaining=Random.Range(walkingSecondsBeforePause.x,walkingSecondsBeforePause.y);StartScan(Random.Range(1,2.5f),transform.position+transform.forward*4);}
        FaceMovement(dt);
    }
    public float AlertDistanceForBrightness(float brightness)
    {return Mathf.Lerp(nearRecognitionDistance*2,Mathf.Max(nearRecognitionDistance*2,brightAlertDistance),Mathf.Clamp01(brightness));}
    public float ChaseDistanceForBrightness(float brightness)=>AlertDistanceForBrightness(brightness)*.5f;
    public Perception Sense()
    {
        var result=new Perception();if(player==null)return result;
        Vector3 eye=EyePosition,head=player.playerCamera.transform.position,body=player.transform.position+Vector3.up*(player.Body.height*.5f);
        result.distance=Vector3.Distance(eye,body);result.playerPosition=player.transform.position;
        bool close=result.distance<=nearRecognitionDistance;
        result.playerVisible=result.distance<=Mathf.Max(playerDetectionDistance,nearRecognitionDistance)&&(close||InView(head))&&(ClearLine(head)||ClearLine(body));
        if(lantern!=null&&lantern.isLit&&lantern.lanternLight!=null&&lantern.lanternLight.enabled)
        {
            result.brightness=Mathf.Clamp01(lantern.ActualLumens/Mathf.Max(1,fullBrightnessLumens));
            Vector3 source=lantern.lanternLight.transform.position;
            if(Vector3.Distance(eye,source)<=lanternDetectionDistance&&InView(source)&&ClearLine(source)){result.lightVisible=true;result.lightPosition=player.transform.position;result.lightDistance=Vector3.Distance(eye,source);}
            else
            {
                // Approximate visible spill: a lit wall/floor can attract attention without revealing the child's position.
                for(int i=0;i<8;i++)
                {
                    Vector3 direction=i==0?Vector3.down:Quaternion.Euler(0,i*51.4f,0)*Vector3.forward;
                    if(!Physics.Raycast(source,direction,out var hit,Mathf.Min(8,lantern.renderingRange),sightMask,QueryTriggerInteraction.Ignore)||hit.transform.IsChildOf(player.transform))continue;
                    Vector3 patch=hit.point+hit.normal*.06f;
                    float lux=lantern.ActualLumens/(4*Mathf.PI*Mathf.Max(.1f,hit.distance*hit.distance))*Mathf.Max(0,Vector3.Dot(-direction,hit.normal));
                    if(lux>=lightPatchDetectionLux&&Vector3.Distance(eye,patch)<=lanternDetectionDistance&&InView(patch)&&ClearLine(patch))
                    {result.lightVisible=true;result.lightPosition=patch;result.lightDistance=Vector3.Distance(eye,patch);break;}
                }
            }
        }
        var moon=MoonLightingController.Current;
        if(moon!=null&&moon.moonLight!=null&&moon.moonLight.enabled)
        {
            Vector3 towardMoon=-moon.moonLight.transform.forward;
            if(!Physics.Raycast(head+towardMoon*.25f,towardMoon,100,sightMask,QueryTriggerInteraction.Ignore))
                result.brightness=Mathf.Max(result.brightness,Mathf.Clamp01(moon.intensityLux/Mathf.Max(.01f,fullMoonBrightnessLux)));
        }
        return result;
    }
    Vector3 EyePosition=>eyes!=null?eyes.position:transform.position+Vector3.up*1.95f;
    bool InView(Vector3 point){return Vector3.Angle(transform.forward,point-EyePosition)<=fieldOfView*.5f;}
    public bool ClearLine(Vector3 point)
    {
        Vector3 delta=point-EyePosition;
        // Ignore our own body and the target player's body, but never environment occluders.
        foreach(var h in Physics.RaycastAll(EyePosition,delta.normalized,delta.magnitude,sightMask,QueryTriggerInteraction.Ignore))
            if(!h.transform.IsChildOf(transform)&&(player==null||!h.transform.IsChildOf(player.transform)))return false;
        return true;
    }
    public void ProcessPerception(Perception sensed,float dt)
    {
        CurrentAlertDistance=AlertDistanceForBrightness(sensed.brightness);CurrentChaseDistance=CurrentAlertDistance*.5f;HasVisualContact=sensed.playerVisible;
        if(State==BehaviourState.Alert)alertAge+=dt;
        bool recognized=sensed.playerVisible&&sensed.distance<=CurrentChaseDistance;
        if(State==BehaviourState.Chase)
        {
            if(recognized){lostSight=0;LastKnownPosition=sensed.playerPosition;}
            else{lostSight+=dt;if(lostSight>=loseSightSeconds)BeginAlert(LastKnownPosition);}
            return;
        }
        bool playerEvidence=sensed.playerVisible&&sensed.distance<=CurrentAlertDistance;
        bool lightEvidence=sensed.lightVisible&&sensed.lightDistance<=CurrentAlertDistance;
        if(playerEvidence||lightEvidence)
        {
            Vector3 evidence=playerEvidence?sensed.playerPosition:sensed.lightPosition;
            if(State==BehaviourState.Roaming){BeginAlert(evidence);return;}
            if(Vector3.Distance(evidence,LastKnownPosition)>1)BeginAlert(evidence);
            recognition=recognized?recognition+dt:0;
            if(recognition>=recognitionSeconds&&alertAge>=minimumAlertSeconds){State=BehaviourState.Chase;LastKnownPosition=sensed.playerPosition;lostSight=0;scanRemaining=0;searching=false;repath=0;postStairWalk=false;}
        }
        else recognition=0;
    }
    public void BeginAlert(Vector3 evidence)
    {
        if(State!=BehaviourState.Alert){alertAge=0;recognition=0;}
        State=BehaviourState.Alert;LastKnownPosition=evidence;searching=false;alertTravel=0;scanRemaining=0;wasScanning=false;postStairWalk=false;
        searchCenter=evidence;
        var evidenceStairs=StalkerStairPassage.At(evidence);
        if(evidenceStairs!=null)searchCenter=Mathf.Abs(transform.position.y-evidenceStairs.lowerExit.y)<Mathf.Abs(transform.position.y-evidenceStairs.upperExit.y)?evidenceStairs.lowerExit:evidenceStairs.upperExit;
        if(!GoTo(searchCenter)){if(agent!=null&&agent.isOnNavMesh)agent.ResetPath();searching=true;searchRemaining=searchDuration;StartScan(2,evidence+Vector3.up);}
    }
    public void ReturnToRoaming()
    {State=BehaviourState.Roaming;recognition=lostSight=0;scanRemaining=0;searching=false;destination=null;wasScanning=false;postStairWalk=false;if(agent!=null&&agent.isOnNavMesh&&stairPassage==null){agent.ResetPath();agent.isStopped=false;}}
    bool GoTo(Vector3 point)
    {
        if(stairPassage!=null)return true;
        return SetPathTo(point);
    }
    bool SetPathTo(Vector3 point)
    {
        if(agent==null||!agent.isOnNavMesh||!NavMesh.SamplePosition(point,out var hit,2,agent.areaMask)||Mathf.Abs(hit.position.y-point.y)>1.5f)return false;
        if(!agent.CalculatePath(hit.position,path)||path.status!=NavMeshPathStatus.PathComplete)return false;
        agent.isStopped=false;agent.SetPath(path);return true;
    }
    bool Arrived()=>!agent.pathPending&&(!agent.hasPath||agent.remainingDistance<=agent.stoppingDistance+.15f);
    void ChoosePatrol()
    {
        if(patrolPoints==null||patrolPoints.Length==0)return;
        // Weighted random stops, with recent rooms suppressed rather than an exhaustive room-by-room route.
        for(int attempt=0;attempt<30;attempt++)
        {
            var candidate=patrolPoints[Random.Range(0,patrolPoints.Length)];if(candidate==null||candidate.observation==StalkerPatrolPoint.Observation.Stairwell||StalkerStairPassage.At(candidate.transform.position)!=null||recent.Contains(candidate))continue;
            float distance=Vector3.Distance(transform.position,candidate.transform.position);if(distance<2)continue;
            if(Random.value>Mathf.Clamp01(candidate.selectionWeight/(1+distance*.025f)))continue;
            if(!GoTo(candidate.transform.position))continue;
            destination=candidate;recent.Enqueue(candidate);while(recent.Count>5)recent.Dequeue();walkRemaining=Random.Range(walkingSecondsBeforePause.x,walkingSecondsBeforePause.y);return;
        }
        recent.Clear();StartScan(2,transform.position+transform.forward*3);
    }
    void SearchNearby()
    {
        for(int i=0;i<12;i++){var offset=Random.insideUnitCircle*searchRadius;Vector3 at=searchCenter+new Vector3(offset.x,0,offset.y);if(StalkerStairPassage.At(at)==null&&GoTo(at))return;}
        StartScan(2,LastKnownPosition+Vector3.up);
    }
    void StartScan(float seconds,Vector3 target)
    {if(stairPassage!=null||StalkerStairPassage.At(transform.position)!=null)return;scanRemaining=scanTotal=seconds;scanDirection=target-transform.position;scanDirection.y=0;if(scanDirection.sqrMagnitude<.01f)scanDirection=transform.forward;}
    void UpdateStairCommitment()
    {
        if(stairPassage!=null)
        {
            if(Vector3.Distance(transform.position,stairExit)>.55f)return;
            stairPassage=null;scanRemaining=0;
            if(State!=BehaviourState.Chase){ChooseAfterStairs();if(destination!=null)searchCenter=destination.transform.position;alertTravel=0;searching=false;}
            else {GoTo(LastKnownPosition);repath=0;}
            return;
        }
        float y=transform.position.y;
        StalkerStairPassage stairs=null;
        foreach(var candidate in StalkerStairPassage.Active)
        {
            if(candidate==null||!candidate.Contains(transform.position))continue;
            bool atBottom=Mathf.Abs(y-candidate.lowerExit.y)<.8f,atTop=Mathf.Abs(y-candidate.upperExit.y)<.8f;
            // Adjacent cellar/upper stair volumes overlap at ground level. Follow the intended floor.
            if(atBottom&&agent.destination.y<=candidate.lowerExit.y+.8f)continue;
            if(atTop&&agent.destination.y>=candidate.upperExit.y-.8f)continue;
            stairs=candidate;break;
        }
        if(stairs==null)return;
        stairAscending=y<stairs.lowerExit.y+.8f?true:y>stairs.upperExit.y-.8f?false:agent.destination.y>=y;
        stairExit=stairAscending?stairs.upperExit:stairs.lowerExit;
        if(!SetPathTo(stairExit))return;
        stairPassage=stairs;scanRemaining=0;postStairWalk=false;agent.isStopped=false;
    }
    void ChooseAfterStairs()
    {
        StalkerPatrolPoint chosen=null;float best=float.PositiveInfinity;
        foreach(var point in patrolPoints)
        {
            if(point==null||Mathf.Abs(point.transform.position.y-transform.position.y)>.7f||point.observation==StalkerPatrolPoint.Observation.Stairwell||StalkerStairPassage.At(point.transform.position)!=null)continue;
            bool desired=stairAscending?(point.observation==StalkerPatrolPoint.Observation.Room||point.observation==StalkerPatrolPoint.Observation.Balcony):point.observation==StalkerPatrolPoint.Observation.Courtyard;
            float score=Vector3.Distance(transform.position,point.transform.position)+(desired?0:100)+Random.Range(0,5);
            if(score>=best||!NavMesh.CalculatePath(transform.position,point.transform.position,agent.areaMask,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
            best=score;chosen=point;
        }
        if(chosen!=null&&GoTo(chosen.transform.position)){destination=chosen;postStairWalk=true;wasScanning=false;walkRemaining=999;}
        else ChoosePatrol();
    }
    bool Scan(float dt)
    {
        if(scanRemaining<=0){agent.isStopped=false;return false;}
        agent.isStopped=true;scanRemaining-=dt;
        float sweep=Mathf.Sin((1-scanRemaining/scanTotal)*Mathf.PI*2)*scanAngle*.5f;
        Quaternion target=Quaternion.LookRotation(Quaternion.Euler(0,sweep,0)*scanDirection);
        transform.rotation=Quaternion.RotateTowards(transform.rotation,target,turnSpeed*dt);return true;
    }
    void FaceMovement(float dt)
    {var direction=agent.desiredVelocity;direction.y=0;if(direction.sqrMagnitude>.02f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),turnSpeed*dt);}
    void OnGUI(){if(showDebugState)GUI.Label(new Rect(Screen.width/2-180,90,360,24),$"Stalker: {State} | Chase recognition: {CurrentChaseDistance:0.0} m");}
    void OnDrawGizmosSelected(){Gizmos.color=Color.yellow;Gizmos.DrawWireSphere(transform.position,playerDetectionDistance);Gizmos.color=Color.red;Gizmos.DrawWireSphere(transform.position,nearRecognitionDistance);Gizmos.DrawSphere(LastKnownPosition,.2f);}
}
