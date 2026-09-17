using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
[InitializeOnLoad]
public static class StalkerIntegrationChecks
{
    const string Dir="Assets/PersianHouseBlockout";
    static double next,deadline;static int stage;
    static StalkerIntegrationChecks(){EditorApplication.delayCall+=Begin;EditorApplication.playModeStateChanged+=Changed;}
    static void Begin()
    {
        if(File.Exists(Dir+"/STALKER_INTEGRATION_CHECKS.txt")||!File.Exists(Dir+"/STALKER_CHECKS.txt"))return;
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=Begin;return;}
        SessionState.SetBool("StalkerIntegration",true);ChildHorrorSetup.PlayChild();
    }
    static void Changed(PlayModeStateChange s){if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("StalkerIntegration",false)){stage=0;next=EditorApplication.timeSinceStartup+3;EditorApplication.update+=Tick;}}
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;
        try
        {
            var a=UnityEngine.Object.FindFirstObjectByType<MansionStalker>();var p=a.player;
            if(stage==0)
            {
                a.Autonomous=false;a.Agent.isStopped=true;p.InputEnabled=false;p.GetComponent<GlobalLightingToggle>().SetLighting(false);
                Check(NavMesh.SamplePosition(new Vector3(7,0,0),out var at,1,NavMesh.AllAreas),"Court sample");a.Agent.Warp(at.position);a.transform.rotation=Quaternion.Euler(0,180,0);
                p.Teleport(new Vector3(7,.05f,-4),Quaternion.identity);a.lantern.SetLit(false);Physics.SyncTransforms();
                var dark=a.Sense();Check(dark.playerVisible&&dark.brightness==0,"Actual unlit player perception failed");
                a.ReturnToRoaming();a.ProcessPerception(dark,.1f);Check(a.State==MansionStalker.BehaviourState.Alert,"Actual dark player should alert");
                a.lantern.lumens=200;a.lantern.SetLit(true);var bright=a.Sense();Check(bright.playerVisible&&bright.lightVisible&&bright.brightness>.9f,"Actual lantern detection failed");
                a.ProcessPerception(bright,1.5f);Check(a.State==MansionStalker.BehaviourState.Chase,"Actual bright player should chase");
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Temporary sight occluder";wall.transform.position=new Vector3(7,1.5f,-3);wall.transform.localScale=new Vector3(3,3,.4f);Physics.SyncTransforms();
                var hidden=a.Sense();Check(!hidden.playerVisible,"Actual wall failed to hide player");Vector3 last=a.LastKnownPosition;p.Teleport(new Vector3(10,.05f,-6),Quaternion.identity);a.ProcessPerception(hidden,2);Check(a.State==MansionStalker.BehaviourState.Alert&&a.LastKnownPosition==last,"Occluded chase leaked current position");UnityEngine.Object.DestroyImmediate(wall);
                p.Teleport(new Vector3(7,.05f,6),Quaternion.identity);Physics.SyncTransforms();Check(!a.Sense().playerVisible,"Rear target outside FOV was seen");
                p.Teleport(new Vector3(70,0,70),Quaternion.identity);a.lantern.lumens=60;a.lantern.SetLit(true);
                a.Agent.speed=12;a.Agent.acceleration=30;a.Agent.isStopped=false;
                var upper=a.patrolPoints.Where(x=>x.transform.position.y>5).OrderBy(x=>Vector3.Distance(x.transform.position,a.transform.position)).First();a.Agent.SetDestination(upper.transform.position);deadline=EditorApplication.timeSinceStartup+40;stage=1;return;
            }
            if(stage==1)
            {
                Check(EditorApplication.timeSinceStartup<deadline,"Actual upper floor traversal timed out");
                if(a.Agent.pathPending||a.Agent.remainingDistance>.6f)return;
                Check(a.transform.position.y>5,"Agent never reached upper floor");
                var cellar=a.patrolPoints.First(x=>x.transform.position.y<-3);a.Agent.SetDestination(cellar.transform.position);deadline=EditorApplication.timeSinceStartup+45;stage=2;return;
            }
            if(stage==2)
            {
                Check(EditorApplication.timeSinceStartup<deadline,"Actual cellar traversal timed out");
                if(a.Agent.pathPending||a.Agent.remainingDistance>.6f)return;
                Check(a.transform.position.y<-3,"Agent never reached cellar");
                a.Agent.isStopped=true;NavMesh.SamplePosition(new Vector3(7,0,0),out var at,1,NavMesh.AllAreas);a.Agent.Warp(at.position);a.transform.rotation=Quaternion.Euler(0,180,0);
                p.Teleport(new Vector3(7,.05f,-3.5f),Quaternion.identity);p.SimulateInput(Vector2.zero,new Vector2(0,12/p.mouseSensitivity),false,false,false,1f/60);p.GetComponent<GlobalLightingToggle>().SetLighting(true);p.InputEnabled=true;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
                stage=3;next=EditorApplication.timeSinceStartup+3;return;
            }
            if(stage==3){ScreenCapture.CaptureScreenshot(Dir+"/Stalker_Preview.png");stage=4;return;}
            File.WriteAllText(Dir+"/STALKER_INTEGRATION_CHECKS.txt","PASS: real player and lantern produce Alert/Chase at the expected brightness.\nPASS: real wall occludes player and preserves last known position.\nPASS: target behind enemy is outside field of view.\nPASS: agent walked from ground to upper floor using the mansion stairs.\nPASS: agent walked from upper floor down to cellar.\nEnemy preview captured from child height.\n");Finish();
        }
        catch(Exception e){File.WriteAllText(Dir+"/STALKER_INTEGRATION_ERROR.txt",e.ToString());Debug.LogException(e);Finish();}
    }
    static void Finish(){SessionState.SetBool("StalkerIntegration",false);EditorApplication.update-=Tick;EditorApplication.isPlaying=false;}
}
