using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CavernaLuzVidaValidation
{
    static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    static string report;

    [MenuItem("Tools/Caverna/Validar lanterna e regeneracao")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var setup = EditorSceneManager.GetSceneManagerSetup();
        GameObject player = null;
        Material material = null;
        try
        {
            report = "";
            var shader = Resources.Load<Shader>("FeixeLanterna");
            Check(shader != null && !ShaderUtil.ShaderHasError(shader), "Shader transparente incluido em Resources e sem erros");
            material = new Material(shader);
            Check(material.renderQueue == 3000 && material.GetTag("RenderType", false) == "Transparent",
                "Feixe usa fila transparente sem keywords de variantes");
            Check(Mathf.Approximately(material.GetColor("_BaseColor").a, .06f), "Alpha padrao do feixe preservado");

            EditorSceneManager.OpenScene(ProgressoJogo.CenaCaverna);
            player = new GameObject("Validacao isolada da vida");
            player.SetActive(false);
            var effect = player.AddComponent<VidaEndy>();
            var life = player.AddComponent<VidaJogadorKraken>();
            life.RestaurarVida(100);
            life.ConfigurarAvisoVidaBaixa(34);
            life.ReceberDano(34, true);
            Check(!effect.VidaBaixa, "Primeiro golpe deixa 66 pontos sem alerta critico");
            life.ReceberDano(34, true);
            Check(life.VidaAtual == 32 && effect.VidaBaixa,
                "Dois golpes do Kraken ativam bordas antes do proximo golpe fatal");
            life.RecuperarDano(34);
            Check(!effect.VidaBaixa, "Cura do pilar encerra o alerta critico");
            life.RestaurarVida(40);
            Check(life.ReceberDano(20, true), "Dano do Kraken aceito");
            Check(effect.telaVermelha != null && effect.telaVermelha.color.a > 0f &&
                !effect.telaVermelha.raycastTarget, "Dano cria flash vermelho sem bloquear cliques");
            Check(effect.VidaBaixa && Mathf.Approximately(effect.FracaoVida, .2f), "Vinte por cento ativa as bordas de perigo");
            life.RestaurarVida(100);
            Check(!effect.VidaBaixa, "Recuperar a vida remove estado critico");
            life.RestaurarVida(50);
            Advance(life, 4.9f);
            Check(life.VidaAtual == 50, "Caverna espera cinco segundos sem dano");
            Advance(life, .11f);
            Check(life.VidaAtual == 60, "Caverna recupera dez pontos apos a espera");
            Advance(life, 0f);
            Check(life.VidaAtual == 60, "Pausa nao avanca a cura");
            Advance(life, 1f);
            Check(life.VidaAtual == 70, "Recupera mais dez pontos por segundo");
            Check(life.ReceberDano(10, true), "Dano aceito durante recuperacao");
            Advance(life, 4.9f);
            Check(life.VidaAtual == 60, "Novo dano reinicia a espera");
            Advance(life, .11f);
            Check(life.VidaAtual == 70, "Cura retoma depois da nova espera");
            Advance(life, 100f);
            Check(life.VidaAtual == 100 && Mathf.Approximately(effect.FracaoVida, 1f), "Cura limitada ao maximo e sincronizada com HUD");
            life.RestaurarVida(50);
            typeof(VidaJogadorKraken).GetProperty("Morto").GetSetMethod(true).Invoke(life, new object[] { true });
            Advance(life, 100f);
            Check(life.VidaAtual == 50, "Morte impede regeneracao");
            typeof(VidaJogadorKraken).GetProperty("Morto").GetSetMethod(true).Invoke(life, new object[] { false });
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            Advance(life, 100f);
            Check(life.VidaAtual == 50, "Fora da caverna nao existe regeneracao automatica");
            report += "PASS: all cave flashlight and health checks\n";
        }
        catch (Exception error)
        {
            report += "FAIL: " + error + "\n";
            Debug.LogException(error);
            throw;
        }
        finally
        {
            if (player) UnityEngine.Object.DestroyImmediate(player);
            if (material) UnityEngine.Object.DestroyImmediate(material);
            EditorSceneManager.RestoreSceneManagerSetup(setup);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/caverna-luz-vida-validation.txt", report);
        }
    }

    static void Advance(VidaJogadorKraken life, float seconds)
    {
        typeof(VidaJogadorKraken).GetMethod("AtualizarRecuperacao", PrivateInstance).Invoke(life, new object[] { seconds });
    }
    static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        report += "PASS: " + message + "\n";
    }
}
