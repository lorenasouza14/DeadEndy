using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

[DefaultExecutionOrder(-1000)]
public class GerenciadorPause : MonoBehaviour
{
    public VideoClip videoPause;
    public TMP_FontAsset fonte;
    public float tamanhoFonte = 70f;
    public bool EstaPausado { get; private set; }

    private VidaEndy vida;
    private StarterAssets.StarterAssetsInputs inputs;
    private MonoBehaviour movimento;
    private StarterAssets.ThirdPersonController controle;
    private bool controlePausadoAnterior;
    private GameObject painel, selecaoAnterior;
    private Button retornar;
    private VideoPlayer video;
    private RawImage imagemVideo;
    private AspectRatioFitter aspecto;
    private float escalaTempoAnterior;
    private bool audioPausadoAnterior, cursorVisivelAnterior, movimentoAtivoAnterior;
    private bool cursorLockedAnterior, cursorLookAnterior, bloqueioInputAnterior;
    private CursorLockMode travaCursorAnterior;

    public static GerenciadorPause Instalar(VidaEndy jogador)
    {
        var prefab = Resources.Load<GerenciadorPause>("MenuPause");
        if (prefab == null)
        {
            Debug.LogError("Prefab Resources/MenuPause nao encontrado.", jogador);
            return null;
        }
        var menu = Instantiate(prefab);
        menu.name = "MenuPause";
        SceneManager.MoveGameObjectToScene(menu.gameObject, jogador.gameObject.scene);
        menu.vida = jogador;
        menu.inputs = jogador.GetComponent<StarterAssets.StarterAssetsInputs>();
        menu.movimento = jogador.scriptDeMovimento != null ? jogador.scriptDeMovimento : jogador.GetComponent<StarterAssets.ThirdPersonController>();
        menu.controle = menu.movimento as StarterAssets.ThirdPersonController;
        menu.CriarInterface();
        return menu;
    }

    void Update()
    {
        if (vida == null) { Destroy(gameObject); return; }
        if (vida.EstaMorto || !vida.isActiveAndEnabled) { Retornar(); return; }
#if ENABLE_INPUT_SYSTEM
        bool alternar = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
#else
        bool alternar = Input.GetKeyDown(KeyCode.Escape);
#endif
        if (alternar)
        {
            if (EstaPausado) Retornar();
            else Pausar();
        }
    }

    public void Pausar()
    {
        if (EstaPausado || vida == null || vida.EstaMorto || !vida.isActiveAndEnabled || Time.timeScale <= 0f) return;
        escalaTempoAnterior = Time.timeScale;
        audioPausadoAnterior = AudioListener.pause;
        cursorVisivelAnterior = Cursor.visible;
        travaCursorAnterior = Cursor.lockState;
        selecaoAnterior = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (inputs != null)
        {
            cursorLockedAnterior = inputs.cursorLocked;
            cursorLookAnterior = inputs.cursorInputForLook;
            bloqueioInputAnterior = inputs.gameplayInputBlocked;
            inputs.gameplayInputBlocked = true;
            inputs.cursorLocked = inputs.cursorInputForLook = false;
            LimparInputs();
        }
        if (controle != null)
        {
            controlePausadoAnterior = controle.GameplayPaused;
            controle.GameplayPaused = true;
        }
        else if (movimento != null)
        {
            movimentoAtivoAnterior = movimento.enabled;
            movimento.enabled = false;
        }
        EstaPausado = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        GarantirEventSystem();
        painel.SetActive(true);
        imagemVideo.color = Color.black;
        EventSystem.current.SetSelectedGameObject(retornar.gameObject);
        if (video.clip != null) video.Prepare();
    }

    public void Retornar()
    {
        if (!EstaPausado) return;
        EstaPausado = false;
        if (video != null) video.Stop();
        if (imagemVideo != null) imagemVideo.texture = null;
        if (painel != null) painel.SetActive(false);
        Time.timeScale = escalaTempoAnterior;
        AudioListener.pause = audioPausadoAnterior;
        Cursor.lockState = travaCursorAnterior;
        Cursor.visible = cursorVisivelAnterior;
        if (inputs != null)
        {
            LimparInputs();
            inputs.gameplayInputBlocked = bloqueioInputAnterior;
            inputs.cursorLocked = cursorLockedAnterior;
            inputs.cursorInputForLook = cursorLookAnterior;
        }
        if (controle != null) controle.GameplayPaused = controlePausadoAnterior;
        else if (movimento != null && vida != null && !vida.EstaMorto) movimento.enabled = movimentoAtivoAnterior;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(selecaoAnterior);
    }

