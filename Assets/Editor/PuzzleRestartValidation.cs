using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PuzzleRestartValidation
{
    static PuzzleRestartValidation() { EditorApplication.playModeStateChanged += Changed; }

    [MenuItem("Tools/Interacao/Configurar restart e testar dificuldade")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var life = Object.FindAnyObjectByType<VidaEndy>();
        if (life == null || life.telaGameOverUI == null) throw new Exception("Tela de game over não encontrada.");
        var panel = life.telaGameOverUI;
        var buttons = panel.GetComponentsInChildren<Button>(true);
        var button = buttons.FirstOrDefault(b => b.name == "ButtonRestart");
        if (button == null) throw new Exception("ButtonRestart não encontrado no game over.");
        Directory.CreateDirectory("Logs");
        var scene = life.gameObject.scene;
        EditorSceneManager.SaveScene(scene, "Logs/puzzle-restart-before.unity", true);
        Undo.RegisterFullObjectHierarchyUndo(panel, "Configurar restart");
        var manager = panel.GetComponent<GerenciadorGameOver>();
        if (manager == null) manager = Undo.AddComponent<GerenciadorGameOver>(panel);
        manager.enabled = true;
        foreach (var old in Object.FindObjectsByType<GerenciadorGameOver>(FindObjectsInactive.Include))
        {
            if (old == manager || old.gameObject.scene != scene) continue;
            Undo.RecordObject(old, "Evitar controle duplicado do cursor");
            old.enabled = false;
        }
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            if (button.onClick.GetPersistentMethodName(i) == "ReiniciarJogo") UnityEventTools.RemovePersistentListener(button.onClick, i);
        UnityEventTools.AddPersistentListener(button.onClick, manager.ReiniciarJogo);
        button.enabled = true;
        button.interactable = true;
        if (button.targetGraphic != null) button.targetGraphic.raycastTarget = true;
        var canvas = button.GetComponentInParent<Canvas>(true);
        var raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster == null) raycaster = Undo.AddComponent<GraphicRaycaster>(canvas.gameObject);
        raycaster.enabled = true;
        EditorUtility.SetDirty(button);
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Não foi possível salvar a cena.");
        SessionState.SetBool("PuzzleRestartValidation.Pending", true);
        EditorApplication.EnterPlaymode();
    }
    static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("PuzzleRestartValidation.Pending", false)) return;
        SessionState.SetBool("PuzzleRestartValidation.Pending", false);
        new GameObject("Teste de dificuldade e restart").AddComponent<PuzzleRestartRunner>().Run();
    }
}

public class PuzzleRestartRunner : MonoBehaviour
{
    string report = "";
    bool loaded;
    public void Run()
    {
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += Loaded;
        StartCoroutine(Guard());
    }
    void Loaded(Scene scene, LoadSceneMode mode) { loaded = true; }
    void OnDestroy() { SceneManager.sceneLoaded -= Loaded; }
    void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        report += "PASS: " + message + "\n";
        File.WriteAllText("Logs/puzzle-restart-validation.txt", report);
    }
    IEnumerator Guard()
    {
        var checks = Checks();
        while (true)
        {
            object wait;
            try { if (!checks.MoveNext()) { EditorApplication.ExitPlaymode(); yield break; } wait = checks.Current; }
            catch (Exception error)
            {
                File.WriteAllText("Logs/puzzle-restart-validation.txt", report + "FAIL: " + error);
                Debug.LogException(error);
                EditorApplication.ExitPlaymode();
                yield break;
            }
            yield return wait;
        }
    }
    IEnumerator Checks()
    {
        yield return null;
        var life = FindAnyObjectByType<VidaEndy>();
        var player = life.GetComponentInParent<StarterAssets.ThirdPersonController>();
        if (player == null) player = life.GetComponent<StarterAssets.ThirdPersonController>();
        Check(!life.telaGameOverUI.activeInHierarchy, "Game over começa oculto");
        foreach (var boss in FindObjectsByType<CaranguejoChefe>(FindObjectsInactive.Include)) boss.enabled = false;
        foreach (var chest in FindObjectsByType<InteracaoBau>())
        {
            Check(chest.isActiveAndEnabled, "Baú ativo: " + chest.transform.parent.name);
            float width = chest.zonaVerdeMax - chest.zonaVerdeMin;
            float speed = chest.velocidadePonteiro;
            var zone = chest.barraPuzzle.GetComponentsInChildren<RectTransform>(true).First(t => t.name == "Zona de acerto" || t.name == "MarcadorVerde");
            chest.SendMessage("IniciarPuzzle");
            yield return new WaitForSeconds(0.25f);
            for (int n = 1; n < chest.trancasParaAbrir; n++)
            {
                chest.barraPuzzle.value = (chest.zonaVerdeMin + chest.zonaVerdeMax) * 0.5f;
                chest.SendMessage("VerificarAcerto");
                float expected = Mathf.Clamp(width * Mathf.Pow(chest.fatorLarguraPorAcerto, n), Mathf.Min(width, chest.larguraMinimaZona), width);
                Check(Mathf.Abs(chest.zonaVerdeMax - chest.zonaVerdeMin - expected) < 0.0001f &&
                    Mathf.Abs(zone.anchorMax.x - zone.anchorMin.x - expected) < 0.0001f &&
                    Mathf.Approximately(chest.velocidadePonteiro, speed + n * chest.aumentoVelocidadePorAcerto),
                    chest.transform.parent.name + " acerto " + n + ": faixa=" + expected + ", velocidade=" + chest.velocidadePonteiro);
                yield return new WaitForSeconds(0.32f);
            }
            chest.barraPuzzle.value = 0;
            chest.SendMessage("VerificarAcerto");
            Check(!chest.FazendoPuzzle && Mathf.Approximately(chest.velocidadePonteiro, speed) &&
                Mathf.Abs(chest.zonaVerdeMax - chest.zonaVerdeMin - width) < 0.0001f, "Erro restaura a dificuldade inicial");
        }
        life.ForcarDerrota();
        yield return new WaitForSecondsRealtime(0.5f);
        var button = life.telaGameOverUI.GetComponentsInChildren<Button>(true).First(b => b.name == "ButtonRestart");
        Check(Cursor.lockState == CursorLockMode.None && Cursor.visible && button.IsInteractable(), "Game over libera mouse e botão");
        var center = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
        var hits = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = center }, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button, "Botão recebe clique, sem imagem bloqueando");
        report += "AGUARDANDO CLIQUE REAL NO RESTART\n";
        File.WriteAllText("Logs/puzzle-restart-validation.txt", report);
        float limit = Time.realtimeSinceStartup + 180;
        while (!loaded && Time.realtimeSinceStartup < limit) yield return null;
        Check(loaded, "Clique do botão recarregou a cena");
        yield return null;
        life = FindAnyObjectByType<VidaEndy>();
        Check(life != null && !life.EstaMorto && life.scriptDeMovimento.enabled && life.controleDeColisao.enabled &&
            !life.telaGameOverUI.activeInHierarchy && Time.timeScale == 1, "Restart restaura vida, movimento e fecha game over");
        var restartedChest = FindObjectsByType<InteracaoBau>().First();
        restartedChest.SendMessage("IniciarPuzzle");
        Check(restartedChest.chefe.gameObject.activeInHierarchy && restartedChest.chefe.estadoAtual == CaranguejoChefe.EstadoChefe.Perseguindo,
            "Nova partida volta a acordar o chefe pelo baú");
        Debug.Log("PASS: dificuldade progressiva e restart real.");
    }
}
