using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class CaveCeilingAdjustment {
 [MenuItem("Tools/Cave/Raise Demo_01 Ceilings 1 Meter")]
 public static void Raise() {
  if (EditorApplication.isPlayingOrWillChangePlaymode) return;
  var s=SceneManager.GetSceneByPath("Assets/PurePoly/Mining_Pack/Scenes/Demo_01.unity");
  if(!s.IsValid() || !s.isLoaded) { Debug.LogWarning("Open Demo_01 first."); return; }
  var root=s.GetRootGameObjects().FirstOrDefault(o=>o.name=="Terrain_Stone");
  if(root==null) { Debug.LogWarning("Terrain_Stone not found."); return; }
  var roofs=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.Trim().StartsWith("PP_Stone_Ground_") && Vector3.Dot(t.up,Vector3.up)<-0.5f).ToArray();
  if(roofs.Length==0) { Debug.LogWarning("No ceilings found."); return; }
  Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup();
  Undo.SetCurrentGroupName("Raise cave ceilings 1 meter");
  Undo.RecordObjects(roofs,"Raise cave ceilings 1 meter");
  foreach(var t in roofs) { t.position+=Vector3.up; PrefabUtility.RecordPrefabInstancePropertyModifications(t); }
  EditorSceneManager.MarkSceneDirty(s); Undo.CollapseUndoOperations(group); SceneView.RepaintAll();
  Debug.Log("Cave ceiling adjustment complete: raised "+roofs.Length+" ceiling pieces by 1 meter. Floor and props unchanged. Undo with Ctrl+Z.");
 }
}