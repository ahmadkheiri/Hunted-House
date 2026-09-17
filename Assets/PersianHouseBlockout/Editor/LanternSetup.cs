using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class LanternSetup
{
    const string Dir="Assets/PersianHouseBlockout";
    static LanternSetup(){EditorApplication.delayCall+=Install;EditorApplication.playModeStateChanged+=Changed;}
    static void Install()
    {
        if(File.Exists(Dir+"/LANTERN_CHECKS.txt"))return;
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Install;return;}
        if(SessionState.GetBool("LanternTest",false))return;
        var previous=SceneManager.GetActiveScene();var path=Dir+"/PersianHouse_ChildHorror.unity";
        var scene=SceneManager.GetSceneByPath(path);bool loaded=scene.IsValid()&&scene.isLoaded;
        if(!loaded)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
        Configure(scene.GetRootGameObjects().Select(o=>o.GetComponent<ChildFirstPersonController>()).First(c=>c!=null));
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);if(!loaded)EditorSceneManager.CloseScene(scene,true);
        var prefab=PrefabUtility.LoadPrefabContents(Dir+"/ChildPlayer.prefab");
        try{Configure(prefab.GetComponent<ChildFirstPersonController>());PrefabUtility.SaveAsPrefabAsset(prefab,Dir+"/ChildPlayer.prefab");}finally{PrefabUtility.UnloadPrefabContents(prefab);}
        if(previous.IsValid())SceneManager.SetActiveScene(previous);AssetDatabase.SaveAssets();
        SessionState.SetBool("LanternTest",true);EditorApplication.delayCall+=ChildHorrorSetup.PlayChild;
    }
    public static void Configure(ChildFirstPersonController player)
    {
        if(player.flashlight!=null)UnityEngine.Object.DestroyImmediate(player.flashlight.gameObject);
        var old=player.playerCamera.transform.Find("Carried lantern");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
        var controller=player.GetComponent<LanternController>();if(controller==null)controller=player.gameObject.AddComponent<LanternController>();
        var root=new GameObject("Carried lantern");root.transform.SetParent(player.playerCamera.transform,false);root.transform.localPosition=new Vector3(.22f,-.25f,.48f);
        Material metal=MaterialAt("Lantern - dark brass",new Color(.12f,.075f,.027f),false);
        Material flame=MaterialAt("Lantern - flame",new Color(1,.5f,.1f),true);
        Shape(root.transform,"Fuel reservoir",PrimitiveType.Cylinder,new Vector3(0,-.10f,0),new Vector3(.115f,.022f,.115f),metal);
        Shape(root.transform,"Chimney cap",PrimitiveType.Cylinder,new Vector3(0,.10f,0),new Vector3(.12f,.018f,.12f),metal);
        Shape(root.transform,"Vent",PrimitiveType.Cylinder,new Vector3(0,.127f,0),new Vector3(.055f,.013f,.055f),metal);
        for(int i=0;i<4;i++){float a=(45+i*90)*Mathf.Deg2Rad;Shape(root.transform,"Cage post "+i,PrimitiveType.Cube,new Vector3(Mathf.Cos(a)*.048f,0,Mathf.Sin(a)*.048f),new Vector3(.009f,.19f,.009f),metal);}
        for(int i=0;i<12;i++){float a=i*Mathf.PI/11;var p=Shape(root.transform,"Handle "+i,PrimitiveType.Sphere,new Vector3(Mathf.Cos(a)*.065f,.14f+Mathf.Sin(a)*.065f,0),new Vector3(.024f,.01f,.01f),metal);p.transform.localRotation=Quaternion.Euler(0,0,a*Mathf.Rad2Deg+90);}
        controller.flame=Shape(root.transform,"Flame",PrimitiveType.Sphere,new Vector3(0,-.025f,0),new Vector3(.022f,.053f,.022f),flame);
        var source=new GameObject("Lantern point light",typeof(Light));source.transform.SetParent(root.transform,false);
        var light=source.GetComponent<Light>();light.type=LightType.Point;light.shadows=LightShadows.Soft;source.AddComponent<HDAdditionalLightData>();
        controller.lanternLight=light;player.flashlight=light;controller.ApplyLight(0);
    }
    static Material MaterialAt(string name,Color color,bool emissive)
    {
        string path=Dir+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m!=null)return m;
        m=new Material(Shader.Find("HDRP/Lit")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",emissive?0:.7f);m.SetFloat("_Smoothness",.35f);
        if(emissive)m.SetColor("_EmissiveColor",new Color(1,.35f,.05f)*1200);
        AssetDatabase.CreateAsset(m,path);return m;
    }
    static Renderer Shape(Transform root,string name,PrimitiveType type,Vector3 at,Vector3 scale,Material mat)
    {
        var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(root,false);go.transform.localPosition=at;go.transform.localScale=scale;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());var r=go.GetComponent<Renderer>();r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;return r;
    }
    static void Changed(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("LanternTest",false))EditorApplication.delayCall+=Test;}
    static void Test()
    {
        try
        {
            var p=UnityEngine.Object.FindFirstObjectByType<ChildFirstPersonController>();p.InputEnabled=false;var l=p.GetComponent<LanternController>();
            l.flickerAmount=0;l.ApplyLight(0);
            if(l.lanternLight.type!=LightType.Point||Mathf.Abs(l.lanternLight.intensity-60/(4*Mathf.PI))>.01f)throw new Exception("Point light units failed");
            for(int i=0;i<50;i++)l.AdjustBrightness(-1);if(l.lumens!=10)throw new Exception("Lower limit");
            for(int i=0;i<50;i++)l.AdjustBrightness(1);if(l.lumens!=200)throw new Exception("Upper limit");
            l.SetLit(false);if(l.lanternLight.enabled||l.flame.enabled||l.ActualLumens!=0)throw new Exception("Off failed");
            l.SetLit(true);if(!l.lanternLight.enabled||!l.flame.enabled)throw new Exception("On failed");
            p.GetComponent<GlobalLightingToggle>().SetLighting(false);if(!l.lanternLight.enabled)throw new Exception("Global lighting affected lantern");
            l.lumens=60;l.ApplyLight(0);
            foreach(var v in p.GetComponentsInChildren<Volume>())if(v.name=="Lantern eye adaptation")v.weight=1;
            ChildHorrorSetup.RenderChildPreview(p.gameObject.scene,p.playerCamera);
            File.Copy(Dir+"/Child_Eye_View.png",Dir+"/Lantern_Preview.png",true);
            File.WriteAllText(Dir+"/LANTERN_CHECKS.txt","PASS: Point source converts 60 lumens to 4.775 candela.\nPASS: Brightness clamps to 10..200 lumens.\nPASS: Switching off disables light and flame; switching on restores them.\nPASS: Global lighting toggle leaves lantern active.\nControls: F on/off; 1 dimmer; 2 brighter. Esc to click corner buttons.\n");
        }
        catch(Exception e){File.WriteAllText(Dir+"/LANTERN_ERROR.txt",e.ToString());Debug.LogException(e);}
        finally{SessionState.SetBool("LanternTest",false);EditorApplication.isPlaying=false;}
    }
}
