using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BloqueiosGuiadosSetup
{
    static BloqueiosGuiadosSetup() { EditorApplication.update += Pendente; }
    static void Pendente()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode ||
            !File.Exists("Logs/configurar-cubos-pendente.txt")) return;
        File.Delete("Logs/configurar-cubos-pendente.txt");
        try { Configurar(); } catch (Exception e) { File.WriteAllText("Logs/cubos-setup.txt", "FAIL: "+e); Debug.LogException(e); }
    }

    [MenuItem("Tools/Mapa/Usar os seis cubos como bloqueios da floresta")]
    public static void Configurar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var cena=SceneManager.GetSceneByPath(ProgressoJogo.CenaJogo);
        if (!cena.IsValid() || !cena.isLoaded) throw new Exception("Abra Demo_Day para configurar os cubos.");
        var nomes=new[]{"Cube","Cube (1)","Cube (2)","Cube (3)","Cube (4)","Cube (5)"};
        var cubos=nomes.Select(nome=>cena.GetRootGameObjects().Single(g=>g.name==nome)).ToArray();
        if(cubos.Any(g=>g.GetComponent<BoxCollider>()==null)) throw new Exception("Um dos guias não tem BoxCollider.");
        EditorSceneManager.SaveScene(cena,"Logs/antes-cubos-floresta-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true);
        foreach(var cubo in cubos)
            if(cubo.GetComponent<BloqueioFloresta>()==null) Undo.AddComponent<BloqueioFloresta>(cubo);
        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        File.WriteAllText("Logs/cubos-setup.txt","PASS: Seis cubos ligados à leitura do diário; posição, rotação e escala preservadas.\n"+
            string.Join("\n",cubos.Select(c=>c.name+" @ "+c.transform.position)));
    }
}
