using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using StarterAssets;

[InitializeOnLoad]
public static class SabaoDebuffCheck
{
    const string Report = @"C:\Users\SUPORTE\Documents\Codex\2026-11-19\cod\work\sabao-forte\validation.txt";
    static SabaoDebuffCheck() { EditorApplication.delayCall += Run; }
    [MenuItem("Tools/Sabao/Testar espuma e deslizamento")]
    static void Preview()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("Entre no Play para testar o sabao."); return; }
        var player = UnityEngine.Object.FindAnyObjectByType<ThirdPersonController>();
        if (player) { player.AplicarSabao(30f); Debug.Log("Sabao aplicado por 30 segundos para testar espuma e deslizamento."); }
    }
    static void Check(bool ok, string text) { if(!ok) throw new Exception(text); File.AppendAllText(Report,"PASS: "+text+"\n"); }
    static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(Report)) return;
        GameObject go = null;
        try
        {
            var method=typeof(ThirdPersonController).GetMethod("AplicarInerciaSabao",BindingFlags.Static|BindingFlags.NonPublic);
            Func<Vector3,Vector3,float,Vector3> filter=(a,b,t)=>(Vector3)method.Invoke(null,new object[]{a,b,t});
            var initial=new Vector3(6,9,0);
            var stopped=filter(initial,Vector3.zero,1f/60);
            Check(stopped.x>5 && stopped.x<6 && stopped.y==0,"Soltar movimento conserva deslizamento horizontal, sem alterar gravidade");
            var turned=filter(initial,Vector3.left*6,1f/60);
            Check(turned.x>0,"Inverter direcao nao inverte velocidade instantaneamente");
            var a=initial; var b=initial;
            for(int i=0;i<30;i++)a=filter(a,Vector3.zero,1f/30);
            for(int i=0;i<120;i++)b=filter(b,Vector3.zero,1f/120);
            Check(Vector3.Distance(a,b)<.001f,"Frenagem equivalente a 30 e 120 FPS");
            Check(a.magnitude>4.7f && a.magnitude<4.9f,"Frenagem forte conserva cerca de 80% da velocidade apos um segundo");
            for(int i=0;i<1200;i++)a=filter(a,Vector3.zero,1f/60);
            Check(a.magnitude<.1f,"Deslizamento ainda desacelera ate parar");
            go=new GameObject("Sabao check"); go.SetActive(false);
            var controller=go.AddComponent<ThirdPersonController>();
            controller.AplicarSabao(10);
            Check(controller.Escorregadio && Mathf.Abs(controller.TempoRestanteSabao-10)<.1f,"Bolha inicia 10 segundos de sabao");
            var end=typeof(ThirdPersonController).GetField("ensaboadoAte",BindingFlags.Instance|BindingFlags.NonPublic);
            end.SetValue(controller,Time.time+4f);
            Check(controller.Escorregadio,"Continua escorregadio apos 6 segundos, alem dos 5 segundos visuais");
            controller.AplicarSabao(10);
            Check(Mathf.Abs(controller.TempoRestanteSabao-10)<.1f,"Nova bolha renova para 10 segundos, sem somar 4+10");
            end.SetValue(controller,Time.time-.1f);
            Check(!controller.Escorregadio && controller.TempoRestanteSabao==0,"Efeito termina apos o prazo");
            controller.AplicarSabao(10); controller.LimparSabao();
            Check(!controller.Escorregadio,"Cancelamento limpa o debuff");
            controller.AplicarSabao(10);
            typeof(ThirdPersonController).GetField("cenaDoSabao",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(controller,default(UnityEngine.SceneManagement.Scene));
            Check(!controller.Escorregadio,"Efeito nao passa para outra cena");
            Check(File.ReadAllText(@"C:\game-deadendy-juncao\DeadEndy\Assets\Scenes\Recuperadas\Level_Kraken_4.unity").Contains("duracaoEmbacamento: 5"),"Cena configurada com 5 segundos visuais");
        }
        catch(Exception e) { File.AppendAllText(Report,"FAIL: "+e); }
        finally { if(go) UnityEngine.Object.DestroyImmediate(go); }
    }
}
