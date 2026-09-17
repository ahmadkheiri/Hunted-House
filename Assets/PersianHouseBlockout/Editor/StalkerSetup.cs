using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class StalkerSetup
{
    const string Dir="Assets/PersianHouseBlockout";
    static double next;static int testStage;static Vector3 walkStart;
    static StalkerSetup(){EditorApplication.delayCall+=AutoInstall;EditorApplication.playModeStateChanged+=Changed;}
    static void AutoInstall(){if(File.Exists(Dir+"/STALKER_SETUP.txt"))return;if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=AutoInstall;return;}Install();}
    [MenuItem("Tools/Persian House/Build or Rebuild Stalker and Navigation")]
    public static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var previous=SceneManager.GetActiveScene();string path=Dir+"/PersianHouse_ChildHorror.unity";var scene=SceneManager.GetSceneByPath(path);bool loaded=scene.IsValid()&&scene.isLoaded;
        NavMeshDataInstance instance=default;
        try
        {
            if(!loaded)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);SceneManager.SetActiveScene(scene);
            var roots=scene.GetRootGameObjects();var house=roots.First(o=>o.transform.Find("01 GROUND FLOOR - 0.00m")!=null);
            var player=roots.Select(o=>o.GetComponent<ChildFirstPersonController>()).First(c=>c!=null);
            foreach(var old in roots.Where(o=>o.name=="Mansion stalker"||o.name=="Stalker navigation and patrol"))UnityEngine.Object.DestroyImmediate(old);
            var navRoot=new GameObject("Stalker navigation and patrol");
            var settings=NavMesh.GetSettingsByIndex(0);settings.agentRadius=.32f;settings.agentHeight=2.1f;settings.agentClimb=.42f;settings.agentSlope=48;settings.overrideVoxelSize=true;settings.voxelSize=.065f;settings.minRegionArea=.5f;
            var sources=new List<NavMeshBuildSource>();UnityEngine.AI.NavMeshBuilder.CollectSources(house.transform,~0,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
            var data=UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(settings,sources,new Bounds(Vector3.zero,new Vector3(110,35,130)),Vector3.zero,Quaternion.identity);
            if(data==null)throw new Exception("Navigation build failed");
            string navPath=Dir+"/StalkerNavigation.asset";var saved=AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);if(saved==null){AssetDatabase.CreateAsset(data,navPath);saved=data;}else{EditorUtility.CopySerialized(data,saved);UnityEngine.Object.DestroyImmediate(data);EditorUtility.SetDirty(saved);}
            navRoot.AddComponent<MansionNavigation>().navigationData=saved;instance=NavMesh.AddNavMeshData(saved);
            var points=new List<StalkerPatrolPoint>();
            foreach(var floor in house.GetComponentsInChildren<Collider>().Where(c=>c.name=="Floor"||c.name=="Hall floor"||c.name=="Hashti floor"||c.name=="Cellar floor"))
            {
                var b=floor.bounds;var at=new Vector3(b.center.x,b.max.y+.05f,b.center.z);
                AddPoint(points,navRoot.transform,floor.transform.parent.name+" / "+Mathf.Round(b.max.y*10)/10,at,at+Vector3.forward*3+Vector3.up,StalkerPatrolPoint.Observation.Room);
            }
            foreach(float y in new[]{0f,6.3f})foreach(int side in new[]{-1,1})foreach(float z in new[]{-7f,2f,9.9f})
            {
                Vector3 at=PersianHouseScaleSetup.LayoutPoint(side*9.65f,y+.05f,z);
                AddPoint(points,navRoot.transform,(y>1?"Balcony":"Gallery")+" overlooks courtyard "+side+" "+z,at,new Vector3(0,.5f,at.z),y>1?StalkerPatrolPoint.Observation.Balcony:StalkerPatrolPoint.Observation.Courtyard);
            }
            foreach(float z in new[]{-8f,8f})AddPoint(points,navRoot.transform,"Courtyard crossing "+z,new Vector3(7,.05f,z),new Vector3(0,2,0),StalkerPatrolPoint.Observation.Courtyard);
            foreach(float y in new[]{-5.1f,0f,6.3f})AddPoint(points,navRoot.transform,"West stair observation "+y,PersianHouseScaleSetup.LayoutPoint(-11.425f,y+.05f,10.35f),PersianHouseScaleSetup.LayoutPoint(-12.725f,y-1,7),StalkerPatrolPoint.Observation.Stairwell);
            Vector3 spawn=PersianHouseScaleSetup.LayoutPoint(-9.65f,.05f,9.9f);if(!NavMesh.SamplePosition(spawn,out var spawnHit,2,NavMesh.AllAreas))throw new Exception("No navigation at enemy spawn");spawn=spawnHit.position;
            var navPathCheck=new NavMeshPath();var reachable=points.Where(p=>NavMesh.CalculatePath(spawn,p.transform.position,NavMesh.AllAreas,navPathCheck)&&navPathCheck.status==NavMeshPathStatus.PathComplete).ToArray();
            string coverage=$"{reachable.Length}/{points.Count} observation stops reachable; ground {reachable.Count(p=>Mathf.Abs(p.transform.position.y)<1)}, upper {reachable.Count(p=>p.transform.position.y>5)}, cellar {reachable.Count(p=>p.transform.position.y<-3)}";
            if(reachable.Length<10||!reachable.Any(p=>p.transform.position.y>5)||!reachable.Any(p=>p.transform.position.y<-3))throw new Exception("Navigation coverage incomplete: "+coverage);
            var enemy=new GameObject("Mansion stalker");enemy.transform.position=spawn;
            var agent=enemy.AddComponent<NavMeshAgent>();agent.agentTypeID=settings.agentTypeID;agent.radius=.32f;agent.height=2.1f;agent.baseOffset=0;agent.acceleration=5;agent.angularSpeed=95;agent.obstacleAvoidanceType=ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            var capsule=enemy.AddComponent<CapsuleCollider>();capsule.radius=.32f;capsule.height=2.1f;capsule.center=Vector3.up*1.05f;
            var ai=enemy.AddComponent<MansionStalker>();ai.player=player;ai.lantern=player.GetComponent<LanternController>();ai.patrolPoints=reachable;
            var eyes=new GameObject("Eyes - sight origin");eyes.transform.SetParent(enemy.transform,false);eyes.transform.localPosition=new Vector3(0,1.97f,.17f);ai.eyes=eyes.transform;
            Body(enemy,agent);
            PrefabUtility.SaveAsPrefabAsset(enemy,Dir+"/MansionStalker.prefab");
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            File.WriteAllText(Dir+"/STALKER_SETUP.txt","Navigation built from mansion colliders.\n"+coverage+"\nEnemy height 2.1m; child 1.1m.\nEditable observation stops are under Stalker navigation and patrol.\n");
            EditorApplication.delayCall+=StalkerRefinementSetup.Reapply;
        }
        catch(Exception e){File.WriteAllText(Dir+"/STALKER_ERROR.txt",e.ToString());Debug.LogException(e);}
        finally{if(instance.valid)instance.Remove();if(!loaded&&scene.IsValid())EditorSceneManager.CloseScene(scene,true);if(previous.IsValid())SceneManager.SetActiveScene(previous);}
    }
    static void AddPoint(List<StalkerPatrolPoint> points,Transform parent,string name,Vector3 at,Vector3 look,StalkerPatrolPoint.Observation kind)
    {
        if(!NavMesh.SamplePosition(at,out var hit,1.5f,NavMesh.AllAreas)||Mathf.Abs(hit.position.y-at.y)>.65f)return;
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.position=hit.position;var p=go.AddComponent<StalkerPatrolPoint>();p.lookAt=look;p.observation=kind;p.selectionWeight=kind==StalkerPatrolPoint.Observation.Room?.7f:1.3f;points.Add(p);
    }
    static Material Mat(string name,Color color)
    {
        string path=Dir+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m!=null)return m;
        m=new Material(Shader.Find("HDRP/Lit"));m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.15f);AssetDatabase.CreateAsset(m,path);return m;
    }
    static Transform Shape(Transform p,string name,PrimitiveType type,Vector3 at,Vector3 scale,Material material)
    {
        var o=GameObject.CreatePrimitive(type);o.name=name;o.transform.SetParent(p,false);o.transform.localPosition=at;o.transform.localScale=scale;UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>());o.GetComponent<Renderer>().sharedMaterial=material;return o.transform;
    }
    static Transform Limb(Transform parent,string name,Vector3 pivot,Vector3 center,Vector3 size,Material mat)
    {var p=new GameObject(name).transform;p.SetParent(parent,false);p.localPosition=pivot;Shape(p,"Mesh",PrimitiveType.Capsule,center,size,mat);return p;}
    static void Body(GameObject enemy,NavMeshAgent agent)
    {
        var coat=Mat("Stalker - charcoal coat",new Color(.075f,.065f,.06f));var skin=Mat("Stalker - pale mask",new Color(.48f,.43f,.35f));var black=Mat("Stalker - dark details",new Color(.012f,.009f,.008f));var p=enemy.transform;
        Shape(p,"Heavy coat",PrimitiveType.Capsule,new Vector3(0,1.22f,0),new Vector3(.7f,.53f,.44f),coat);
        var motion=enemy.AddComponent<StalkerBodyMotion>();motion.agent=agent;
        motion.head=Limb(p,"Masked head",new Vector3(0,1.82f,0),new Vector3(0,.13f,0),new Vector3(.32f,.2f,.3f),skin);
        Shape(motion.head,"Dark eye sockets",PrimitiveType.Cube,new Vector3(0,.15f,.145f),new Vector3(.25f,.055f,.03f),black);
        motion.leftArm=Limb(p,"Left arm",new Vector3(-.4f,1.62f,0),new Vector3(0,-.4f,0),new Vector3(.19f,.43f,.22f),coat);
        motion.rightArm=Limb(p,"Right arm",new Vector3(.4f,1.62f,0),new Vector3(0,-.4f,0),new Vector3(.19f,.43f,.22f),coat);
        motion.leftLeg=Limb(p,"Left leg",new Vector3(-.18f,.86f,0),new Vector3(0,-.39f,0),new Vector3(.24f,.4f,.26f),black);
        motion.rightLeg=Limb(p,"Right leg",new Vector3(.18f,.86f,0),new Vector3(0,-.39f,0),new Vector3(.24f,.4f,.26f),black);
    }
    static void Changed(PlayModeStateChange s){if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("StalkerTests",false)){testStage=0;next=EditorApplication.timeSinceStartup+3;EditorApplication.update+=Test;}}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Test()
    {
        if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+4;
        try
        {
            var a=UnityEngine.Object.FindFirstObjectByType<MansionStalker>();Check(a!=null&&a.Agent.isOnNavMesh,"Agent not on navigation");
            if(testStage==0)
            {
                a.Autonomous=false;a.player.InputEnabled=false;
                Check(a.ChaseDistanceForBrightness(0)<a.ChaseDistanceForBrightness(1),"Brightness relation");
                var sensed=new MansionStalker.Perception{playerVisible=true,distance=4,brightness=0,playerPosition=a.transform.position+Vector3.forward*5};
                a.ReturnToRoaming();a.ProcessPerception(sensed,.1f);Check(a.State==MansionStalker.BehaviourState.Alert,"Dark distant sight must alert");
                sensed.brightness=1;a.ProcessPerception(sensed,1.5f);Check(a.State==MansionStalker.BehaviourState.Chase,"Bright sight must chase");
                Vector3 known=a.LastKnownPosition;a.ProcessPerception(new MansionStalker.Perception{playerPosition=Vector3.one*100},2);Check(a.State==MansionStalker.BehaviourState.Alert&&a.LastKnownPosition==known,"Hidden target leaked into memory");
                a.ReturnToRoaming();sensed.distance=1;sensed.brightness=0;a.ProcessPerception(sensed,.1f);a.ProcessPerception(sensed,1.5f);Check(a.State==MansionStalker.BehaviourState.Chase,"Near recognition in darkness");
                a.ReturnToRoaming();a.ProcessPerception(new MansionStalker.Perception{lightVisible=true,lightPosition=known},1);Check(a.State==MansionStalker.BehaviourState.Alert,"Lantern evidence should only alert");
                Vector3 eye=a.eyes.position,target=eye+a.transform.forward*4;var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=(eye+target)*.5f;wall.transform.localScale=Vector3.one;Physics.SyncTransforms();Check(!a.ClearLine(target),"Wall failed to block sight");UnityEngine.Object.DestroyImmediate(wall);
                a.ReturnToRoaming();a.Autonomous=true;a.player.Teleport(new Vector3(70,0,70),Quaternion.identity);walkStart=a.transform.position;
            }
            if(testStage==1){Check(Vector3.Distance(walkStart,a.transform.position)>.1f||a.Agent.hasPath,"Roaming did not choose a route");a.Autonomous=false;a.BeginAlert(a.transform.position);a.searchDuration=1;a.searchRadius=1;a.Autonomous=true;}
            if(testStage==2){Check(a.State==MansionStalker.BehaviourState.Roaming,"Search must expire back to roaming");File.WriteAllText(Dir+"/STALKER_CHECKS.txt","PASS: navigation agent loaded and roams.\nPASS: bright light increases recognition range.\nPASS: distant dark sight alerts; bright sight chases.\nPASS: proximity recognizes player in darkness.\nPASS: visible lantern evidence alerts without automatic chase.\nPASS: occluding wall blocks sight.\nPASS: lost target returns to Alert at last seen location, without hidden-player tracking.\nPASS: search expires to Roaming.\n");Finish();}
            testStage++;
        }
        catch(Exception e){File.WriteAllText(Dir+"/STALKER_ERROR.txt",e.ToString());Debug.LogException(e);Finish();}
    }
    static void Finish(){SessionState.SetBool("StalkerTests",false);EditorApplication.update-=Test;EditorApplication.isPlaying=false;}
}
