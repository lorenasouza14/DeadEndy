using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

public static class CaveSceneIntegration
{
    [MenuItem("Tools/Caverna/Adicionar molde ao lado e salvar")]
    public static void AddToScene()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.name != "Demo_01")
        { Debug.LogError("Abra Demo_01 fora do Play Mode."); return; }
        foreach(var item in scene.GetRootGameObjects())
            if(item.name == "Molde Caverna") { Selection.activeGameObject = item; return; }
        var oldRoots = scene.GetRootGameObjects();
        bool found=false; Bounds existing=new Bounds();
        foreach(var go in oldRoots)
            foreach(var r in go.GetComponentsInChildren<MeshRenderer>())
            { if(!found){existing=r.bounds;found=true;} else existing.Encapsulate(r.bounds); }
        CaveBlockoutSetup.Create();
        var root=Selection.activeGameObject;
        if(root == null || root.name != "Molde Caverna") return;
        var renderers=root.GetComponentsInChildren<MeshRenderer>();
        Bounds cave=renderers[0].bounds;
        foreach(var r in renderers) cave.Encapsulate(r.bounds);
        if(found) root.transform.position=new Vector3(existing.max.x + 20 - cave.min.x,0,existing.center.z-cave.center.z);
        PrefabUtility.SaveAsPrefabAsset(root,"Assets/MoldeCaverna/MoldeCaverna.prefab");
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved=EditorSceneManager.SaveScene(scene);
        if(SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.LookAt(root.transform.position + new Vector3(0,0,0),Quaternion.Euler(65,0,0),90);
        string report="Scene="+scene.path+"\nSaved="+saved+"\nOriginalRoots="+oldRoots.Length+"\nCurrentRoots="+scene.rootCount+"\nPosition="+root.transform.position+"\n";
        foreach(var go in oldRoots) report+="Preserved="+go.name+":"+(go!=null)+"\n";
        foreach(var mf in root.GetComponentsInChildren<MeshFilter>(true)) report+="Mesh="+mf.name+" Vertices="+mf.sharedMesh.vertexCount+"\n";
        File.WriteAllText("Assets/MoldeCaverna/IntegrationReport.txt",report);
        AssetDatabase.Refresh();
        Debug.Log("CAVERNA_INTEGRADA " + report);
    }
}
