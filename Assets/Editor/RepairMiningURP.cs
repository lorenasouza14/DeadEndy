using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal;
[InitializeOnLoad]
public static class RepairMiningURP {
 const string Work=@"C:\Users\SUPORTE\Documents\Codex\2026-10-12\cha\work\mining-urp";
 static RepairMiningURP(){EditorApplication.delayCall+=Auto;}
 static void Auto(){if(!File.Exists(Work+"/done-3d.txt")) Run();}
 [MenuItem("Tools/Caverna/Corrigir materiais Mining URP")]
 public static void Run(){
  if(EditorApplication.isPlayingOrWillChangePlaymode) return;
  Directory.CreateDirectory(Work);
  var upgrades=new List<MaterialUpgrader>();
  foreach(var t in typeof(StandardUpgrader).Assembly.GetTypes().Where(t=>!t.IsAbstract && t.GetInterfaces().Any(i=>i.Name=="IMaterialUpgradersProvider"))) {
   var method=t.GetMethod("GetUpgraders"); if(method==null)continue;
   try{upgrades.AddRange((IEnumerable<MaterialUpgrader>)method.Invoke(Activator.CreateInstance(t,true),null));}catch{}
  }
  var report=new StringBuilder(); int changed=0;
  foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/PurePoly/Mining_Pack/Materials"})){
   var path=AssetDatabase.GUIDToAssetPath(guid);var m=AssetDatabase.LoadAssetAtPath<Material>(path);
   if(!m || !m.shader) {report.AppendLine("MISSING "+path);continue;}
   string old=m.shader.name;
   var upgrader=(old=="Standard" || old=="Standard (Specular setup)") ? new StandardUpgrader(old) : upgrades.FirstOrDefault(u=>u.OldShaderPath==old);
   if(upgrader!=null){
    string backup=Path.Combine(Work,Path.GetFileName(path)+".bak"); if(!File.Exists(backup))File.Copy(path,backup);
    Undo.RecordObject(m,"Converter material para URP");
    upgrader.Upgrade(m,(MaterialUpgrader.UpgradeFlags)0);
    EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);changed++;
   }
   report.AppendLine(path+" | "+old+" -> "+m.shader.name+" | supported="+m.shader.isSupported+" | shaderError="+ShaderUtil.ShaderHasError(m.shader));
  }
  report.AppendLine("Changed="+changed);
  File.WriteAllText(Work+"/done-3d.txt",report.ToString());
  SceneView.RepaintAll();Debug.Log("MINING_URP_REPAIR "+report);
 }
}

