using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System;
using System.IO;
using System.Collections.Generic;

[InitializeOnLoad]
public static class KrakenSceneSplit
{
    const string Report = @"C:\Users\SUPORTE\Documents\Codex\2026-11-01\co\work\kraken-split\inspection.json";
    [Serializable] public class Item { public string id,name,parent,prefab; public Vector3 position,rotation,size,center; public bool active; public string[] components; }
    [Serializable] public class Data { public string scene, selected; public List<Item> items = new List<Item>(); }
    static KrakenSceneSplit() { EditorApplication.delayCall += Inspect; }
    [MenuItem("Tools/Kraken/Inspect Scene")]
    public static void Inspect()
    {
        var scene=SceneManager.GetActiveScene(); var data=new Data{scene=scene.path,selected=Selection.activeGameObject?GlobalObjectId.GetGlobalObjectIdSlow(Selection.activeGameObject).ToString():""};
        foreach(var root in scene.GetRootGameObjects()) foreach(var t in root.GetComponentsInChildren<Transform>(true))
        {
            var go=t.gameObject; var renderers=go.GetComponentsInChildren<Renderer>(true); var bounds=new Bounds(t.position,Vector3.zero); bool first=true;
            foreach(var r in renderers){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
            var cs=new List<string>(); foreach(var c in go.GetComponents<Component>()) cs.Add(c?c.GetType().FullName:"MISSING");
            data.items.Add(new Item{id=GlobalObjectId.GetGlobalObjectIdSlow(go).ToString(),name=go.name,parent=t.parent?GlobalObjectId.GetGlobalObjectIdSlow(t.parent.gameObject).ToString():"",prefab=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go),position=t.position,rotation=t.eulerAngles,size=bounds.size,center=bounds.center,active=go.activeSelf,components=cs.ToArray()});
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Report)); File.WriteAllText(Report,JsonUtility.ToJson(data,true));
    }
}