    public void Sair()
    {
        if (!EstaPausado) return;
        Retornar();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void LimparInputs()
    {
        inputs.move = inputs.look = Vector2.zero;
        inputs.jump = inputs.sprint = false;
    }

    void OnDisable() { Retornar(); }
    void OnDestroy()
    {
        Retornar();
        if (video != null)
        {
            video.prepareCompleted -= VideoPreparado;
            video.errorReceived -= ErroVideo;
        }
    }

    void VideoPreparado(VideoPlayer player)
    {
        if (!EstaPausado) return;
        aspecto.aspectRatio = player.height > 0 ? (float)player.width / player.height : 16f / 9f;
        imagemVideo.texture = player.texture;
        imagemVideo.color = Color.white;
        player.Play();
    }

    void ErroVideo(VideoPlayer player, string erro)
    {
        Debug.LogWarning("Nao foi possivel reproduzir menu_pause: " + erro, this);
        // Os botoes permanecem disponiveis mesmo se o dispositivo nao decodificar o video.
    }

    void GarantirEventSystem()
    {
        if (EventSystem.current != null) return;
        var eventos = new GameObject("EventSystemPause", typeof(EventSystem));
        eventos.transform.SetParent(transform, false);
#if ENABLE_INPUT_SYSTEM
        eventos.AddComponent<InputSystemUIInputModule>();
#else
        eventos.AddComponent<StandaloneInputModule>();
#endif
    }

    void CriarInterface()
    {
        painel = new GameObject("TelaPause", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        painel.transform.SetParent(transform, false);
        var canvas = painel.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        var scaler = painel.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var fundo = new GameObject("Fundo", typeof(RectTransform), typeof(Image));
        fundo.transform.SetParent(painel.transform, false);
        Esticar(fundo.GetComponent<RectTransform>());
        fundo.GetComponent<Image>().color = Color.black;

        var tela = new GameObject("VideoPause", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter), typeof(VideoPlayer));
        tela.transform.SetParent(painel.transform, false);
        Esticar(tela.GetComponent<RectTransform>());
        imagemVideo = tela.GetComponent<RawImage>();
        imagemVideo.raycastTarget = false;
        aspecto = tela.GetComponent<AspectRatioFitter>();
        aspecto.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        aspecto.aspectRatio = 16f / 9f;
        video = tela.GetComponent<VideoPlayer>();
        video.playOnAwake = false;
        video.source = VideoSource.VideoClip;
        video.clip = videoPause;
        video.isLooping = true;
        video.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        video.renderMode = VideoRenderMode.APIOnly;
        video.waitForFirstFrame = true;
        var som = tela.AddComponent<AudioSource>();
        som.playOnAwake = false;
        som.ignoreListenerPause = true;
        som.spatialBlend = 0f;
        video.audioOutputMode = VideoAudioOutputMode.AudioSource;
        if (videoPause != null && videoPause.audioTrackCount > 0)
        {
            video.controlledAudioTrackCount = 1;
            video.EnableAudioTrack(0, true);
            video.SetTargetAudioSource(0, som);
        }
        video.prepareCompleted += VideoPreparado;
        video.errorReceived += ErroVideo;

        TMP_Text modelo = vida.telaGameOverUI != null ? vida.telaGameOverUI.GetComponentInChildren<TMP_Text>(true) : null;
        retornar = CriarBotao("Retornar", new Vector2(-240f, -332f), modelo);
        var sair = CriarBotao("Sair", new Vector2(240f, -332f), modelo);
        retornar.onClick.AddListener(Retornar);
        sair.onClick.AddListener(Sair);
        retornar.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = sair, selectOnDown = sair };
        sair.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = retornar, selectOnUp = retornar };
        painel.SetActive(false);
    }

    Button CriarBotao(string nome, Vector2 posicao, TMP_Text modelo)
    {
        var objeto = new GameObject(nome, typeof(RectTransform), typeof(Image), typeof(Button));
        objeto.transform.SetParent(painel.transform, false);
        var rect = objeto.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = posicao;
        rect.sizeDelta = new Vector2(420f, 100f);
        objeto.GetComponent<Image>().color = Color.clear;
        var botao = objeto.GetComponent<Button>();
        botao.transition = Selectable.Transition.None;
        botao.targetGraphic = objeto.GetComponent<Image>();
        var legenda = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
        legenda.transform.SetParent(objeto.transform, false);
        Esticar(legenda.GetComponent<RectTransform>());
        var texto = legenda.GetComponent<TextMeshProUGUI>();
        texto.text = nome;
        texto.font = modelo != null ? modelo.font : fonte;
        if (modelo != null) texto.fontSharedMaterial = modelo.fontSharedMaterial;
        texto.fontSize = modelo != null ? modelo.fontSize : tamanhoFonte;
        texto.fontStyle = modelo != null ? modelo.fontStyle : FontStyles.Normal;
        texto.color = modelo != null ? modelo.color : Color.white;
        texto.enableAutoSizing = false;
        texto.textWrappingMode = TextWrappingModes.NoWrap;
        texto.alignment = TextAlignmentOptions.Center;
        texto.raycastTarget = false;
        objeto.AddComponent<AnimacaoBotaoMenu>();
        return botao;
    }

    static void Esticar(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
