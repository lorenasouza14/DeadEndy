using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent, RequireComponent(typeof(InventarioEndy))]
public class DiarioEndy : MonoBehaviour
{
    [Header("Uma imagem por página, na ordem de leitura")]
    public Sprite[] paginas = new Sprite[0];
    public TMP_FontAsset fonte;
    [Min(2)] public int paginasProvisorias = 6;
    [Min(0.1f)] public float duracaoFolheada = 0.45f;
    public bool Aberto { get; private set; }
    public bool Folheando { get; private set; }
    public bool FoiLido { get; private set; }
    public int PaginaEsquerda { get; private set; }
    public int TotalPaginas => paginas != null && paginas.Length > 0 ? paginas.Length : Mathf.Max(2, paginasProvisorias);
    public bool PodeVoltar => PaginaEsquerda > 0;
    public bool PodeAvancar => PaginaEsquerda + 2 < TotalPaginas;

    public void RestaurarPagina(int pagina)
    {
        PaginaEsquerda = Mathf.Clamp(pagina / 2, 0, (TotalPaginas - 1) / 2) * 2;
    }

    public void RestaurarLeitura(bool lido) { FoiLido = lido && GetComponent<InventarioEndy>().temDiario; }

    InventarioEndy inventario;
    VidaEndy vida;
    StarterAssets.StarterAssetsInputs inputs;
    StarterAssets.ThirdPersonController movimento;
    GameObject tela;
    RectTransform livro, folhaMovel;
    Image esquerda, direita, imagemMovel, papelMovel;
    TMP_Text vazioEsquerda, vazioDireita, vazioMovel, numeroEsquerda, numeroDireita, contador;
    Button anterior, proxima;
    float escalaTempoAnterior;
    bool cursorVisivelAnterior, cursorLockedAnterior, lookAnterior, movimentoAnterior;
    CursorLockMode travaAnterior;
    int frameFechamento = -1;
    static readonly Color Tinta = new Color(137f/255f, 81f/255f, 41f/255f);
    static readonly Color Papel = new Color(181f/255f, 143f/255f, 70f/255f);

    void Awake()
    {
        inventario = GetComponent<InventarioEndy>();
        vida = GetComponent<VidaEndy>();
        inputs = GetComponent<StarterAssets.StarterAssetsInputs>();
        movimento = GetComponent<StarterAssets.ThirdPersonController>();
    }

    void Update()
    {
        if (Aberto && (!inventario.isActiveAndEnabled || (vida != null && vida.EstaMorto))) { Fechar(); return; }
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;
        bool alternar = kb.jKey.wasPressedThisFrame;
        bool fechar = kb.escapeKey.wasPressedThisFrame;
        bool avancar = kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame;
        bool voltar = kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame;
#else
        bool alternar = Input.GetKeyDown(KeyCode.J), fechar = Input.GetKeyDown(KeyCode.Escape);
        bool avancar = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
        bool voltar = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
#endif
        if (Aberto && (alternar || fechar)) { Fechar(); return; }
        if (!Aberto && alternar) { Abrir(); return; }
        if (Aberto && avancar) Folhear(1);
        else if (Aberto && voltar) Folhear(-1);
    }

    public bool Abrir()
    {
        if (Aberto || !isActiveAndEnabled || !inventario.isActiveAndEnabled || !inventario.temDiario ||
            inventario.ExecutandoAcao || InteracaoBau.PuzzleAtivo || Time.timeScale == 0 ||
            Time.frameCount == frameFechamento || (vida != null && vida.EstaMorto)) return false;
        if (tela == null) CriarInterface();
        PaginaEsquerda = Mathf.Clamp(PaginaEsquerda, 0, ((TotalPaginas - 1) / 2) * 2);
        escalaTempoAnterior = Time.timeScale;
        travaAnterior = Cursor.lockState;
        cursorVisivelAnterior = Cursor.visible;
        if (inputs != null)
        {
            cursorLockedAnterior = inputs.cursorLocked;
            lookAnterior = inputs.cursorInputForLook;
            inputs.cursorLocked = false;
            inputs.cursorInputForLook = false;
            LimparInputs();
        }
        if (movimento != null) { movimentoAnterior = movimento.enabled; movimento.enabled = false; }
        Time.timeScale = 0;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Aberto = true;
        tela.SetActive(true);
        AtualizarPaginas();
        if (!FoiLido)
        {
            FoiLido = true;
            AutosaveEndy.Instancia?.SolicitarAutosave();
        }
        return true;
    }

