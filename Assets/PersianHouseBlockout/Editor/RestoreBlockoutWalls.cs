using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class RestoreBlockoutWalls
{
    const string Dir="Assets/PersianHouseBlockout";
    static RestoreBlockoutWalls(){EditorApplication.delayCall+=Run;}
    static void Restore(GameObject root)
    {
        var selected=root.transform.Find("05 SELECTED KITBASH - courtyard facades");
        if(selected!=null)UnityEngine.Object.DestroyImmediate(selected.gameObject);
        int count=0;
        foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name.StartsWith("Side arcade ")){t.gameObject.SetActive(true);count++;}
        if(count!=24)throw new Exception("Expected 24 original arcade groups, found "+count);
    }
    static void Run()
    {
        if(File.Exists(Dir+"/RESTORED_BLOCKOUT_WALLS.txt"))return;
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Run;return;}
        if(PrefabStageUtility.GetCurrentPrefabStage()!=null)StageUtility.GoToMainStage();
        var previous=SceneManager.GetActiveScene();var assets=new List<string>();
        try
        {
            string prefabPath=Dir+"/PersianHouse_Blockout.prefab";var prefab=PrefabUtility.LoadPrefabContents(prefabPath);
            try{Restore(prefab);PrefabUtility.SaveAsPrefabAsset(prefab,prefabPath);}finally{PrefabUtility.UnloadPrefabContents(prefab);}assets.Add(prefabPath);
            foreach(string name in new[]{"PersianHouse_Blockout","PersianHouse_ChildHorror"})
            {
                string path=Dir+"/"+name+".unity";var scene=SceneManager.GetSceneByPath(path);bool loaded=scene.IsValid()&&scene.isLoaded;
                if(!loaded)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                var root=scene.GetRootGameObjects().First(o=>o.transform.Find("01 GROUND FLOOR - 0.00m")!=null);Restore(root);
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);assets.Add(path);
                foreach(var o in scene.GetRootGameObjects()){var child=o.GetComponent<ChildFirstPersonController>();if(child!=null)ChildHorrorSetup.RenderChildPreview(scene,child.playerCamera);}
                if(!loaded)EditorSceneManager.CloseScene(scene,true);
            }
            AssetDatabase.SaveAssets();
            foreach(string path in assets)
                if(AssetDatabase.GetDependencies(path,true).Any(p=>p.Contains("SelectedKitbash")||p.Contains("Kitbash3D.Middle.East")))throw new Exception("Unexpected Kitbash dependency: "+path);
            File.WriteAllText(Dir+"/RESTORED_BLOCKOUT_WALLS.txt","Restored all 24 original arcade groups in the house prefab and both scenes.\nRemoved the two Kitbash facade instances.\nVerified: no Kitbash mesh or material dependencies remain in the house prefab or scenes.\nRetained the 1.5x environment, expanded rooms and child controller.\n");
            if(previous.IsValid())SceneManager.SetActiveScene(previous);EditorApplication.delayCall+=ChildHorrorSetup.Test;
        }
        catch(Exception e){File.WriteAllText(Dir+"/RESTORE_ERROR.txt",e.ToString());Debug.LogException(e);}
    }
}
