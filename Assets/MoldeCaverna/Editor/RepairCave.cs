using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class RepairCave {
 [MenuItem("Tools/Caverna/Restaurar piso e paredes")]
 public static void Repair() {
  if(EditorApplication.isPlaying) { Debug.LogError("Saia do Play Mode primeiro."); return; }
  var root=GameObject.Find("Molde Caverna"); if(!root) return;
  foreach(var name in new[]{"Cave_Floor","Cave_Walls"}) {
   var t=root.transform.Find(name);
   if(!t) {
    var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MoldeCaverna/Meshes/"+name+".obj");
    var go=(GameObject)PrefabUtility.InstantiatePrefab(asset,root.transform);
    go.name=name; go.transform.localPosition=Vector3.zero; go.transform.localRotation=Quaternion.identity; go.transform.localScale=Vector3.one;
    Undo.RegisterCreatedObjectUndo(go,"Restaurar caverna"); t=go.transform;
    var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/MoldeCaverna/"+name+".mat");
    foreach(var r in go.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial=mat;
    foreach(var mf in go.GetComponentsInChildren<MeshFilter>()) {var c=go.AddComponent<MeshCollider>(); c.sharedMesh=mf.sharedMesh;}
   }
   t.gameObject.SetActive(true);
   SceneVisibilityManager.instance.Show(t.gameObject,true);
  }
  Selection.activeGameObject=root;
  EditorSceneManager.MarkSceneDirty(root.scene);
  if(SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.LookAt(root.transform.position,Quaternion.Euler(65,0,0),70);
  Debug.Log("Piso e paredes restaurados. Outras alteracoes da cena foram preservadas.");
 }
}
