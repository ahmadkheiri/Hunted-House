using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ChildHorrorSetup
{
    const string Dir="Assets/PersianHouseBlockout";
    const string Path=Dir+"/PersianHouse_ChildHorror.unity";
    const string Report=Dir+"/CHILD_CONTROLLER_CHECKS.txt";
    static ChildHorrorSetup()
    {EditorApplication.delayCall+=SetupOnce;EditorApplication.playModeStateChanged+=OnPlayState;}
    static void SetupOnce()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(Path)) return;
        if(EditorApplication.isCompiling || EditorApplication.isUpdating) {EditorApplication.delayCall+=SetupOnce;return;}
        Build();
    }
    [MenuItem("Tools/Persian House/Play as Child")]
    public static void PlayChild()
    {
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(Path);
        if(!EditorApplication.isPlaying) EditorApplication.isPlaying=true;
    }
    [MenuItem("Tools/Persian House/Validate Child Controller")]
    public static void Test()
    {SessionState.SetBool("ChildHorrorTest",true);PlayChild();}

    static void Build()
    {
        Scene previous=SceneManager.GetActiveScene();Scene scene=default;
        try
        {
            scene=EditorSceneManager.OpenScene(Dir+"/PersianHouse_Blockout.unity",OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            foreach(var r in scene.GetRootGameObjects())
            {
                if(r.transform.Find("01 GROUND FLOOR - 0.00m")!=null) r.transform.localScale=Vector3.one*2f;
                foreach(var cam in r.GetComponentsInChildren<Camera>(true)) {cam.gameObject.SetActive(false);cam.tag="Untagged";}
                foreach(var t in r.GetComponentsInChildren<Transform>(true))
                {
                    if(t.name.StartsWith("ROOF -")) t.gameObject.SetActive(true);
                    if(t.name.StartsWith("04 SCALE REFERENCE")) t.gameObject.SetActive(false);
                }
            }
            var player=new GameObject("CHILD PLAYER - 1.10m tall - eye height 0.95m",typeof(CharacterController));
            player.transform.position=new Vector3(0,.04f,-38.8f);
            var controller=player.AddComponent<ChildFirstPersonController>();
            var body=player.GetComponent<CharacterController>();body.height=1.1f;body.radius=.19f;body.center=new Vector3(0,.55f,0);body.stepOffset=.38f;body.skinWidth=.025f;
            var camera=new GameObject("Child Eyes - 0.95m",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();
            camera.transform.SetParent(player.transform,false);camera.transform.localPosition=new Vector3(0,.95f,0);
            camera.tag="MainCamera";camera.fieldOfView=62;camera.nearClipPlane=.03f;camera.farClipPlane=200;
            var hd=camera.gameObject.AddComponent<HDAdditionalCameraData>();hd.clearColorMode=HDAdditionalCameraData.ClearColorMode.Color;hd.backgroundColorHDR=new Color(.18f,.19f,.21f);
            controller.playerCamera=camera;
            var torch=new GameObject("Child flashlight - F to toggle",typeof(Light));torch.transform.SetParent(camera.transform,false);torch.transform.localPosition=new Vector3(.12f,-.10f,.08f);
            var light=torch.GetComponent<Light>();light.type=LightType.Spot;light.lightUnit=LightUnit.Candela;light.intensity=4000;light.range=16;light.spotAngle=62;light.innerSpotAngle=32;light.color=new Color(1,.94f,.84f);light.shadows=LightShadows.Soft;
            torch.AddComponent<HDAdditionalLightData>();controller.flashlight=light;
            // A blockout child silhouette casts a small shadow without covering the first-person view.
            var silhouette=new GameObject("Child silhouette - shadow only");silhouette.transform.SetParent(player.transform,false);
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Dir+"/Materials/Blockout - recesses.mat");
            Shape(silhouette.transform,"Head",PrimitiveType.Sphere,new Vector3(0,.97f,0),new Vector3(.25f,.26f,.24f),mat);
            Shape(silhouette.transform,"Torso",PrimitiveType.Capsule,new Vector3(0,.61f,0),new Vector3(.30f,.20f,.22f),mat);
            foreach(float x in new[]{-.08f,.08f}) Shape(silhouette.transform,"Leg",PrimitiveType.Capsule,new Vector3(x,.23f,0),new Vector3(.11f,.22f,.12f),mat);
            foreach(float x in new[]{-.20f,.20f}) Shape(silhouette.transform,"Arm",PrimitiveType.Capsule,new Vector3(x,.62f,.015f),new Vector3(.09f,.19f,.09f),mat);
            PrefabUtility.SaveAsPrefabAsset(player,Dir+"/ChildPlayer.prefab");
            EditorSceneManager.SaveScene(scene,Path);
            RenderChildPreview(scene,camera);
            SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(Path);
            File.WriteAllText(Dir+"/CHILD_PLAYER_README.txt","PLAY: Tools > Persian House > Play as Child (or press Play).\nPlayable scene: "+Path+"\nChildPlayer.prefab is reusable. Environment is 2x on X/Y/Z. Child remains unscaled; 1 unit = 1 metre.\nChild height 1.10m; eyes 0.95m; collider radius 0.19m.\nCrouch: height 0.70m, eyes 0.56m.\nWalk 1.65m/s; sprint 2.9m/s for 4 seconds; crouch 0.8m/s.\nMouse look; WASD move; Shift sprint; Ctrl/C crouch; Space small jump; F flashlight; R restart; Esc unlock cursor.\nRoof enabled in the playable scene; original blockout scene remains unchanged.\nBody is a primitive shadow silhouette, not an animated character model.\nHead bob is disabled by default; all dimensions and movement settings are editable on the child controller.\n");
            SessionState.SetBool("ChildHorrorTest",true);EditorApplication.delayCall+=PlayChild;
        }
        catch(Exception e) {Debug.LogException(e);File.WriteAllText(Report,"SETUP FAILED\n"+e);if(previous.IsValid()) SceneManager.SetActiveScene(previous);}
    }
    static void Shape(Transform parent,string name,PrimitiveType type,Vector3 at,Vector3 scale,Material material)
    {
        var o=GameObject.CreatePrimitive(type);o.name=name;o.transform.SetParent(parent,false);o.transform.localPosition=at;o.transform.localScale=scale;
        UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>());var renderer=o.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.ShadowsOnly;
    }
    static void RenderChildPreview(Scene scene,Camera camera)
    {
        var hidden=new List<GameObject>();
        for(int i=0;i<SceneManager.sceneCount;i++)
        {
            var other=SceneManager.GetSceneAt(i);if(other==scene)continue;
            foreach(var o in other.GetRootGameObjects())if(o.activeSelf){hidden.Add(o);o.SetActive(false);}
        }
        Vector3 pos=camera.transform.parent.position;Quaternion rot=camera.transform.rotation;
        var rt=new RenderTexture(1440,900,24);var prior=RenderTexture.active;
        try
        {
            camera.transform.parent.position=new Vector3(7.2f,.03f,-13.6f);
            camera.transform.rotation=Quaternion.Euler(-8,-12,0);
            rt.Create();RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});
            RenderTexture.active=rt;var tex=new Texture2D(1440,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1440,900),0,0);tex.Apply();File.WriteAllBytes(Dir+"/Child_Eye_View.png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);
        }
        finally
        {
            camera.transform.parent.position=pos;camera.transform.rotation=rot;RenderTexture.active=prior;rt.Release();UnityEngine.Object.DestroyImmediate(rt);
            foreach(var o in hidden)if(o!=null)o.SetActive(true);
        }
    }
    static void OnPlayState(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("ChildHorrorTest",false)) EditorApplication.delayCall+=RunChecks;
    }
    static void RunChecks()
    {
        var results=new List<string>();
        try
        {
            var c=UnityEngine.Object.FindFirstObjectByType<ChildFirstPersonController>();Check(c!=null,"Child player exists");c.InputEnabled=false;
            Check(Camera.main==c.playerCamera,"Child camera is the only active main view");
            var listeners=UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);Check(listeners.Length==1,"One audio listener");
            Check(c.transform.localScale==Vector3.one,"Child scale stays 1x");
            Check(Mathf.Abs(c.Body.height-1.1f)<.01f,"1.10m child collider");
            Step(c,Vector2.zero,60);Check(Mathf.Abs(c.playerCamera.transform.position.y-c.transform.position.y-.95f)<.02f,"Eyes 0.95m above feet");
            Step(c,Vector2.up,600);Check(c.transform.position.z>-24,"Walking traverses the entrance");results.Add("PASS: child-height camera and entrance traversal.");
            c.Teleport(new Vector3(0,.04f,-37.2f),Quaternion.identity);Step(c,Vector2.right,240);Check(c.transform.position.x<3.8f,"Walls stop the controller");results.Add("PASS: corridor wall collision.");
            c.Teleport(new Vector3(0,.04f,-37.2f),Quaternion.identity);Step(c,Vector2.zero,30,true);
            var ceiling=GameObject.CreatePrimitive(PrimitiveType.Cube);ceiling.name="Temporary clearance test";ceiling.transform.position=new Vector3(0,.99f,-37.2f);ceiling.transform.localScale=new Vector3(1,.2f,1);Physics.SyncTransforms();
            Step(c,Vector2.zero,30);Check(c.IsCrouching && c.Body.height<.75f,"Cannot stand into low ceiling");UnityEngine.Object.DestroyImmediate(ceiling);Physics.SyncTransforms();Step(c,Vector2.zero,30);Check(!c.IsCrouching,"Can stand after ceiling clears");results.Add("PASS: crouch and blocked-standing clearance.");
            c.Teleport(new Vector3(-25.45f,.04f,20.8f),Quaternion.identity);Step(c,Vector2.zero,30);
            WalkTo(c,new Vector3(-25.45f,0,10.9f));WalkTo(c,new Vector3(-22.85f,0,10.9f));WalkTo(c,new Vector3(-22.85f,0,20.7f));Step(c,Vector2.zero,30);
            Check(Mathf.Abs(c.transform.position.y-8.4f)<.15f,"Climb the actual 24-step staircase");results.Add("PASS: actual west stairs reached first floor at +8.4m.");
            c.Teleport(new Vector3(-22.85f,.04f,20.7f),Quaternion.identity);Step(c,Vector2.zero,30);
            WalkTo(c,new Vector3(-22.85f,0,10.9f));WalkTo(c,new Vector3(-25.45f,0,10.9f));WalkTo(c,new Vector3(-25.45f,0,20.7f));Step(c,Vector2.zero,45);
            Check(Mathf.Abs(c.transform.position.y+6.8f)<.15f,"Descend to actual cellar");results.Add("PASS: actual cellar stairs reached -6.8m.");
            c.Respawn();Step(c,Vector2.zero,30);for(int i=0;i<270;i++)c.SimulateInput(Vector2.up,Vector2.zero,true,false,false,1f/60);
            Check(c.Stamina01<.1f && !c.IsSprinting,"Sprint stamina exhausts");Step(c,Vector2.zero,450);Check(c.Stamina01>.95f,"Stamina recovers");results.Add("PASS: limited sprint and recovery.");
            c.Respawn();Step(c,Vector2.zero,30);float y0=c.transform.position.y;c.SimulateInput(Vector2.zero,Vector2.zero,false,false,true,1f/60);Step(c,Vector2.zero,8);Check(c.transform.position.y>y0+.1f,"Small jump works");results.Add("PASS: small jump and restart.");
            File.WriteAllText(Report,"SUCCESS - actual Unity Play Mode tests\n"+string.Join("\n",results));Debug.Log("CHILD_HORROR_CHECKS_PASSED");
        }
        catch(Exception e){File.WriteAllText(Report,"FAILED\n"+string.Join("\n",results)+"\n"+e);Debug.LogException(e);}
        finally{SessionState.SetBool("ChildHorrorTest",false);EditorApplication.isPlaying=false;}
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Step(ChildFirstPersonController c,Vector2 move,int frames,bool crouch=false)
    {for(int i=0;i<frames;i++)c.SimulateInput(move,Vector2.zero,false,crouch,false,1f/60);}
    static void WalkTo(ChildFirstPersonController c,Vector3 goal)
    {
        for(int i=0;i<600;i++)
        {
            var d=goal-c.transform.position;d.y=0;if(d.magnitude<.08f)return;
            c.SimulateInput(new Vector2(d.x,d.z).normalized,Vector2.zero,false,false,false,1f/60);
        }
        throw new Exception("Stair navigation blocked at "+c.transform.position+" en route to "+goal);
    }
}
