using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

[DefaultExecutionOrder(-10000)]
public class MenuManager : MonoBehaviour
{
    static MenuManager instancia;
    public static bool EstaAberto => instancia != null && instancia.isActiveAndEnabled;
    public Button botaoContinuar;
    public TMP_FontAsset fonte;
    [TextArea(3, 12)] public string creditos = "Dead Endy\n\nCréditos em breve.";
    bool carregando;
    GameObject dialogo;
    TMP_Text mensagem;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ReiniciarInstancia() { instancia = null; }

    void OnEnable() { SceneManager.sceneLoaded += AoCarregarCena; }
    void OnDisable() { SceneManager.sceneLoaded -= AoCarregarCena; }

    void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        if (!carregando) PrepararMenu();
    }

    void PrepararMenu()
    {
        // O editor pode iniciar com MenuPrincipal e Demo_Day abertos juntos.
        // Desativa somente os objetos da fase em Play Mode, sem alterar a cena salva.
        if (gameObject.scene.isLoaded) SceneManager.SetActiveScene(gameObject.scene);
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var cena = SceneManager.GetSceneAt(i);
            if (!cena.isLoaded || cena == gameObject.scene || !ProgressoJogo.EhCenaJogo(cena.path)) continue;
            foreach (var raiz in cena.GetRootGameObjects()) raiz.SetActive(false);
        }
        LiberarCursor();
    }

    void LiberarCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void LateUpdate() { LiberarCursor(); }
    void OnApplicationFocus(bool foco) { if (foco) LiberarCursor(); }

    void Awake()
    {
        instancia = this;
        Time.timeScale = 1;
        PrepararMenu();
    }

    void GarantirEventos()
    {
        if (EventSystem.current == null)
        {
            var eventos = new GameObject("EventSystem", typeof(EventSystem));
            SceneManager.MoveGameObjectToScene(eventos, gameObject.scene);
#if ENABLE_INPUT_SYSTEM
            eventos.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
#else
            eventos.AddComponent<StandaloneInputModule>();
#endif
        }
    }

    void Start()
    {
        PrepararMenu();
        GarantirEventos();
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            foreach (var candidato in FindObjectsByType<Canvas>())
                if (candidato.gameObject.scene == gameObject.scene) { canvas = candidato; break; }
        foreach (var botao in FindObjectsByType<Button>())
            if (botao.gameObject.scene == gameObject.scene && botao.GetComponent<AnimacaoBotaoMenu>() == null)
                botao.gameObject.AddComponent<AnimacaoBotaoMenu>();
        if (canvas != null)
        {
            mensagem = Texto(canvas.transform, "Estado do save", "", new Vector2(0, 0), new Vector2(900, 75), 24);
            mensagem.rectTransform.anchorMin = mensagem.rectTransform.anchorMax = new Vector2(0.5f, 0);
            mensagem.rectTransform.pivot = new Vector2(0.5f, 0);
            mensagem.rectTransform.anchoredPosition = new Vector2(0, 16);
        }
        AtualizarContinuar();
    }

    public void AtualizarContinuar()
    {
        bool podeContinuar = ProgressoJogo.TentarCarregar(out _, out string aviso);
        if (botaoContinuar != null) botaoContinuar.interactable = podeContinuar;
        if (mensagem != null) mensagem.text = aviso ?? (podeContinuar ?
            "Continue do último ponto seguro." : "Seu progresso será salvo automaticamente em pontos seguros.");
    }

    public void IniciarNovoJogo()
    {
        if (carregando || (dialogo != null && dialogo.activeSelf)) return;
        if (ProgressoJogo.ExisteArquivo)
            AbrirDialogo("Começar uma nova aventura?\n\nO progresso salvo será substituído.", "Começar de novo", () => CarregarJogo(true), "Cancelar");
        else CarregarJogo(true);
    }

    public void ContinuarJogo()
    {
        if (carregando || (dialogo != null && dialogo.activeSelf)) return;
        CarregarJogo(false);
    }

    void CarregarJogo(bool novo)
    {
        if (carregando) return;
        if (!Application.CanStreamedLevelBeLoaded(ProgressoJogo.CenaIntegrada))
        {
            AbrirDialogo("A cena do jogo não está disponível nesta versão.", "Voltar", FecharDialogo);
            return;
        }
        if (novo) ProgressoJogo.PrepararNovoJogo();
        else if (!ProgressoJogo.PrepararContinuar(out string aviso))
        {
            AtualizarContinuar();
            AbrirDialogo(aviso ?? "Ainda não há progresso salvo.", "Voltar", FecharDialogo);
            return;
        }
        carregando = true;
        if (mensagem != null) mensagem.text = "Carregando...";
        FecharDialogo();
        Time.timeScale = 1;
        // O personagem assume o cursor quando a fase estiver pronta, nunca no clique do menu.
        SceneManager.LoadSceneAsync(ProgressoJogo.CenaIntegrada, LoadSceneMode.Single);
    }

    public void MostrarCreditos()
    {
        if (!carregando) AbrirDialogo(creditos, "Voltar", FecharDialogo);
    }

    public void SairDoJogo()
    {
        if (carregando) return;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void Update()
    {
        if (dialogo == null || !dialogo.activeSelf) return;
#if ENABLE_INPUT_SYSTEM
        bool fechar = UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        bool fechar = Input.GetKeyDown(KeyCode.Escape);
#endif
        if (fechar) FecharDialogo();
    }

    void AbrirDialogo(string texto, string confirmar, UnityEngine.Events.UnityAction acao, string cancelar = null)
    {
        if (dialogo != null) { dialogo.SetActive(false); Destroy(dialogo); }
        dialogo = new GameObject("Diálogo do menu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        dialogo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        dialogo.GetComponent<Canvas>().sortingOrder = 500;
        var escala = dialogo.GetComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1600, 900);
        escala.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var fundo = Painel(dialogo.transform, "Escurecimento", Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.8f));
        fundo.anchorMin = Vector2.zero;
        fundo.anchorMax = Vector2.one;
        fundo.offsetMin = fundo.offsetMax = Vector2.zero;
        var caixa = Painel(dialogo.transform, "Painel", Vector2.zero, new Vector2(920, 460), new Color(0.19f, 0.1f, 0.05f));
        Texto(caixa, "Mensagem", texto, new Vector2(0, 65), new Vector2(820, 270), 32);
        Botao(caixa, confirmar, new Vector2(cancelar == null ? 0 : -205, -150), acao);
        if (cancelar != null) Botao(caixa, cancelar, new Vector2(205, -150), FecharDialogo);
    }

    void FecharDialogo() { if (dialogo != null) dialogo.SetActive(false); }

    void Botao(Transform pai, string rotulo, Vector2 posicao, UnityEngine.Events.UnityAction acao)
    {
        var rect = Painel(pai, rotulo, posicao, new Vector2(360, 70), new Color(0.42f, 0.26f, 0.12f));
        var botao = rect.gameObject.AddComponent<Button>();
        botao.targetGraphic = rect.GetComponent<Image>();
        botao.onClick.AddListener(acao);
        Texto(rect, "Texto", rotulo, Vector2.zero, new Vector2(340, 65), 29);
        rect.gameObject.AddComponent<AnimacaoBotaoMenu>();
    }

    RectTransform Painel(Transform pai, string nome, Vector2 posicao, Vector2 tamanho, Color cor)
    {
        var go = new GameObject(nome, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(pai, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = posicao;
        rect.sizeDelta = tamanho;
        go.GetComponent<Image>().color = cor;
        return rect;
    }

    TMP_Text Texto(Transform pai, string nome, string texto, Vector2 posicao, Vector2 tamanho, float tamanhoFonte)
    {
        var go = new GameObject(nome, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(pai, false);
        var label = go.GetComponent<TextMeshProUGUI>();
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = Vector2.one * 0.5f;
        label.rectTransform.anchoredPosition = posicao;
        label.rectTransform.sizeDelta = tamanho;
        if (fonte != null) label.font = fonte;
        label.fontSize = tamanhoFonte;
        label.color = new Color(1, 0.91f, 0.76f);
        label.alignment = TextAlignmentOptions.Center;
        label.text = texto;
        label.raycastTarget = false;
        return label;
    }

    void OnDestroy()
    {
        if (instancia == this) instancia = null;
        if (dialogo != null) Destroy(dialogo);
    }
}
