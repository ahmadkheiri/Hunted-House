using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
[InitializeOnLoad]
public static class StalkerRefinementSetup
{
    const string Dir="Assets/PersianHouseBlockout";
    static int stage,stationarySteps;static double next,deadline;static bool checkedStairs;
    static StalkerRefinementSetup(){EditorApplication.delayCall+=Install;EditorApplication.playModeStateChanged+=Changed;}
    public static void Reapply(){File.Delete(Dir+"/STALKER_REFINEMENT_SETUP.txt");Install();}
    static void Install()
    {
        if(File.Exists(Dir+"/STALKER_REFINEMENT_SETUP.txt"))return;
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=Install;return;}
        var previous=SceneManager.GetActiveScene();string path=Dir+"/PersianHouse_ChildHorror.unity";var scene=SceneManager.GetSceneByPath(path);bool loaded=scene.IsValid()&&scene.isLoaded;
        try
        {
            if(!loaded)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);SceneManager.SetActiveScene(scene);
            var a=scene.GetRootGameObjects().Select(o=>o.GetComponent<MansionStalker>()).First(x=>x!=null);
            var nav=scene.GetRootGameObjects().First(o=>o.GetComponent<MansionNavigation>()!=null);
            var house=scene.GetRootGameObjects().First(o=>o.transform.Find("03 STAIRS - 175mm risers")!=null);
            foreach(var old in nav.GetComponentsInChildren<StalkerStairPassage>())UnityEngine.Object.DestroyImmediate(old.gameObject);
            foreach(var group in house.GetComponentsInChildren<Transform>().Where(t=>t.name=="West ground to first"||t.name=="East ground to first"||t.name=="Basement to ground stairs"))
            {
                var geometry=group.GetComponentsInChildren<Collider>().Where(c=>c.name.StartsWith("Flight ")||c.name=="Mid landing").ToArray();
                Bounds bounds=geometry[0].bounds;foreach(var c in geometry)bounds.Encapsulate(c.bounds);bounds.Expand(new Vector3(.12f,.3f,.12f));
                bool cellar=group.name.StartsWith("Basement"),east=group.name.StartsWith("East");float x=east?10.85f:-13.3f,baseY=cellar?-5.1f:0,topY=cellar?0:6.3f;
                var g=new GameObject(group.name+" - continuous traversal");g.transform.SetParent(nav.transform,false);var zone=g.AddComponent<StalkerStairPassage>();zone.traversalBounds=bounds;
                zone.lowerExit=PersianHouseScaleSetup.LayoutPoint(x+.575f,baseY+.05f,10.65f);zone.upperExit=PersianHouseScaleSetup.LayoutPoint(x+1.875f,topY+.05f,10.65f);
            }
            a.patrolPoints=a.patrolPoints.Where(p=>p!=null&&p.observation!=StalkerPatrolPoint.Observation.Stairwell).ToArray();
            var clips=CreateSounds();ConfigureAudio(a.gameObject,clips);
            var prefab=PrefabUtility.LoadPrefabContents(Dir+"/MansionStalker.prefab");try{ConfigureAudio(prefab,clips);PrefabUtility.SaveAsPrefabAsset(prefab,Dir+"/MansionStalker.prefab");}finally{PrefabUtility.UnloadPrefabContents(prefab);}
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            File.WriteAllText(Dir+"/STALKER_REFINEMENT_SETUP.txt","Three stair passages configured. Four original spatial boot-step clips added. Chase radius is half the brightness-dependent alert radius.\n");
            SessionState.SetBool("StalkerRefinementTest",true);EditorApplication.delayCall+=ChildHorrorSetup.PlayChild;
        }
        catch(Exception e){Debug.LogException(e);File.WriteAllText(Dir+"/STALKER_REFINEMENT_ERROR.txt",e.ToString());}
        finally{if(!loaded&&scene.IsValid())EditorSceneManager.CloseScene(scene,true);if(previous.IsValid())SceneManager.SetActiveScene(previous);}
    }
    static void ConfigureAudio(GameObject go,AudioClip[] clips)
    {var f=go.GetComponent<StalkerFootsteps>();if(f==null)f=go.AddComponent<StalkerFootsteps>();f.footstepClips=clips;var source=go.GetComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1;source.minDistance=2;source.maxDistance=25;source.dopplerLevel=0;source.volume=.7f;}
    static AudioClip[] CreateSounds()
    {
        string folder=Dir+"/Audio";Directory.CreateDirectory(folder);var clips=new AudioClip[4];
        for(int k=0;k<4;k++)
        {
            string path=folder+"/Stalker_Boot_"+(k+1)+".wav";
            if(!File.Exists(path))
            {
                const int rate=44100,count=15435;var random=new System.Random(171+k);float filtered=0;
                using(var file=new BinaryWriter(File.Create(path)))
                {
                    file.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));file.Write(36+count*2);file.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));file.Write(16);file.Write((short)1);file.Write((short)1);file.Write(rate);file.Write(rate*2);file.Write((short)2);file.Write((short)16);file.Write(System.Text.Encoding.ASCII.GetBytes("data"));file.Write(count*2);
                    for(int i=0;i<count;i++)
                    {
                        float t=i/(float)rate,noise=(float)random.NextDouble()*2-1;filtered=Mathf.Lerp(filtered,noise,.14f);
                        float onset=Mathf.Clamp01(t/.002f),thump=Mathf.Sin(2*Mathf.PI*(68+k*4)*t)*Mathf.Exp(-t*24)*.48f;
                        float grit=(noise*.12f+filtered*.7f)*Mathf.Exp(-t*38);float sole=filtered*Mathf.Exp(-Mathf.Pow((t-.065f)/.038f,2))*.3f;
                        float value=(thump+grit+sole)*onset*Mathf.Clamp01((.35f-t)/.025f);file.Write((short)(Mathf.Clamp(value,-.95f,.95f)*32767));
                    }
                }
            }
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);clips[k]=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        return clips;
    }
    static void Changed(PlayModeStateChange s){if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("StalkerRefinementTest",false)){stage=0;checkedStairs=false;next=EditorApplication.timeSinceStartup+3;EditorApplication.update+=Test;}}
    static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
    static void Test()
    {
        if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.05;
        try
        {
            var a=UnityEngine.Object.FindFirstObjectByType<MansionStalker>();var foot=a.GetComponent<StalkerFootsteps>();var stairs=StalkerStairPassage.Active.First(s=>s.name.StartsWith("West ground"));
            if(stage==0)
            {
                a.Autonomous=false;a.player.InputEnabled=false;a.player.Teleport(new Vector3(70,0,70),Quaternion.identity);
                foreach(float b in new[]{0f,.3f,1f})Check(Mathf.Abs(a.ChaseDistanceForBrightness(b)*2-a.AlertDistanceForBrightness(b))<.001f,"Chase must be 50% of alert range");
                var cue=new MansionStalker.Perception{playerVisible=true,brightness=1,distance=12,playerPosition=a.transform.position};
                a.ReturnToRoaming();a.ProcessPerception(cue,3);Check(a.State==MansionStalker.BehaviourState.Alert,"Outer range must alert first");a.ProcessPerception(cue,3);Check(a.State==MansionStalker.BehaviourState.Alert,"Outer band must not chase");
                cue.distance=7;a.ProcessPerception(cue,1);Check(a.State==MansionStalker.BehaviourState.Chase,"Inner range should chase");
                a.ReturnToRoaming();a.ProcessPerception(cue,2);Check(a.State==MansionStalker.BehaviourState.Alert,"Near detection must grant alert opportunity");cue.brightness=0;a.ProcessPerception(cue,2);Check(a.State==MansionStalker.BehaviourState.Alert,"Dimming lantern should prevent escalation");
                cue.brightness=1;cue.distance=7;a.ProcessPerception(cue,2);Check(a.State==MansionStalker.BehaviourState.Chase,"Bright inner range should re-escalate");
                Check(foot.footstepClips.Length==4&&foot.footstepClips.All(c=>c!=null)&&foot.GetComponent<AudioSource>().spatialBlend==1,"Footstep clips or spatial audio missing");
                a.ReturnToRoaming();a.Agent.Warp(stairs.lowerExit);a.roamingSpeed=4;a.alertSpeed=4;a.chaseSpeed=4;a.scanSeconds=new Vector2(4,4);a.walkingSecondsBeforePause=new Vector2(.2f,.2f);
                a.Agent.SetDestination(stairs.upperExit);a.Autonomous=true;deadline=EditorApplication.timeSinceStartup+55;stage=1;return;
            }
            if(stage==1||stage==3)
            {
                Check(EditorApplication.timeSinceStartup<deadline,"Stair traversal or lookout walk timed out at "+a.transform.position+" state="+a.State+" stopped="+a.Agent.isStopped);
                if(a.IsTraversingStairs)
                {
                    Check(!a.Agent.isStopped&&!a.IsScanning,"Enemy paused to scan on stairs");
                    if(!checkedStairs)
                    {
                        a.BeginAlert(a.transform.position);Check(!a.Agent.isStopped&&a.Agent.hasPath,"Alert interrupted stairs");a.ReturnToRoaming();Check(a.Agent.hasPath,"Roam transition reset stair path");checkedStairs=true;
                    }
                }
                if(a.IsWalkingToStairLookout)Check(!a.IsScanning&&!a.Agent.isStopped,"Enemy stopped before reaching post-stair observation");
                if(!checkedStairs||a.IsTraversingStairs||a.IsWalkingToStairLookout||!a.IsScanning)return;
                Check(StalkerStairPassage.At(a.transform.position)==null,"Observation stop still on stairs");
                if(stage==1){Check(a.transform.position.y>5,"Upstairs observation not on upper floor");stationarySteps=foot.StepsPlayed;next=EditorApplication.timeSinceStartup+1;stage=2;return;}
                Check(Mathf.Abs(a.transform.position.y)<.7f,"Downstairs lookout not on ground");Check(foot.StepsPlayed>0,"Moving enemy made no footsteps");
                File.WriteAllText(Dir+"/STALKER_REFINEMENT_CHECKS.txt","PASS: outer radius alerts; inner radius is exactly 50% at all brightness levels.\nPASS: initial detection allows an alert response window; dimming prevents escalation.\nPASS: actual ascent/descent never enters a scan or stopped state on the staircase.\nPASS: Alert/Roam changes preserve the committed stair path.\nPASS: upstairs enemy continues to an observation stop; downstairs continues to a ground-floor lookout.\nPASS: four spatial footstep clips play while walking and no new steps play while scanning.\n");Finish();return;
            }
            if(stage==2)
            {
                Check(foot.StepsPlayed==stationarySteps,"Footsteps continued while scanning");a.Autonomous=false;a.ReturnToRoaming();a.Agent.Warp(stairs.upperExit);a.Agent.SetDestination(stairs.lowerExit);a.Autonomous=true;checkedStairs=false;deadline=EditorApplication.timeSinceStartup+55;stage=3;
            }
        }
        catch(Exception e){Debug.LogException(e);File.WriteAllText(Dir+"/STALKER_REFINEMENT_ERROR.txt",e.ToString());Finish();}
    }
    static void Finish(){SessionState.SetBool("StalkerRefinementTest",false);EditorApplication.update-=Test;EditorApplication.isPlaying=false;}
}
