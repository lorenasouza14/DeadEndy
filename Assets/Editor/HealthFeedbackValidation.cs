using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HealthFeedbackValidation
{
    const string Pending = "HealthFeedbackValidation.Pending";
    static HealthFeedbackValidation() { EditorApplication.playModeStateChanged += Changed; }
    [MenuItem("Tools/Player/Validar vida baixa e recuperacao")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }
    static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        new GameObject("Health feedback validation").AddComponent<HealthFeedbackValidationRunner>().Run();
    }
}

public class HealthFeedbackValidationRunner : MonoBehaviour
{
    string report = "";
    public void Run() { StartCoroutine(Guard()); }
    IEnumerator Guard()
    {
        var checks = Checks();
        while (true)
        {
            object wait;
            try
            {
                if (!checks.MoveNext()) { Finish(null); yield break; }
                wait = checks.Current;
            }
            catch (Exception error) { Finish(error); yield break; }
            yield return wait;
        }
    }
    void Finish(Exception error)
    {
        Time.timeScale = 1;
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/health-feedback-validation.txt", report + (error == null ? "PASS: all health checks" : "FAIL: " + error));
        if (error != null) Debug.LogException(error);
        EditorApplication.ExitPlaymode();
    }
    void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        report += "PASS: " + message + "\n";
    }
    static void Advance(VidaEndy life, float seconds)
    {
        typeof(VidaEndy).GetMethod("AtualizarRecuperacao", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(life, new object[] { seconds });
    }
    IEnumerator Checks()
    {
        yield return null;
        foreach (var boss in Object.FindObjectsByType<CaranguejoChefe>(FindObjectsInactive.Include)) boss.enabled = false;
        foreach (var original in Object.FindObjectsByType<VidaEndy>(FindObjectsInactive.Include)) original.enabled = false;
        foreach (var move in Object.FindObjectsByType<StarterAssets.ThirdPersonController>()) move.enabled = false;

        var player = new GameObject("Health test Endy");
        player.SetActive(false);
        var life = player.AddComponent<VidaEndy>();
        life.vidaMaxima = 10;
        var flashObject = new GameObject("Test damage flash", typeof(RectTransform), typeof(Image));
        flashObject.transform.SetParent(player.transform, false);
        life.telaVermelha = flashObject.GetComponent<Image>();
        life.telaGameOverUI = new GameObject("Test game over");
        life.telaGameOverUI.SetActive(false);
        player.SetActive(true);
        yield return null;
        Check(life.VidaAtual == 10 && !life.VidaBaixa, "Comeca com vida maxima configurada");
        var indicator = player.GetComponent<IndicadorVidaEndy>();
        Check(indicator != null && indicator.Visivel, "Borda de perigo criada automaticamente");
        foreach (var graphic in player.GetComponentsInChildren<Graphic>()) Check(!graphic.raycastTarget, "Interface nao intercepta cliques: " + graphic.name);
        for (int i = 0; i < 7; i++) life.TomarDano();
        Check(life.VidaAtual == 3 && life.VidaBaixa && !life.EstaRegenerando, "30% ativa alerta sem curar durante golpes");
        yield return new WaitForSeconds(0.65f);
        Time.timeScale = 0;
        int beforePause = life.VidaAtual;
        yield return new WaitForSecondsRealtime(0.25f);
        Check(life.VidaAtual == beforePause, "Pausa nao recupera vida");
        yield return new WaitForEndOfFrame();
        Directory.CreateDirectory("Logs");
        ScreenCapture.CaptureScreenshot("Logs/health-critical-preview.png");
        yield return null;
        Time.timeScale = 1;
        life.TomarDano();
        Advance(life, 4.9f);
        Check(life.VidaAtual == 2, "Novo golpe exige novamente cinco segundos sem dano");
        Advance(life, 0.11f);
        Check(life.VidaAtual == 3 && life.EstaRegenerando, "Primeiro ponto recuperado apos a espera");
        life.TomarDano();
        Check(!life.EstaRegenerando && life.VidaAtual == 2, "Dano interrompe regeneracao imediatamente");
        Advance(life, 4.9f);
        Check(life.VidaAtual == 2, "Regeneracao interrompida reinicia a espera completa");
        Advance(life, 0.11f);
        Advance(life, 1f);
        Check(life.VidaAtual == 4 && !life.VidaBaixa, "Recupera um ponto por segundo e sai da vida critica");
        yield return new WaitForSeconds(0.7f);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot("Logs/health-recovery-preview.png");
        yield return null;
        Advance(life, 100f);
        Check(life.VidaAtual == 10 && !life.EstaRegenerando, "Cura limitada ao maximo mesmo com quadro longo");
        life.regenerarVida = false;
        life.TomarDano();
        Advance(life, 100f);
        Check(life.VidaAtual == 9, "Opcao de desativar recuperacao respeitada");
        life.regenerarVida = true;
        for (int i = 0; i < 9; i++) life.TomarDano();
        Check(life.EstaMorto && life.VidaAtual == 0 && life.telaGameOverUI.activeSelf, "Dano fatal preserva game over");
        Advance(life, 100f);
        life.TomarDano();
        Check(life.VidaAtual == 0 && !life.EstaRegenerando && !indicator.Visivel && life.telaVermelha.color.a == 0f,
            "Morte nao regenera, nao deixa vida negativa e oculta efeitos");
        Object.Destroy(player);
        yield return null;
    }
}
