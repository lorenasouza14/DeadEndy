using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class VedacaoFlorestaSetup
{
    static VedacaoFlorestaSetup() { EditorApplication.update += Pendente; }
    static void Pendente()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists("Logs/vedar-floresta-pendente.txt")) return;
        File.Delete("Logs/vedar-floresta-pendente.txt");
        try { Configurar(); } catch(Exception e) { File.WriteAllText("Logs/vedacao-setup.txt","FAIL: "+e); Debug.LogException(e); }
    }
    [MenuItem("Tools/Mapa/Vedar passagens entre os cubos da floresta")]
    public static void Configurar()
    {
        var cena=SceneManager.GetSceneByPath(ProgressoJogo.CenaJogo);
        if (!cena.IsValid() || !cena.isLoaded || EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Abra Demo_Day fora do Play Mode.");
        var raizes=cena.GetRootGameObjects();
        var guias=Enumerable.Range(0,6).Select(i=>raizes.Single(g=>g.name==(i==0?"Cube":"Cube ("+i+")")).GetComponent<BloqueioFloresta>()).ToArray();
        if(guias.Any(g=>g==null)) throw new Exception("Os seis cubos-guia precisam estar configurados.");
        EditorSceneManager.SaveScene(cena,"Logs/antes-vedacao-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true);
        var raiz=raizes.FirstOrDefault(g=>g.name=="Vedacao complementar da floresta");
        if(raiz==null) { raiz=new GameObject("Vedacao complementar da floresta"); SceneManager.MoveGameObjectToScene(raiz,cena); Undo.RegisterCreatedObjectUndo(raiz,"Vedar floresta"); }
        int indice=0;
        Vector2 Ponta(int guia,int lado)
        {
            var col=guias[guia].GetComponent<BoxCollider>();
            var p=col.transform.TransformPoint(col.center+Vector3.right*col.size.x*.5f*lado);
            return new Vector2(p.x,p.z);
        }
        void Linha(string nome,params Vector2[] pontos)
        {
            for(int i=0;i<pontos.Length-1;i++)
            {
                string id="Vedacao "+(++indice).ToString("00")+" - "+nome;
                var filho=raiz.transform.Find(id);
                var go=filho!=null?filho.gameObject:new GameObject(id);
                if(filho==null) {go.transform.SetParent(raiz.transform,false);Undo.RegisterCreatedObjectUndo(go,"Vedar brecha");}
                Undo.RecordObject(go.transform,"Ajustar vedacao");
                var a=pontos[i];var b=pontos[i+1];var delta=b-a;
                go.transform.position=new Vector3((a.x+b.x)*.5f,20,(a.y+b.y)*.5f);
                go.transform.rotation=Quaternion.LookRotation(new Vector3(-delta.y,0,delta.x));
                go.transform.localScale=new Vector3(delta.magnitude+1.2f,80,1.2f);
                if(go.GetComponent<BoxCollider>()==null) Undo.AddComponent<BoxCollider>(go);
                if(go.GetComponent<BloqueioFloresta>()==null) Undo.AddComponent<BloqueioFloresta>(go);
            }
        }
        Linha("encosta sul",Ponta(0,-1),new Vector2(20,56),new Vector2(5,57),new Vector2(-10,65),new Vector2(-20,78),Ponta(2,1));
        // A reentrancia acompanha o lago; preserva o percurso por terra ao redor da agua.
        Linha("margem do lago",Ponta(2,-1),new Vector2(0,101),new Vector2(14,105),new Vector2(14,119),new Vector2(0,125),new Vector2(-35,123),Ponta(3,1));
        Linha("encosta oeste",Ponta(3,-1),new Vector2(-55,150),new Vector2(-52,161),new Vector2(-42,175),new Vector2(-36,185),new Vector2(-30,195),Ponta(4,1));
        Linha("juncao norte",Ponta(4,-1),Ponta(5,1));
        Linha("encosta leste",Ponta(5,-1),new Vector2(22,150),new Vector2(30,138),new Vector2(45,128),new Vector2(55,125),new Vector2(68,110),new Vector2(78,92),new Vector2(82,70),new Vector2(90,50),Ponta(1,1));
        Linha("costas da alavanca sul",Ponta(1,-1),new Vector2(67,38),new Vector2(61,45),new Vector2(54,50),new Vector2(44,54),Ponta(0,1));
        EditorSceneManager.MarkSceneDirty(cena);EditorSceneManager.SaveScene(cena);
        File.WriteAllText("Logs/vedacao-setup.txt","PASS: "+indice+" trechos complementares, unidos aos seis cubos existentes.");
    }
}
