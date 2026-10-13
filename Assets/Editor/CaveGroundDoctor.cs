using UnityEngine;using UnityEditor;using System.IO;using System.Text;using StarterAssets;
[InitializeOnLoad] public static class CaveGroundDoctor {
 static CaveGroundDoctor(){EditorApplication.delayCall+=Inspect;}
 [MenuItem("Tools/Player/Inspect cave ground")]
 public static void Inspect(){
 var b=new StringBuilder();b.AppendLine("Playing="+EditorApplication.isPlaying);
 var p=GameObject.Find("PlayerArmature");if(p){var c=p.GetComponent<ThirdPersonController>();var cc=p.GetComponent<CharacterController>();b.AppendLine("Player="+p.transform.position+" Scale="+p.transform.lossyScale+" CC="+EditorJsonUtility.ToJson(cc));b.AppendLine("CTRL="+EditorJsonUtility.ToJson(c));
 foreach(var h in Physics.RaycastAll(p.transform.position+Vector3.up*.5f,Vector3.down,4,~(1<<p.layer),QueryTriggerInteraction.Ignore))b.AppendLine("FootRay "+h.collider.name+" p="+h.point+" normal="+h.normal+" layer="+h.collider.gameObject.layer);
 }
 var sel=Selection.activeGameObject;b.AppendLine("Selected="+(sel?sel.name:"none"));
 foreach(var col in Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
 if(col.name.StartsWith("PP_Cave_Tube")||col.gameObject==sel)b.AppendLine("CAVE "+col.name+" ID="+col.GetEntityId()+" bounds="+col.bounds+" convex="+col.convex+" layer="+col.gameObject.layer+" scale="+col.transform.lossyScale+" rotation="+col.transform.eulerAngles);
 }
 File.WriteAllText(PlayerSceneDoctor.W+"/cave-ground.txt",b.ToString());PlayerSceneDoctor.Inspect();
 }
}

