using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using UnityEngine.SceneManagement;
[InitializeOnLoad]
public static class PlayerSceneDoctor {
 public const string W=@"C:\Users\SUPORTE\Documents\Codex\2026-10-12\cha\work\player-fix";
 static PlayerSceneDoctor(){EditorApplication.delayCall+=Inspect;}
 [MenuItem("Tools/Player/Inspect")]
 public static void Inspect(){
 Directory.CreateDirectory(W);var b=new StringBuilder();b.AppendLine("Play="+EditorApplication.isPlaying);
 foreach(var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
 if(!t.gameObject.scene.IsValid())continue;
 if(t.name=="PlayerArmature" || t.name=="Endy" || t.name=="PlayerCameraRoot" || t.name=="PlayerFollowCamera" || t.GetComponent<Camera>() || t.GetComponent<Animator>()){
 b.AppendLine("OBJECT "+t.name+" parent="+(t.parent?t.parent.name:"none")+" active="+t.gameObject.activeInHierarchy+" pos="+t.position+" localRot="+t.localEulerAngles+" scale="+t.lossyScale+" layer="+t.gameObject.layer);
 foreach(var c in t.GetComponents<Component>()){if(!c)continue;b.AppendLine(c.GetType().FullName+" "+EditorJsonUtility.ToJson(c));}
 foreach(var r in t.GetComponentsInChildren<SkinnedMeshRenderer>())b.AppendLine("Renderer "+r.name+" bounds="+r.bounds);
 }
 }
 var p=GameObject.Find("PlayerArmature");if(p) foreach(var h in Physics.RaycastAll(p.transform.position+Vector3.up*3,Vector3.down,15)) b.AppendLine("GROUND "+h.collider.name+" point="+h.point+" layer="+h.collider.gameObject.layer+" trigger="+h.collider.isTrigger);
 File.WriteAllText(W+"/inspect.txt",b.ToString());
 }
}
