using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Absolute scale assignment: re-running never compounds 2x into 4x.
[InitializeOnLoad]
public static class PersianHouseScaleSetup
{
    const string Dir="Assets/PersianHouseBlockout";
    public const float EnvironmentScale=2f;
    static PersianHouseScaleSetup(){EditorApplication.delayCall+=AutoApply;}
    static void AutoApply()
    {
        if(File.Exists(Dir+"/ENVIRONMENT_SCALE_2X.txt"))return;
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {EditorApplication.delayCall+=AutoApply;return;}
        Apply();
    }
    [MenuItem("Tools/Persian House/Apply 2x Environment")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(PrefabStageUtility.GetCurrentPrefabStage()!=null)StageUtility.GoToMainStage();
        var previous=SceneManager.GetActiveScene();
        try
        {
            var prefab=PrefabUtility.LoadPrefabContents(Dir+"/PersianHouse_Blockout.prefab");
            try{prefab.transform.localScale=Vector3.one*EnvironmentScale;PrefabUtility.SaveAsPrefabAsset(prefab,Dir+"/PersianHouse_Blockout.prefab");}
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
            foreach(string name in new[]{"PersianHouse_Blockout","PersianHouse_ChildHorror"})
            {
                string path=Dir+"/"+name+".unity";var scene=SceneManager.GetSceneByPath(path);
                bool wasLoaded=scene.IsValid()&&scene.isLoaded;
                if(!wasLoaded)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                foreach(var obj in scene.GetRootGameObjects())
                {
                    if(obj.transform.Find("01 GROUND FLOOR - 0.00m")!=null)
                    {obj.transform.localScale=Vector3.one*EnvironmentScale;obj.name="Persian House | 2x environment | 54.4 x 81.6m";}
                    var child=obj.GetComponent<ChildFirstPersonController>();
                    if(child!=null)
                    {
                        obj.transform.localScale=Vector3.one;obj.transform.position=new Vector3(0,.04f,-38.8f);
                        child.maximumStepHeight=.38f;obj.GetComponent<CharacterController>().stepOffset=.38f;
                        child.playerCamera.farClipPlane=300;
                    }
                    if(obj.name=="Overview Camera")
                    {obj.transform.position=new Vector3(-66,78,-92);obj.transform.LookAt(new Vector3(0,2,0));obj.GetComponent<Camera>().farClipPlane=400;}
                }
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                if(!wasLoaded)EditorSceneManager.CloseScene(scene,true);
            }
            var player=PrefabUtility.LoadPrefabContents(Dir+"/ChildPlayer.prefab");
            try
            {
                player.transform.localScale=Vector3.one;player.transform.position=new Vector3(0,.04f,-38.8f);
                player.GetComponent<ChildFirstPersonController>().maximumStepHeight=.38f;
                player.GetComponent<CharacterController>().stepOffset=.38f;
                player.GetComponentInChildren<Camera>().farClipPlane=300;
                PrefabUtility.SaveAsPrefabAsset(player,Dir+"/ChildPlayer.prefab");
            }
            finally{PrefabUtility.UnloadPrefabContents(player);}
            AssetDatabase.SaveAssets();
            File.WriteAllText(Dir+"/ENVIRONMENT_SCALE_2X.txt","Environment scale: X=2, Y=2, Z=2 (absolute).\nFootprint: 54.4 x 81.6m; courtyard: 35.2 x 37.6m.\nFirst floor: +8.4m; cellar: -6.8m.\nChild unchanged: body 1.10m, eyes 0.95m, scale 1,1,1.\nSpawn moved to the scaled entrance. Step allowance 0.38m supports the enlarged 0.35m stair risers.\nEarlier BUILD_REPORT dimensions describe the original 1x source layout.\n");
            if(previous.IsValid())SceneManager.SetActiveScene(previous);
            EditorApplication.delayCall+=ChildHorrorSetup.Test;
        }
        catch(Exception e){Debug.LogException(e);File.WriteAllText(Dir+"/SCALE_ERROR.txt",e.ToString());}
    }
}
