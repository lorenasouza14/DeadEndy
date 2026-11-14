using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class CaveLookTools {
 const string Report=@"C:\Users\SUPORTE\Documents\Codex\2026-09-27\cod\work\cave-look-report.txt";
 [MenuItem("Tools/Cave/Inspect Lighting")]
 public static void Inspect() {
  var b=new StringBuilder();
  b.AppendLine("Active="+SceneManager.GetActiveScene().path);
  for(int i=0;i<SceneManager.sceneCount;i++){var s=SceneManager.GetSceneAt(i);b.AppendLine("Scene="+s.path+" loaded="+s.isLoaded+" dirty="+s.isDirty);}
  b.AppendLine("Ambient="+RenderSettings.ambientMode+" "+RenderSettings.ambientLight+" intensity="+RenderSettings.ambientIntensity+" reflection="+RenderSettings.reflectionIntensity);
  foreach(SceneView v in SceneView.sceneViews)b.AppendLine("View lighting="+v.sceneLighting+" mode="+v.cameraMode.name+" camera="+v.camera.transform.position);
  foreach(var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(x=>x.enabled)){
   if(l.type==LightType.Directional || Vector3.Distance(l.transform.position,Selection.activeTransform ? Selection.activeTransform.position : Vector3.zero)<25)
    b.AppendLine("Light="+l.name+" scene="+l.gameObject.scene.name+" type="+l.type+" intensity="+l.intensity+" shadows="+l.shadows+" position="+l.transform.position+" range="+l.range+" color="+l.color);
  }
  File.WriteAllText(Report,b.ToString()); Debug.Log("Cave lighting report ready.");
 }
 [MenuItem("Tools/Cave/Apply Natural Lighting")]
 public static void ApplyNatural() {
  if(EditorApplication.isPlayingOrWillChangePlaymode) return;
  var scene=SceneManager.GetSceneByPath("Assets/PurePoly/Mining_Pack/Scenes/Demo_01.unity");
  if(!scene.IsValid() || !scene.isLoaded) return;
  string backup=Path.Combine(Path.GetDirectoryName(Report),"cave-look-backup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")); Directory.CreateDirectory(backup);
  var scenes=Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s=>s.isLoaded).ToArray();
  foreach(var s in scenes) {
   if(string.IsNullOrEmpty(s.path)) throw new InvalidOperationException("Save the untitled scene first.");
   if(!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(s)) throw new IOException("Could not preserve "+s.path);
   File.Copy(s.path,Path.Combine(backup,s.name+"-"+s.handle+".unity"));
  }
  SceneManager.SetActiveScene(scene);
  foreach(var s in scenes) if(s!=scene) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(s,false);
  Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Natural cave lighting");
  Undo.RecordObject(Unsupported.GetRenderSettings(),"Natural cave ambient");
  RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
  RenderSettings.ambientLight=new Color(0.20f,0.22f,0.25f);
  RenderSettings.reflectionIntensity=0.25f;
  var lights=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Light>(true)).ToArray();
  foreach(var l in lights) {
   Undo.RecordObject(l,"Natural cave lights");
   if(l.type==LightType.Directional) { if(l.shadows==LightShadows.None) l.enabled=false; else {l.intensity=0.55f;l.shadows=LightShadows.Soft;} }
   else { l.intensity=Mathf.Min(l.intensity*0.7f,5f); l.color=Color.Lerp(l.color,Color.white,0.12f); }
   PrefabUtility.RecordPrefabInstancePropertyModifications(l);
  }
  string dir="Assets/PurePoly/Mining_Pack/Materials/";
  string[] names={"PP_Standard_Material","PP_Ground","PP_Standard_Material_Emmissive","PP_Standard_Material_Transparent"};
  float[] rough={0.15f,0.10f,0.32f,0.45f};
  for(int i=0;i<names.Length;i++) {
   string path=dir+names[i]+".mat"; var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m)continue;
   File.Copy(path,Path.Combine(backup,names[i]+".mat"));Undo.RecordObject(m,"Natural surface response");
   m.SetFloat("_Smoothness",rough[i]);
   if(i==1)m.SetColor("_BaseColor",new Color(0.46f,0.42f,0.35f,1));
   EditorUtility.SetDirty(m);
  }
  var rp=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Settings/PC_RPAsset.asset");
  if(rp){File.Copy(AssetDatabase.GetAssetPath(rp),Path.Combine(backup,"PC_RPAsset.asset"));var so=new SerializedObject(rp);var prop=so.FindProperty("m_AdditionalLightsShadowmapResolution");if(prop!=null){prop.intValue=4096;so.ApplyModifiedProperties();}}
  foreach(SceneView v in SceneView.sceneViews){v.sceneLighting=true;v.Repaint();}
  UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);Undo.CollapseUndoOperations(group);AssetDatabase.SaveAssets();
  File.WriteAllText(Path.Combine(Path.GetDirectoryName(Report),"cave-look-applied.txt"),"Backup="+backup+"\nLights="+lights.Length+"\nApplied natural cave lighting; other scenes saved and unloaded.");
  Inspect();Debug.Log("Natural cave lighting applied. Backup: "+backup);
 }}