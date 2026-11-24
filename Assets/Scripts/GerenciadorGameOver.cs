using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class GerenciadorGameOver : MonoBehaviour
{
    private bool reiniciando;

    void Awake()
    {
        var canvas = GetComponent<Canvas>() ?? gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 31000;
        var rect = GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        var escala = GetComponent<CanvasScaler>() ?? gameObject.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920, 1080);
        escala.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        if (!GetComponent<GraphicRaycaster>()) gameObject.AddComponent<GraphicRaycaster>();

        var fundo = transform.Find("FundoEscuro") as RectTransform;
        if (fundo)
        {
            fundo.anchorMin = Vector2.zero;
            fundo.anchorMax = Vector2.one;
            // Ultrapassa as bordas para evitar frestas por arredondamento na build.
            fundo.offsetMin = new Vector2(-2f, -2f);
            fundo.offsetMax = new Vector2(2f, 2f);
            fundo.localScale = Vector3.one;
            var imagemFundo = fundo.GetComponent<Image>();
            if (imagemFundo)
            {
                imagemFundo.sprite = null;
                imagemFundo.overrideSprite = null;
                imagemFundo.type = Image.Type.Simple;
                imagemFundo.preserveAspect = false;
            }
        }
        var arte = transform.Find("GameOver");
        var imagem = arte ? arte.GetComponent<Image>() : null;
        if (imagem && imagem.sprite)
        {
            imagem.preserveAspect = false;
            var aspecto = imagem.GetComponent<AspectRatioFitter>() ?? imagem.gameObject.AddComponent<AspectRatioFitter>();
            aspecto.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            aspecto.aspectRatio = imagem.sprite.rect.width / imagem.sprite.rect.height;
            // Avanca a arte alem das bordas para esconder a margem dos quadros animados.
            imagem.rectTransform.localScale = Vector3.one * 1.25f;
        }

        var reiniciar = GetComponentInChildren<Button>(true);
        if (!reiniciar) return;
        PosicionarBotao(reiniciar, -230);
        var sair = Instantiate(reiniciar, reiniciar.transform.parent);
        sair.name = "ButtonSair";
        sair.onClick = new Button.ButtonClickedEvent();
        sair.onClick.AddListener(SairDoJogo);
        var texto = sair.GetComponentInChildren<TMP_Text>(true);
        if (texto) texto.text = "Sair do jogo";
        PosicionarBotao(sair, 230);
    }

    static void PosicionarBotao(Button botao, float x)
    {
        var rect = (RectTransform)botao.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
        rect.pivot = new Vector2(.5f, 0f);
        rect.anchoredPosition = new Vector2(x, 70);
        rect.sizeDelta = new Vector2(420, 100);
        botao.transform.SetAsLastSibling();
    }

    public void SairDoJogo()
    {
        if (reiniciando) return;
        reiniciando = true;
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public static void MostrarNaFase()
    {
        var tela = Instantiate(Resources.Load<GerenciadorGameOver>("TelaGameOver"));
        GerenciadorPause.GarantirEventSystem(tela.transform);
        EventSystem.current.SetSelectedGameObject(tela.GetComponentInChildren<Button>().gameObject);
        Time.timeScale = 0f;
    }

    void OnEnable()
    {
        reiniciando = false;
        SoltarRatoParaInterface();
    }

    public void ReiniciarJogo()
    {
        if (reiniciando) return;
        reiniciando = true;
        if (ProgressoJogo.EhCenaIlha(gameObject.scene.path)) ProgressoJogo.PrepararContinuar(out _);
        Debug.Log("Reiniciando a batalha...");
        Time.timeScale = 1f;

        // Devolve o controle ao Endy ANTES de recarregar a cena
        TravarRatoNoJogo();

        SceneManager.LoadScene(gameObject.scene.path);
    }

    void TravarRatoNoJogo()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        StarterAssets.StarterAssetsInputs inputs = FindAnyObjectByType<StarterAssets.StarterAssetsInputs>();
        if (inputs != null)
        {
            inputs.cursorLocked = true;
            inputs.cursorInputForLook = true;
        }
    }

    void SoltarRatoParaInterface()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        StarterAssets.StarterAssetsInputs inputs = FindAnyObjectByType<StarterAssets.StarterAssetsInputs>();
        if (inputs != null)
        {
            inputs.cursorLocked = false;
            inputs.cursorInputForLook = false;
            inputs.look = Vector2.zero;
            inputs.move = Vector2.zero;
            inputs.jump = false;
            inputs.sprint = false;
        }
    }
}