    public void Fechar()
    {
        if (!Aberto) return;
        StopAllCoroutines();
        Folheando = false;
        if (folhaMovel != null) folhaMovel.gameObject.SetActive(false);
        if (tela != null) tela.SetActive(false);
        Aberto = false;
        AutosaveEndy.Instancia?.SolicitarAutosave();
        frameFechamento = Time.frameCount;
        Time.timeScale = escalaTempoAnterior;
        // Game Over passa a ser o dono do cursor e do movimento quando Endy morre.
        if (vida != null && vida.EstaMorto) return;
        Cursor.lockState = travaAnterior;
        Cursor.visible = cursorVisivelAnterior;
        if (inputs != null)
        {
            inputs.cursorLocked = cursorLockedAnterior;
            inputs.cursorInputForLook = lookAnterior;
            LimparInputs();
        }
        if (movimento != null) movimento.enabled = movimentoAnterior;
    }

    void LimparInputs()
    {
        inputs.move = inputs.look = Vector2.zero;
        inputs.jump = inputs.sprint = false;
    }

    public bool Folhear(int direcao)
    {
        if (!Aberto || Folheando || direcao == 0 || (direcao > 0 ? !PodeAvancar : !PodeVoltar)) return false;
        StartCoroutine(AnimarFolha(direcao > 0 ? 1 : -1));
        return true;
    }

    IEnumerator AnimarFolha(int direcao)
    {
        Folheando = true;
        anterior.interactable = proxima.interactable = false;
        int origem = PaginaEsquerda;
        int destino = origem + direcao * 2;
        // Primeiro revela a página sob a folha; a segunda metade mostra o verso.
        if (direcao > 0) MostrarPagina(direita, vazioDireita, destino + 1);
        else MostrarPagina(esquerda, vazioEsquerda, destino);
        folhaMovel.gameObject.SetActive(true);
        float duracao = Mathf.Max(0.1f, duracaoFolheada);
        bool verso = false;
        PrepararFolha(direcao > 0, direcao > 0 ? origem + 1 : origem);
        for (float tempo = 0; tempo < duracao; tempo += Time.unscaledDeltaTime)
        {
            float t = tempo / duracao;
            if (t >= 0.5f && !verso)
            {
                verso = true;
                PrepararFolha(direcao < 0, direcao > 0 ? destino : destino + 1);
            }
            float largura = Mathf.Abs(Mathf.Cos(t * Mathf.PI));
            folhaMovel.localScale = new Vector3(Mathf.Max(0.005f, largura), 1, 1);
            papelMovel.color = Color.Lerp(Papel * new Color(0.72f, 0.72f, 0.72f, 1), Papel, largura);
            yield return null;
        }
        PaginaEsquerda = destino;
        folhaMovel.gameObject.SetActive(false);
        Folheando = false;
        AtualizarPaginas();
    }

    void PrepararFolha(bool ladoDireito, int pagina)
    {
        folhaMovel.pivot = new Vector2(ladoDireito ? 0 : 1, 0.5f);
        folhaMovel.anchoredPosition = Vector2.zero;
        MostrarPagina(imagemMovel, vazioMovel, pagina);
    }

    void MostrarPagina(Image imagem, TMP_Text vazio, int indice)
    {
        Sprite sprite = paginas != null && indice >= 0 && indice < paginas.Length ? paginas[indice] : null;
        imagem.sprite = sprite;
        imagem.enabled = sprite != null;
        vazio.text = indice >= TotalPaginas ? "" : "Página " + (indice + 1) + "\n\n" +
            "As palavras desta aventura\nainda serão reveladas...";
        vazio.gameObject.SetActive(sprite == null);
    }

    void AtualizarPaginas()
    {
        MostrarPagina(esquerda, vazioEsquerda, PaginaEsquerda);
        MostrarPagina(direita, vazioDireita, PaginaEsquerda + 1);
        numeroEsquerda.text = (PaginaEsquerda + 1).ToString();
        numeroDireita.text = PaginaEsquerda + 1 < TotalPaginas ? (PaginaEsquerda + 2).ToString() : "";
        contador.text = (PaginaEsquerda + 1) + " – " + Mathf.Min(PaginaEsquerda + 2, TotalPaginas) + " / " + TotalPaginas;
        anterior.interactable = PodeVoltar;
        proxima.interactable = PodeAvancar;
    }

