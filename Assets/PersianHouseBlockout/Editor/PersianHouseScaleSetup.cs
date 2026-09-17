using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Absolute overall scale plus a one-time, room-wing expansion in the source layout.
[InitializeOnLoad]
public static class PersianHouseScaleSetup
{
    const string Dir="Assets/PersianHouseBlockout";
    public const float EnvironmentScale=1.5f;
    const string Marker="Layout - wider room wings v1";
    public static float WingX(float x){return Mathf.Sign(x)*(Mathf.Min(Mathf.Abs(x),10.6f)+Mathf.Max(0,Mathf.Abs(x)-10.6f)*1.5f);}
    public static float WingZ(float z){return Mathf.Sign(z)*(Mathf.Min(Mathf.Abs(z),11.2f)+Mathf.Max(0,Mathf.Abs(z)-11.2f)*1.25f);}
    public static Vector3 LayoutPoint(float x,float y,float z){return new Vector3(WingX(x)*EnvironmentScale,y,WingZ(z)*EnvironmentScale);}
    public static void Resize(Transform root)
    {
        root.localScale=Vector3.one;
        if(root.Find(Marker)==null)
        {
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var t=renderer.transform;var b=renderer.localBounds;Bounds bounds=new Bounds();bool first=true;
                for(int i=0;i<8;i++)
                {
                    var local=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    var p=root.InverseTransformPoint(t.TransformPoint(local));if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
                }
                float xmin=WingX(bounds.min.x),xmax=WingX(bounds.max.x),zmin=WingZ(bounds.min.z),zmax=WingZ(bounds.max.z);
                var pos=root.InverseTransformPoint(t.position);pos.x+=(xmin+xmax)/2-bounds.center.x;pos.z+=(zmin+zmax)/2-bounds.center.z;
                float fx=(xmax-xmin)/Mathf.Max(.0001f,bounds.size.x),fz=(zmax-zmin)/Mathf.Max(.0001f,bounds.size.z);
                var axis=root.InverseTransformVector(t.TransformVector(Vector3.right)).normalized;
                var scale=t.localScale;scale.x*=Mathf.Abs(axis.x)>.5f?fx:fz;scale.z*=Mathf.Abs(axis.x)>.5f?fz:fx;
                t.position=root.TransformPoint(pos);t.localScale=scale;
            }
            new GameObject(Marker).transform.SetParent(root,false);
        }
        root.localScale=Vector3.one*EnvironmentScale;
    }
    static PersianHouseScaleSetup(){EditorApplication.delayCall+=AutoApply;}
    static void AutoApply()
    {
        if(File.Exists(Dir+"/ENVIRONMENT_SCALE_1_5X.txt"))return;
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {EditorApplication.delayCall+=AutoApply;return;}
        Apply();
    }
    [MenuItem("Tools/Persian House/Apply 1.5x Environment and Spacious Rooms")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(PrefabStageUtility.GetCurrentPrefabStage()!=null)StageUtility.GoToMainStage();
        var previous=SceneManager.GetActiveScene();
        try
        {
            var prefab=PrefabUtility.LoadPrefabContents(Dir+"/PersianHouse_Blockout.prefab");
            try{Resize(prefab.transform);PrefabUtility.SaveAsPrefabAsset(prefab,Dir+"/PersianHouse_Blockout.prefab");}
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
            foreach(string name in new[]{"PersianHouse_Blockout","PersianHouse_ChildHorror"})
            {
                string path=Dir+"/"+name+".unity";var scene=SceneManager.GetSceneByPath(path);
                bool wasLoaded=scene.IsValid()&&scene.isLoaded;
                if(!wasLoaded)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                foreach(var obj in scene.GetRootGameObjects())
                {
                    if(obj.transform.Find("01 GROUND FLOOR - 0.00m")!=null)
                    {Resize(obj.transform);obj.name="Persian House | 1.5x environment | spacious room wings";}
                    var child=obj.GetComponent<ChildFirstPersonController>();
                    if(child!=null)
                    {
                        obj.transform.localScale=Vector3.one;obj.transform.position=LayoutPoint(0,.04f,-19.4f);
                        child.maximumStepHeight=.38f;obj.GetComponent<CharacterController>().stepOffset=.38f;
                        child.playerCamera.farClipPlane=300;
                    }
                    if(obj.name=="Overview Camera")
                    {obj.transform.position=new Vector3(-55,65,-76);obj.transform.LookAt(new Vector3(0,2,0));obj.GetComponent<Camera>().farClipPlane=400;}
                }
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                foreach(var obj in scene.GetRootGameObjects())
                {var child=obj.GetComponent<ChildFirstPersonController>();if(child!=null)ChildHorrorSetup.RenderChildPreview(scene,child.playerCamera);}
                if(!wasLoaded)EditorSceneManager.CloseScene(scene,true);
            }
            var player=PrefabUtility.LoadPrefabContents(Dir+"/ChildPlayer.prefab");
            try
            {
                player.transform.localScale=Vector3.one;player.transform.position=LayoutPoint(0,.04f,-19.4f);
                player.GetComponent<ChildFirstPersonController>().maximumStepHeight=.38f;
                player.GetComponent<CharacterController>().stepOffset=.38f;
                player.GetComponentInChildren<Camera>().farClipPlane=300;
                PrefabUtility.SaveAsPrefabAsset(player,Dir+"/ChildPlayer.prefab");
            }
            finally{PrefabUtility.UnloadPrefabContents(player);}
            AssetDatabase.SaveAssets();
            File.WriteAllText(Dir+"/ENVIRONMENT_SCALE_1_5X.txt","CURRENT LAYOUT: overall X/Y/Z scale 1.5, with expanded room wings.\nFootprint approximately 45.3 x 68.1m (was 54.4 x 81.6m).\nCourtyard: 26.4 x 28.2m. First floor +6.3m; cellar -5.1m.\nSide room wings widened 50% in source space; north and south rooms deepened 25%.\nTypical side-room clear width approx. 6.08m (was 5.4m in the 2x layout).\nChild unchanged: body 1.10m, eyes 0.95m.\nAll blockout parts remain separate editable objects, including colliders.\nThis report supersedes ENVIRONMENT_SCALE_2X and original BUILD_REPORT dimensions.\n");
            if(previous.IsValid())SceneManager.SetActiveScene(previous);
            EditorApplication.delayCall+=ChildHorrorSetup.Test;
        }
        catch(Exception e){Debug.LogException(e);File.WriteAllText(Dir+"/SCALE_ERROR.txt",e.ToString());}
    }
}
