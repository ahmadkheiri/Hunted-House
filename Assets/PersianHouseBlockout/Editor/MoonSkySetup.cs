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
public static class MoonSkySetup
{
    const string Dir="Assets/PersianHouseBlockout";
    static double next;static int stage;
    static MoonSkySetup(){EditorApplication.delayCall+=Install;EditorApplication.playModeStateChanged+=Changed;}
    static void Install()
    {
        if(File.Exists(Dir+"/MOON_SKY_CHECKS.txt"))return;
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Install;return;}
        if(SessionState.GetBool("MoonSkyTest",false))return;
        var previous=SceneManager.GetActiveScene();string path=Dir+"/PersianHouse_ChildHorror.unity";
        var scene=SceneManager.GetSceneByPath(path);bool loaded=scene.IsValid()&&scene.isLoaded;
        if(!loaded)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var player=scene.GetRootGameObjects().Select(o=>o.GetComponent<ChildFirstPersonController>()).First(c=>c!=null);
        foreach(var light in scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Light>(true)))if(!light.transform.IsChildOf(player.transform))light.enabled=false;
        var root=scene.GetRootGameObjects().FirstOrDefault(o=>o.name=="Night sky and moon");if(root!=null)UnityEngine.Object.DestroyImmediate(root);
        root=new GameObject("Night sky and moon");var controller=root.AddComponent<MoonLightingController>();
        var moon=new GameObject("Moon - directional light");moon.transform.SetParent(root.transform,false);
        var lightSource=moon.AddComponent<Light>();lightSource.type=LightType.Directional;lightSource.color=new Color(.78f,.86f,1);lightSource.shadows=LightShadows.Soft;
        var hd=moon.AddComponent<HDAdditionalLightData>();hd.interactsWithSky=true;hd.angularDiameter=1.2f;hd.flareMultiplier=0;hd.distance=384400000;hd.surfaceTexture=MoonTexture();hd.surfaceTint=Color.white;
        controller.moonLight=lightSource;controller.Apply();
        var volume=root.AddComponent<Volume>();volume.isGlobal=true;volume.priority=500;volume.sharedProfile=Profile();
        var camera=player.playerCamera.GetComponent<HDAdditionalCameraData>();camera.clearColorMode=HDAdditionalCameraData.ClearColorMode.Sky;
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        if(!loaded)EditorSceneManager.CloseScene(scene,true);if(previous.IsValid())SceneManager.SetActiveScene(previous);
        var prefab=PrefabUtility.LoadPrefabContents(Dir+"/ChildPlayer.prefab");
        try{prefab.GetComponent<ChildFirstPersonController>().playerCamera.GetComponent<HDAdditionalCameraData>().clearColorMode=HDAdditionalCameraData.ClearColorMode.Sky;PrefabUtility.SaveAsPrefabAsset(prefab,Dir+"/ChildPlayer.prefab");}finally{PrefabUtility.UnloadPrefabContents(prefab);}
        AssetDatabase.SaveAssets();SessionState.SetBool("MoonSkyTest",true);EditorApplication.delayCall+=ChildHorrorSetup.PlayChild;
    }
    static T Add<T>(VolumeProfile p) where T:VolumeComponent
    {var c=p.Add<T>(true);AssetDatabase.AddObjectToAsset(c,p);return c;}
    static VolumeProfile Profile()
    {
        string path=Dir+"/MoonlitNight.asset";var p=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);if(p!=null)return p;
        p=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(p,path);
        Add<VisualEnvironment>(p).skyType.Override((int)SkyType.PhysicallyBased);
        var sky=Add<PhysicallyBasedSky>(p);sky.spaceEmissionTexture.Override(Stars());sky.spaceEmissionMultiplier.Override(.12f);sky.atmosphericScattering.Override(false);sky.updateMode.Override(EnvironmentUpdateMode.OnChanged);
        var e=Add<Exposure>(p);e.mode.Override(ExposureMode.Fixed);e.fixedExposure.Override(-2);
        Add<Fog>(p).enabled.Override(false);
        var b=Add<Bloom>(p);b.intensity.Override(.025f);b.scatter.Override(.25f);
        var indirect=Add<IndirectLightingController>(p);indirect.indirectDiffuseLightingMultiplier.Override(.15f);indirect.reflectionLightingMultiplier.Override(.2f);
        EditorUtility.SetDirty(p);return p;
    }
    static Cubemap Stars()
    {
        string path=Dir+"/NightStars.asset";var existing=AssetDatabase.LoadAssetAtPath<Cubemap>(path);if(existing!=null)return existing;
        const int n=512;var cube=new Cubemap(n,TextureFormat.RGBAHalf,true){name="Procedural night stars",filterMode=FilterMode.Trilinear};var random=new System.Random(917);
        for(int face=0;face<6;face++)
        {
            var pixels=new Color[n*n];for(int i=0;i<pixels.Length;i++)pixels[i]=new Color(.0001f,.00018f,.0004f,1);
            for(int j=0;j<230;j++)
            {
                int x=random.Next(2,n-2),y=random.Next(2,n-2);float strength=.3f+(float)Math.Pow(random.NextDouble(),4)*5;
                Color tint=Color.Lerp(new Color(.65f,.77f,1),new Color(1,.86f,.7f),(float)random.NextDouble());
                pixels[y*n+x]=tint*strength;
                if(strength>2)foreach(var d in new[]{new Vector2Int(1,0),new Vector2Int(-1,0),new Vector2Int(0,1),new Vector2Int(0,-1)})pixels[(y+d.y)*n+x+d.x]=tint*strength*.12f;
            }
            cube.SetPixels(pixels,(CubemapFace)face);
        }
        cube.Apply(true,false);AssetDatabase.CreateAsset(cube,path);return cube;
    }
    static Texture2D MoonTexture()
    {
        string path=Dir+"/MoonSurface.asset";var old=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(old!=null)return old;
        const int n=256;var t=new Texture2D(n,n,TextureFormat.RGBA32,true,true){name="Blockout moon surface",wrapMode=TextureWrapMode.Clamp};var colors=new Color[n*n];
        var craters=new Vector3[45];var random=new System.Random(42);for(int i=0;i<craters.Length;i++)craters[i]=new Vector3((float)random.NextDouble(),(float)random.NextDouble(),.01f+(float)random.NextDouble()*.07f);
        for(int y=0;y<n;y++)for(int x=0;x<n;x++)
        {
            float u=x/(float)n,v=y/(float)n;float value=.35f+.4f*Mathf.PerlinNoise(u*5+8,v*5+3)+.1f*Mathf.PerlinNoise(u*39,v*39);
            foreach(var c in craters){float d=Vector2.Distance(new Vector2(u,v),new Vector2(c.x,c.y))/c.z;if(d<1)value*=.7f+.3f*d;else if(d<1.13f)value+=.07f;}
            colors[y*n+x]=new Color(value,value,value,1);
        }
        t.SetPixels(colors);t.Apply();AssetDatabase.CreateAsset(t,path);return t;
    }
    static void Changed(PlayModeStateChange s)
    {if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("MoonSkyTest",false)){stage=0;next=EditorApplication.timeSinceStartup+5;EditorApplication.update+=Test;}}
    static void Test()
    {
        if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+4;
        try
        {
            var p=UnityEngine.Object.FindFirstObjectByType<ChildFirstPersonController>();var m=MoonLightingController.Current;
            if(stage==0)
            {
                p.InputEnabled=false;p.Teleport(new Vector3(4,.05f,-6),Quaternion.identity);p.SimulateInput(Vector2.zero,new Vector2(0,35/p.mouseSensitivity),false,false,false,1f/60);
                if(m==null||m.moonLight.intensity!=.15f)throw new Exception("Moon source missing or wrong intensity");
                var q=m.moonLight.transform.rotation;m.azimuth=80;m.elevation=25;m.Apply();if(Quaternion.Angle(q,m.moonLight.transform.rotation)<10)throw new Exception("Moon angle failed");
                m.intensityLux=5;m.Apply();if(m.moonLight.intensity!=1)throw new Exception("Intensity clamp failed");
                m.elevation=45;m.azimuth=0;m.intensityLux=.15f;m.Apply();
                var g=p.GetComponent<GlobalLightingToggle>();g.SetLighting(false);if(m.moonLight.enabled||!p.flashlight.enabled)throw new Exception("Global off failed");g.SetLighting(true);if(!m.moonLight.enabled)throw new Exception("Global restore failed");
            }
            if(stage==1){p.InputEnabled=true;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;ScreenCapture.CaptureScreenshot(Dir+"/MoonlitNight_Preview.png");}
            if(stage==2){File.WriteAllText(Dir+"/MOON_SKY_CHECKS.txt","PASS: Night scene loads with 0.15 lux moon.\nPASS: elevation and azimuth rotate the moon light.\nPASS: moon intensity clamps to 0..1 lux.\nPASS: L disables/restores moonlight while preserving the lantern.\nNight sky preview rendered in Play Mode.\n");Finish();}
            stage++;
        }
        catch(Exception e){File.WriteAllText(Dir+"/MOON_SKY_ERROR.txt",e.ToString());Debug.LogException(e);Finish();}
    }
    static void Finish(){SessionState.SetBool("MoonSkyTest",false);EditorApplication.update-=Test;EditorApplication.isPlaying=false;}
}
