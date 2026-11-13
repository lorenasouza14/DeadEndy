using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class DanoQuedaValidation
{
    const string Chave = "DanoQuedaValidation.Ativo";
    const string Relatorio = "Logs/dano-queda-validation.txt";
    const BindingFlags Privado = BindingFlags.NonPublic | BindingFlags.Instance;
    static Stack<IEnumerator> rotinas;
    static ThirdPersonController jogador;
    static CharacterController capsula;
    static VidaEndy vida;
    static StarterAssetsInputs entrada;
    static Vector3 repouso;
    static int frame;
    static float esperar;
    static double esperarReal;
    static DanoQuedaValidation() { EditorApplication.update += Atualizar; }

    [MenuItem("Tools/Player/Configurar dano de queda na fase %#&q")]
    public static void Configurar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var cena = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var player = UnityEngine.Object.FindObjectsByType<ThirdPersonController>().Single(x => x.gameObject.scene == cena);
        Undo.RecordObject(player,"Configurar dano de queda");
        player.receberDanoDeQueda=true; player.alturaSeguraQueda=12; player.danoPorUnidadeQueda=0.25f;
        PrefabUtility.RecordPrefabInstancePropertyModifications(player); EditorUtility.SetDirty(player);
        EditorSceneManager.MarkSceneDirty(cena); EditorSceneManager.SaveScene(cena);
        Selection.activeGameObject=player.gameObject;
        Debug.Log("Dano de queda configurado: altura segura 12, dano por unidade excedente 0,25. Cena salva.");
    }
    [MenuItem("Tools/Player/Validar dano de queda %#&w")]
    public static void Testar()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        Directory.CreateDirectory("Logs"); File.WriteAllText(Relatorio,"Quedas com Endy real e CharacterController em Play Mode.\n");
        rotinas=null; SessionState.SetBool(Chave,true); EditorApplication.isPlaying=true;
    }
    static void Exigir(bool ok,string s) { if(!ok) throw new Exception(s); File.AppendAllText(Relatorio,"PASS: "+s+"\n"); }
    static void Atualizar()
    {
        if(!SessionState.GetBool(Chave,false) || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
        Application.runInBackground=true;
        if(Time.time<1) return;
        try
        {
            if(rotinas==null) { rotinas=new Stack<IEnumerator>(); rotinas.Push(Executar()); esperar=0; esperarReal=0; frame=-1; }
            if(frame==Time.frameCount || Time.time<esperar || EditorApplication.timeSinceStartup<esperarReal) return;
            frame=Time.frameCount;
            while(rotinas.Count>0)
            {
                var r=rotinas.Peek(); if(!r.MoveNext()) { rotinas.Pop(); continue; }
                if(r.Current is IEnumerator filho) { rotinas.Push(filho); continue; }
                if(r.Current is float segundos) esperar=Time.time+segundos;
                return;
            }
            Finalizar(null);
        } catch(Exception e) { Finalizar(e); }
    }
    static void Finalizar(Exception erro)
    {
        SessionState.SetBool(Chave,false); Time.timeScale=1;
        if(jogador!=null) jogador.GameplayPaused=false;
        File.AppendAllText(Relatorio,erro==null?"PASS: todas as verificacoes de queda.\n":"FAIL: "+erro+"\n");
        if(erro!=null) Debug.LogException(erro); else Debug.Log("Dano de queda aprovado em Play Mode.");
        EditorApplication.isPlaying=false;
    }
    static void Posicionar(Vector3 p, bool reset=true)
    {
        capsula.enabled=false; jogador.transform.SetPositionAndRotation(p,Quaternion.identity); capsula.enabled=true;
        typeof(ThirdPersonController).GetMethod("CancelKnockback",Privado).Invoke(jogador,null);
        typeof(ThirdPersonController).GetField("_verticalVelocity",Privado).SetValue(jogador,0f);
        typeof(ThirdPersonController).GetField("_speed",Privado).SetValue(jogador,0f);
        entrada.move=Vector2.zero; entrada.jump=false; entrada.sprint=false;
        if(reset) jogador.ReiniciarMedicaoQueda(); Physics.SyncTransforms();
    }
    static void Curar() { typeof(VidaEndy).GetMethod("Awake",Privado).Invoke(vida,null); }
    static IEnumerator Pousar()
    {
        float fim=Time.time+12; yield return null;
        while(!capsula.isGrounded && !vida.EstaMorto && Time.time<fim) yield return null;
        Exigir(capsula.isGrounded || vida.EstaMorto,"Queda terminou em contato real com o piso.");
    }
    static IEnumerator Queda(float altura, bool pausa=false, bool arremesso=false)
    {
        Curar(); Posicionar(repouso+Vector3.up*altura);
        if(arremesso) jogador.ReceberArremesso(Vector3.forward,1f,0,0.5f,2f);
        yield return 0.15f;
        Exigir(vida.VidaAtual==vida.vidaMaxima,"Nenhum dano antes do pouso de "+altura+" unidades.");
        if(pausa)
        {
            Vector3 antes=jogador.transform.position; Time.timeScale=0; jogador.GameplayPaused=true;
            esperarReal=EditorApplication.timeSinceStartup+0.35; yield return null;
            Exigir(Vector3.Distance(antes,jogador.transform.position)<0.001f && vida.VidaAtual==vida.vidaMaxima,"Pausa no ar congela movimento e dano.");
            jogador.GameplayPaused=false; Time.timeScale=1;
        }
        yield return Pousar();
        float esperado=jogador.CalcularDanoQueda(altura);
        Exigir(Mathf.Abs(vida.VidaAtual-(vida.vidaMaxima-esperado))<0.12f,"Queda "+altura+": dano "+(vida.vidaMaxima-vida.VidaAtual).ToString("F3")+", esperado "+esperado+" (tolerancia da capsula 0,12).");
        float restante=vida.VidaAtual; yield return 0.6f;
        Exigir(vida.VidaAtual==restante,"Pouso aplica dano apenas uma vez.");
    }
    static IEnumerator Executar()
    {
        jogador=UnityEngine.Object.FindAnyObjectByType<ThirdPersonController>(); capsula=jogador.GetComponent<CharacterController>(); vida=jogador.GetComponent<VidaEndy>(); entrada=jogador.GetComponent<StarterAssetsInputs>();
        foreach(var g in UnityEngine.Object.FindObjectsByType<GorilaGuardiao>()) g.enabled=false;
        vida.regenerarVida=false;
        Exigir(jogador.receberDanoDeQueda && jogador.alturaSeguraQueda==12 && jogador.danoPorUnidadeQueda==0.25f,"Configuracao da fase: limite 12, dano por unidade 0,25.");
        Exigir(jogador.CalcularDanoQueda(12)==0 && jogador.CalcularDanoQueda(16)==1 && jogador.CalcularDanoQueda(24)==3 && jogador.CalcularDanoQueda(22)==2.5f,"Formula de dano e limite seguro exatos.");
        var piso=GameObject.CreatePrimitive(PrimitiveType.Cube); piso.name="Piso temporario de teste de queda"; piso.transform.position=new Vector3(0,999.5f,0); piso.transform.localScale=new Vector3(100,1,100);
        Posicionar(new Vector3(0,1000.3f,0)); yield return Pousar(); yield return 0.3f; repouso=jogador.transform.position;
        yield return Queda(10); yield return Queda(12); yield return Queda(16); yield return Queda(24,true); yield return Queda(22); yield return Queda(24,false,true);
        Curar(); Posicionar(repouso); yield return 0.6f; entrada.jump=true; yield return null; entrada.jump=false; yield return 1.6f;
        Exigir(vida.VidaAtual==vida.vidaMaxima,"Pulo normal nao causa dano.");
        jogador.receberDanoDeQueda=false; yield return Queda(24); jogador.receberDanoDeQueda=true;
        Curar(); Posicionar(repouso+Vector3.up*10); yield return 0.4f; Posicionar(repouso,false); yield return 0.3f;
        Exigir(vida.VidaAtual==vida.vidaMaxima,"Teleporte externo nao herda dano da queda interrompida.");
        Curar(); Posicionar(repouso); yield return 0.4f;
        Exigir(jogador.ReceberCatapulta(repouso+Vector3.forward*5,24),"Catapulta existente iniciou.");
        yield return 0.3f; yield return Pousar();
        Exigir(vida.VidaAtual<vida.vidaMaxima-2.5f && vida.VidaAtual>vida.vidaMaxima-3.5f,"Queda da catapulta usa o ponto mais alto do arco.");
        Curar(); Posicionar(repouso+Vector3.up*60); yield return Pousar();
        Exigir(vida.EstaMorto && vida.VidaAtual==0 && !jogador.enabled,"Queda fatal usa morte existente e desliga movimento.");
    }
}
