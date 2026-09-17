using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class GlobalLightingSetup
{
    const string Dir="Assets/PersianHouseBlockout";
    static GlobalLightingSetup(){EditorApplication.delayCall+=Install;EditorApplication.playModeStateChanged+=Changed;}
    static void Install()
    {
        if(File.Exists(Dir+"/GLOBAL_LIGHTING_CHECKS.txt"))return;
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Install;return;}
        var previous=SceneManager.GetActiveScene();string path=Dir+"/PersianHouse_ChildHorror.unity";var scene=SceneManager.GetSceneByPath(path);bool loaded=scene.IsValid()&&scene.isLoaded;
        if(!loaded)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
        var player=scene.GetRootGameObjects().First(o=>o.GetComponent<ChildFirstPersonController>()!=null);
        if(player.GetComponent<GlobalLightingToggle>()==null)player.AddComponent<GlobalLightingToggle>();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);if(!loaded)EditorSceneManager.CloseScene(scene,true);
        var prefab=PrefabUtility.LoadPrefabContents(Dir+"/ChildPlayer.prefab");
        try{if(prefab.GetComponent<GlobalLightingToggle>()==null)prefab.AddComponent<GlobalLightingToggle>();PrefabUtility.SaveAsPrefabAsset(prefab,Dir+"/ChildPlayer.prefab");}finally{PrefabUtility.UnloadPrefabContents(prefab);}
        if(previous.IsValid())SceneManager.SetActiveScene(previous);AssetDatabase.SaveAssets();
        SessionState.SetBool("GlobalLightingTest",true);EditorApplication.delayCall+=ChildHorrorSetup.PlayChild;
    }
    static void Changed(PlayModeStateChange state)
    {if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("GlobalLightingTest",false))EditorApplication.delayCall+=Test;}
    static void Test()
    {
        try
        {
            var player=UnityEngine.Object.FindFirstObjectByType<ChildFirstPersonController>();player.InputEnabled=false;
            var toggle=player.GetComponent<GlobalLightingToggle>();
            var lights=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(l=>l.gameObject.scene==player.gameObject.scene&&!l.transform.IsChildOf(player.transform)).ToArray();
            var states=lights.Select(l=>l.enabled).ToArray();bool flash=player.flashlight.enabled;
            if(lights.Length==0)throw new Exception("No scene lights found");
            toggle.SetLighting(false);
            if(toggle.IsOn || lights.Any(l=>l.enabled) || player.flashlight.enabled!=flash)throw new Exception("Off toggle failed or changed flashlight");
            toggle.SetLighting(true);
            if(!toggle.IsOn || lights.Where((l,i)=>l.enabled!=states[i]).Any() || player.flashlight.enabled!=flash)throw new Exception("On toggle failed to restore state");
            var rect=GlobalLightingToggle.ButtonRect;
            if(!GlobalLightingToggle.IsPointerOverButton(new Vector2(rect.center.x,Screen.height-rect.center.y)))throw new Exception("Button pointer exclusion failed");
            File.WriteAllText(Dir+"/GLOBAL_LIGHTING_CHECKS.txt","PASS: global lights turn off and restore their original states.\nPASS: flashlight stays unchanged.\nPASS: UI hit region is excluded from mouse-look recapture.\nControls: L toggles while playing; Esc unlocks the pointer to click Global Lighting ON/OFF in the top-right.\n");
        }
        catch(Exception e){File.WriteAllText(Dir+"/GLOBAL_LIGHTING_ERROR.txt",e.ToString());Debug.LogException(e);}
        finally{SessionState.SetBool("GlobalLightingTest",false);EditorApplication.isPlaying=false;}
    }
}