    void CriarInterface()
    {
        tela = new GameObject("Leitura do diário", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = tela.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = tela.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 1000);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var fundo = Painel("Fundo", tela.transform, Vector2.zero, Vector2.zero, new Color(0.045f, 0.028f, 0.015f, 0.9f));
        fundo.rectTransform.anchorMin = Vector2.zero;
        fundo.rectTransform.anchorMax = Vector2.one;
        fundo.rectTransform.sizeDelta = Vector2.zero;
        //Texto("Título", tela.transform, "Diário do Endy", new Vector2(0, 408), new Vector2(800, 60), 42, Papel);
        livro = Painel("Capa", tela.transform, Vector2.zero, new Vector2(1212, 710), new Color(0.28f, 0.14f, 0.07f)).rectTransform;
        Painel("Miolo", livro, new Vector2(0, -5), new Vector2(1188, 688), new Color(0.65f, 0.53f, 0.38f));
        var paginaE = Painel("Página esquerda", livro, new Vector2(-291, 0), new Vector2(580, 672), Papel);
        var paginaD = Painel("Página direita", livro, new Vector2(291, 0), new Vector2(580, 672), Papel);
        Conteudo(paginaE.transform, out esquerda, out vazioEsquerda);
        Conteudo(paginaD.transform, out direita, out vazioDireita);
        numeroEsquerda = Texto("Número", paginaE.transform, "", new Vector2(0, -316), new Vector2(100, 30), 20, Tinta);
        numeroDireita = Texto("Número", paginaD.transform, "", new Vector2(0, -316), new Vector2(100, 30), 20, Tinta);
        Painel("Lombada", livro, Vector2.zero, new Vector2(8, 672), new Color(0.38f, 0.27f, 0.19f, 0.5f));
        papelMovel = Painel("Folha virando", livro, Vector2.zero, new Vector2(582, 672), Papel);
        folhaMovel = papelMovel.rectTransform;
        Conteudo(folhaMovel, out imagemMovel, out vazioMovel);
        folhaMovel.gameObject.SetActive(false);
        var gesto = livro.gameObject.AddComponent<GestoFolhearDiario>();
        gesto.diario = this;
        anterior = Botao("Anterior", new Vector2(-470, -404), new Vector2(260, 58), () => Folhear(-1));
        proxima = Botao("Próxima", new Vector2(470, -404), new Vector2(260, 58), () => Folhear(1));
        contador = Texto("Contagem", tela.transform, "", new Vector2(0, -404), new Vector2(240, 40), 24, Papel);
        Botao("Fechar  [J / Esc]", new Vector2(565, 408), new Vector2(290, 52), Fechar);
        Texto("Controles", tela.transform, "A / D ou setas para folhear • Arraste a página com o mouse", new Vector2(0, -466), new Vector2(1300, 40), 23, Papel);
        tela.SetActive(false);
    }

    void Conteudo(Transform pai, out Image imagem, out TMP_Text vazio)
    {
        imagem = Painel("Imagem da página", pai, Vector2.zero, Vector2.zero, Color.white);
        // A arte já contém o papel e suas margens: ocupa a folha inteira, inclusive ao virar.
        var rect = imagem.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        imagem.preserveAspect = false;
        imagem.raycastTarget = false;
        vazio = Texto("Página provisória", pai, "", Vector2.zero, new Vector2(510, 550), 30, Tinta);
    }

    Button Botao(string rotulo, Vector2 posicao, Vector2 tamanho, UnityEngine.Events.UnityAction acao)
    {
        var painel = Painel(rotulo, tela.transform, posicao, tamanho, new Color(0.36f, 0.22f, 0.12f));
        var botao = painel.gameObject.AddComponent<Button>();
        botao.targetGraphic = painel;
        var cores = botao.colors;
        cores.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.4f);
        botao.colors = cores;
        botao.navigation = new Navigation { mode = Navigation.Mode.None };
        botao.onClick.AddListener(acao);
        Texto("Texto", painel.transform, rotulo, Vector2.zero, tamanho, 24, Papel);
        return botao;
    }

    Image Painel(string nome, Transform pai, Vector2 posicao, Vector2 tamanho, Color cor)
    {
        var go = new GameObject(nome, typeof(RectTransform), typeof(Image));
        go.layer = 5;
        go.transform.SetParent(pai, false);
        var image = go.GetComponent<Image>();
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = posicao;
        rect.sizeDelta = tamanho;
        image.color = cor;
        return image;
    }

    TMP_Text Texto(string nome, Transform pai, string texto, Vector2 posicao, Vector2 tamanho, float tamanhoFonte, Color cor)
    {
        var go = new GameObject(nome, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = 5;
        go.transform.SetParent(pai, false);
        var label = go.GetComponent<TextMeshProUGUI>();
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = Vector2.one * 0.5f;
        label.rectTransform.sizeDelta = tamanho;
        label.rectTransform.anchoredPosition = posicao;
        if (fonte != null) label.font = fonte;
        label.text = texto;
        label.fontSize = tamanhoFonte;
        label.color = cor;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    void OnDisable() { Fechar(); }
    void OnDestroy() { Fechar(); if (tela != null) Destroy(tela); }
}

public class GestoFolhearDiario : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public DiarioEndy diario;
    Vector2 inicio;
    public void OnBeginDrag(PointerEventData evento) { inicio = evento.position; }
    public void OnDrag(PointerEventData evento) { }
    public void OnEndDrag(PointerEventData evento)
    {
        float delta = evento.position.x - inicio.x;
        if (Mathf.Abs(delta) > Screen.width * 0.04f) diario.Folhear(delta < 0 ? 1 : -1);
    }
}
