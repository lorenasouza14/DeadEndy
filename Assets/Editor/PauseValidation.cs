using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

[InitializeOnLoad]
public static class PauseValidation
{
    const string Pending = "PauseValidation.Pending";
    static PauseValidation() { EditorApplication.playModeStateChanged += Changed; }

    // Execute in a separate batch editor so the user's open scene is never replaced.
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Execute esta validacao em batchmode.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        new GameObject("Validacao pause").AddComponent<PauseValidationRunner>();
    }
}

public class PauseValidationRunner : MonoBehaviour
{
    string report = "";
    IEnumerator Start()
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
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/pause-validation.txt", report + (error == null ? "PASS: all pause checks\n" : "FAIL: " + error));
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }

    void Check(bool result, string description)
    {
        if (!result) throw new Exception(description);
        report += "PASS: " + description + "\n";
        Debug.Log("PauseValidation: " + description);
    }

    IEnumerator Checks()
    {
        var prefab = Resources.Load<GerenciadorPause>("MenuPause");
        Check(prefab != null && prefab.videoPause != null && prefab.fonte != null, "Prefab inclui video menu_pause e fonte do game over");
        Check(AssetDatabase.GetAssetPath(prefab.videoPause) == "Assets/Videos/menu_pause.mp4", "Video correto referenciado para o build");
        new GameObject("Camera", typeof(Camera));
        var player = new GameObject("Endy teste", typeof(StarterAssets.StarterAssetsInputs));
        var life = player.AddComponent<VidaEndy>();
        life.mostrarIndicadorVida = false;
        var inputs = player.GetComponent<StarterAssets.StarterAssetsInputs>();
        var movement = player.AddComponent<PauseValidationMovement>();
        life.scriptDeMovimento = movement;
        life.telaGameOverUI = new GameObject("GameOver teste");
        life.telaGameOverUI.SetActive(false);
        var templateObject = new GameObject("Restart", typeof(RectTransform), typeof(TextMeshProUGUI));
        templateObject.transform.SetParent(life.telaGameOverUI.transform);
        var template = templateObject.GetComponent<TextMeshProUGUI>();
        template.font = prefab.fonte;
        template.fontSize = 70f;
        yield return null;
        yield return null;
        var menu = UnityEngine.Object.FindAnyObjectByType<GerenciadorPause>();
        Check(menu != null && !menu.EstaPausado, "VidaEndy instala menu automaticamente, inicialmente fechado");
        var buttons = menu.GetComponentsInChildren<Button>(true);
        var resume = buttons.Single(b => b.name == "Retornar");
        var quit = buttons.Single(b => b.name == "Sair");
        Check(buttons.All(b => b.GetComponentInChildren<TMP_Text>().font == template.font && b.GetComponentInChildren<TMP_Text>().fontSize == 70), "Ambos os botoes preservam fonte e tamanho 70 do menu de morte");
        Time.timeScale = 0.6f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        inputs.cursorLocked = false;
        inputs.cursorInputForLook = true;
#if ENABLE_INPUT_SYSTEM
        var keyboard = InputSystem.AddDevice<Keyboard>();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
#else
        menu.Pausar();
#endif
        Check(menu.EstaPausado && Time.timeScale == 0f && AudioListener.pause, "Escape pausa mundo e audio");
        Check(!movement.enabled && inputs.gameplayInputBlocked && !inputs.cursorInputForLook, "Movimento e camera bloqueados");
        inputs.MoveInput(Vector2.one);
        inputs.LookInput(Vector2.one);
        inputs.JumpInput(true);
        Check(inputs.move == Vector2.zero && inputs.look == Vector2.zero && !inputs.jump, "Inputs durante o pause nao ficam acumulados");
        float health = life.VidaAtual;
        life.TomarDano();
        Check(life.VidaAtual == health, "Pause impede dano");
        float gameTime = Time.time;
        var video = menu.GetComponentInChildren<VideoPlayer>(true);
        float deadline = Time.realtimeSinceStartup + 30f;
        while (!video.isPlaying && Time.realtimeSinceStartup < deadline) yield return null;
        Check(video.isPlaying && video.texture != null && video.isLooping, "Video prepara e reproduz em loop durante pause");
        double videoTime = video.time;
        yield return new WaitForSecondsRealtime(0.6f);
        Check(Time.time == gameTime && video.time != videoTime, "Video avanca enquanto tempo do jogo fica congelado");
        var pointer = new PointerEventData(EventSystem.current);
        EventSystem.current.SetSelectedGameObject(null);
        ExecuteEvents.Execute(resume.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        yield return new WaitForSecondsRealtime(0.25f);
        Check(resume.transform.localScale.x > 1.03f, "Hover anima com tempo do jogo parado");
        ExecuteEvents.Execute(resume.gameObject, pointer, ExecuteEvents.pointerDownHandler);
        yield return new WaitForSecondsRealtime(0.25f);
        Check(resume.transform.localScale.x < 1f, "Pressionar produz animacao leve");
        ExecuteEvents.Execute(resume.gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ScreenCapture.CaptureScreenshot("Logs/pause-preview.png");
        yield return new WaitForSecondsRealtime(0.3f);
        resume.onClick.Invoke();
        Check(!menu.EstaPausado && Mathf.Approximately(Time.timeScale, 0.6f) && !AudioListener.pause, "Retornar restaura velocidade e audio anteriores");
        Check(movement.enabled && !inputs.gameplayInputBlocked && inputs.cursorInputForLook && !inputs.cursorLocked && Cursor.visible, "Retornar restaura movimento e cursor anteriores");
        Check(!video.isPlaying, "Video para ao fechar menu");
        movement.enabled = false;
        AudioListener.pause = true;
        menu.Pausar();
        menu.Pausar();
        menu.Retornar();
        Check(!movement.enabled && AudioListener.pause && Mathf.Approximately(Time.timeScale, 0.6f), "Pause repetido preserva estado de puzzle e audio ja pausado");
        AudioListener.pause = false;
        menu.Pausar();
        menu.enabled = false;
        Check(!menu.EstaPausado && Time.timeScale > 0f && !AudioListener.pause, "Desativar menu libera estado global");
        menu.enabled = true;
        menu.Pausar();
        life.ForcarDerrota();
        Check(!menu.EstaPausado && life.EstaMorto && life.telaGameOverUI.activeSelf && Time.timeScale > 0, "Morte fecha pause antes de abrir game over");
        menu.Pausar();
        Check(!menu.EstaPausado, "Pause nao abre sobre game over");
#if ENABLE_INPUT_SYSTEM
        InputSystem.RemoveDevice(keyboard);
#endif
        UnityEngine.Object.Destroy(player);
        yield return null;
        yield return null;
        Check(menu == null, "Menu acompanha ciclo de vida do jogador");
        var secondPlayer = new GameObject("Endy sair");
        secondPlayer.SetActive(false);
        secondPlayer.AddComponent<StarterAssets.StarterAssetsInputs>();
        var controller = secondPlayer.AddComponent<StarterAssets.ThirdPersonController>();
        controller.CinemachineCameraTarget = new GameObject("Alvo camera");
        controller.CinemachineCameraTarget.transform.SetParent(secondPlayer.transform);
        var secondLife = secondPlayer.AddComponent<VidaEndy>();
        secondLife.mostrarIndicadorVida = false;
        secondLife.scriptDeMovimento = controller;
        secondPlayer.SetActive(true);
        yield return null;
        yield return null;
        var secondMenu = UnityEngine.Object.FindAnyObjectByType<GerenciadorPause>();
        controller.ReceberArremesso(Vector3.forward, 5f, 3f, 2f, 0f);
        Vector3 position = secondPlayer.transform.position;
        secondMenu.Pausar();
        yield return new WaitForSecondsRealtime(0.2f);
        Check(controller.enabled && controller.GameplayPaused && controller.EstaArremessado && secondPlayer.transform.position == position,
            "Pause congela controller real sem cancelar arremesso");
        secondMenu.Retornar();
        Check(!controller.GameplayPaused && controller.EstaArremessado, "Retornar preserva arremesso em andamento");
        secondMenu.Pausar();
        EditorApplication.playModeStateChanged += VerifyExit;
        secondMenu.GetComponentsInChildren<Button>(true).Single(b => b.name == "Sair").onClick.Invoke();
        Check(!secondMenu.EstaPausado && Time.timeScale > 0 && !AudioListener.pause,
            "Sair restaura estado global antes de encerrar");
        yield return new WaitForSecondsRealtime(5f);
        throw new Exception("Sair nao encerrou Play Mode.");
    }

    void VerifyExit(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingPlayMode) return;
        EditorApplication.playModeStateChanged -= VerifyExit;
        report += "PASS: Botao Sair encerra Play Mode\n";
        Finish(null);
    }
}

public class PauseValidationMovement : MonoBehaviour { }
