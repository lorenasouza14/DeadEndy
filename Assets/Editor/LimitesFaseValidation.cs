using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class LimitesFaseValidation
{
    static LimitesFaseValidation() { EditorApplication.update += Pendente; }
    static void Pendente()
    {
        if (!File.Exists("Logs/validar-limites-pendente.txt") || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete("Logs/validar-limites-pendente.txt"); Validar();
    }
    static void Invocar(object alvo, string nome) => alvo.GetType().GetMethod(nome,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(alvo,null);
    static void Check(bool ok,string mensagem,StringBuilder report)
    { if(!ok) throw new Exception(mensagem); report.AppendLine("PASS: "+mensagem); }

    [MenuItem("Tools/Mapa/Validar limites da primeira fase")]
    public static void Validar()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        var cena=SceneManager.GetSceneByPath(ProgressoJogo.CenaJogo);
        var fontes=cena.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BloqueioFloresta>(true)).ToArray();
        var anterior=SceneManager.GetActiveScene();
        var teste=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        var report=new StringBuilder(); float tempo=Time.timeScale;
        var cursor=Cursor.lockState; bool cursorVisivel=Cursor.visible;
        try
        {
            Check(fontes.Length>6,"Guias originais e vedacoes complementares configurados",report);
            var guias=fontes.Select(f=>{
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=f.name;
                // Isola as provas fisicas dos colliders da cena real, sem alterar a cena do usuario.
                go.transform.SetPositionAndRotation(f.transform.position+Vector3.up*2000,f.transform.rotation);
                go.transform.localScale=f.transform.lossyScale;
                var origem=f.GetComponent<BoxCollider>(); var col=go.GetComponent<BoxCollider>(); col.center=origem.center; col.size=origem.size;
                var guia=go.AddComponent<BloqueioFloresta>(); guia.margemVertical=f.margemVertical;
                return guia;
            }).ToArray();
            var medidas=guias.Select(g=>g.GetComponent<BoxCollider>().size).ToArray();
            var player=new GameObject("Endy teste");
            var inventario=player.AddComponent<InventarioEndy>();
            var diario=player.AddComponent<DiarioEndy>(); Invocar(diario,"Awake");
            var cc=player.AddComponent<CharacterController>(); cc.height=1.8f;cc.radius=.28f;cc.center=Vector3.up*.9f;
            var tochas=new GameObject("Tochas teste").AddComponent<GerenciadorPuzzleTriangulo>();
            tochas.fogoDasTochas=new[]{new GameObject("Fogo1"),new GameObject("Fogo2"),new GameObject("Fogo3")}; Invocar(tochas,"Start");
            var limites=new GameObject("Limites teste").AddComponent<LimitesPrimeiraFase>();
            limites.endy=inventario;limites.tochas=tochas;Invocar(limites,"Awake");Physics.SyncTransforms();
            RotasVedacaoValidation.Validar(cena,limites.areaBaus,report);
            Check(limites.GetComponentsInChildren<BoxCollider>().Length==limites.areaBaus.Length,"Contorno dos baus acompanha a nova entrada",report);
            Check(!limites.PosicaoBloqueada(new Vector3(0,5,130)),"Ponto fora dos cubos nao e mais tratado como area proibida antiga",report);
            Check(!limites.BausLiberados&&!limites.FlorestaLiberada,"Inicio mantem regras das tochas e do diario",report);
            for(int i=0;i<guias.Length;i++)
            {
                var guia=guias[i];var col=guia.GetComponent<BoxCollider>();var antes=col.size;guia.Preparar();
                Check(col.size==antes&&col.size.x==medidas[i].x&&col.size.z==medidas[i].z,"Preparo idempotente preserva largura e espessura: "+guia.name,report);
                Check(guia.GetComponent<Renderer>()==null || !guia.GetComponent<Renderer>().enabled,"Cubo invisivel na partida: "+guia.name,report);
                foreach(float escala in new[]{1f,.4f})
                foreach(float trecho in new[]{-.48f,0f,.48f})
                {
                    Vector3 centro=guia.transform.TransformPoint(col.center+Vector3.right*col.size.x*trecho);var normal=guia.transform.forward;
                    cc.enabled=false;player.transform.localScale=Vector3.one*escala;player.transform.position=centro+normal*3;
                    Physics.SyncTransforms();
                    var baseCapsula=player.transform.position+Vector3.up*cc.radius*escala;
                    var topoCapsula=player.transform.position+Vector3.up*(cc.height-cc.radius)*escala;
                    Check(Physics.CapsuleCast(baseCapsula,topoCapsula,cc.radius*escala,-normal,out _,6,~0,QueryTriggerInteraction.Ignore),
                        "Capsula normal/encolhida "+escala+" interceptada no centro/pontas "+trecho+": "+guia.name,report);
                    cc.enabled=true;cc.Move(-normal*6);
                    // Nas quinas, Move pode deslizar pela parede vizinha: a projeção na normal
                    // da primeira parede não descreve mais o lado permitido. Testa-a no centro.
                    if(trecho==0) Check(Vector3.Dot(player.transform.position-centro,normal)>.4f,"Controller bloqueado: "+guia.name+" escala "+escala,report);
                }
            }
            tochas.AcenderParteDoTriangulo(0);tochas.AcenderParteDoTriangulo(1);limites.AtualizarBloqueios();
            Check(!limites.BausLiberados,"Duas tochas nao liberam os baus",report);
            tochas.AcenderParteDoTriangulo(2);limites.AtualizarBloqueios();
            Check(limites.BausLiberados&&guias.All(g=>g.GetComponent<BoxCollider>().enabled),"Tres tochas liberam baus e preservam bloqueios do mato",report);
            inventario.temDiario=true;limites.AtualizarBloqueios();
            Check(!limites.FlorestaLiberada,"Coletar sem ler ainda bloqueia",report);
            Time.timeScale=1;Check(diario.Abrir()&&diario.FoiLido,"Abrir diario registra leitura",report);diario.Fechar();limites.AtualizarBloqueios();
            foreach(var guia in guias)
            {
                var col=guia.GetComponent<BoxCollider>();var centro=guia.transform.TransformPoint(col.center);var normal=guia.transform.forward;
                Check(!col.enabled,"Leitura desliga collider do guia: "+guia.name,report);
                cc.enabled=false;player.transform.localScale=Vector3.one;player.transform.position=centro+normal*3;cc.enabled=true;
                Physics.SyncTransforms();cc.Move(-normal*6);
                Check(Vector3.Dot(player.transform.position-centro,normal)<-2,"Passagem liberada fisicamente: "+guia.name,report);
            }
            diario.RestaurarLeitura(false);limites.AtualizarBloqueios();Check(guias.All(g=>g.GetComponent<BoxCollider>().enabled),"Save sem leitura mantem todos os bloqueios",report);
            diario.RestaurarLeitura(true);limites.AtualizarBloqueios();Check(guias.All(g=>!g.GetComponent<BoxCollider>().enabled),"Continuar com diario lido libera todos os guias",report);
        }
        catch(Exception e){report.AppendLine("FAIL: "+e);Debug.LogException(e);}
        finally
        {
            EditorSceneManager.CloseScene(teste,true);SceneManager.SetActiveScene(anterior);
            Time.timeScale=tempo;Cursor.lockState=cursor;Cursor.visible=cursorVisivel;
            File.WriteAllText("Logs/limites-fase-validation.txt",report.ToString());
        }
    }
}
