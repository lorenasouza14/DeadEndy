using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(InventarioEndy))]
public class IndicadorItemEndy : MonoBehaviour
{
    public TMP_FontAsset fonte;
    public Sprite pocaoVerde, pocaoAzul, pirulito, trompete, radio, diario;
    private InventarioEndy inventario;
    private VidaEndy vida;
    private GameObject interfaceItem;
    private RectTransform areaSegura, painelItem, primeiraLinha, segundaLinha;
    private Image icone, iconeDiario;
    private CanvasGroup acoes;
    private TMP_Text teclaPrincipal, acaoPrincipal;
    private GameObject dicaDiario;
    private InventarioEndy.TipoItemNaMao anterior = (InventarioEndy.TipoItemNaMao)(-1);
    private Rect ultimaAreaSegura;
    private Vector2 ultimaResolucao;
    private static readonly Color Marrom = new Color(0.18f, 0.085f, 0.025f, 1f);
    private static readonly Color Creme = new Color(1f, 0.91f, 0.76f, 1f);
    public bool Visivel => interfaceItem != null && interfaceItem.activeSelf && painelItem.gameObject.activeSelf;
    public Sprite IconeAtual => icone != null ? icone.sprite : null;
    public bool DiarioVisivel => interfaceItem != null && interfaceItem.activeSelf && iconeDiario.gameObject.activeSelf;

    void Awake()
    {
        inventario = GetComponent<InventarioEndy>();
        vida = GetComponent<VidaEndy>();
        interfaceItem = new GameObject("Itens do Endy", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        // Canvas independente da escala do personagem quando ele bebe a poção de encolher.
        var canvas = interfaceItem.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = interfaceItem.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var group = interfaceItem.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        areaSegura = Rect("Área segura", interfaceItem.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
        painelItem = Rect("Item na mão", areaSegura, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-48, 44), new Vector2(434, 144));
        icone = Picture("Ícone do item", painelItem, new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(144, 144));
        var linhas = Rect("Ações", painelItem, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(270, 112));
        acoes = linhas.gameObject.AddComponent<CanvasGroup>();
        primeiraLinha = Row(linhas, "Ação principal", 28, "R", "Beber", out teclaPrincipal, out acaoPrincipal);
        segundaLinha = Row(linhas, "Arremesso", -28, "F", "Arremessar", out _, out _);
        iconeDiario = Picture("Diário guardado", areaSegura, Vector2.zero, Vector2.zero, new Vector2(48, 44), new Vector2(80, 80));
        iconeDiario.sprite = diario;
        var dica = Text("Abrir diário", areaSegura, "[J] Ler diário", 28, Creme, new Vector2(250, 48));
        dica.rectTransform.anchorMin = dica.rectTransform.anchorMax = Vector2.zero;
        dica.rectTransform.pivot = Vector2.zero;
        dica.rectTransform.anchoredPosition = new Vector2(144, 56);
        dica.alignment = TextAlignmentOptions.MidlineLeft;
        dica.outlineColor = Marrom;
        dica.outlineWidth = 0.18f;
        dicaDiario = dica.gameObject;
        Atualizar();
    }

    void LateUpdate() { Atualizar(); }

    public GameObject CriarDicaDeFase(Sprite lampada, out TMP_Text mensagem)
    {
        var painel = Rect("Dica da fase", areaSegura, Vector2.one, Vector2.one,
            Vector2.zero, Vector2.zero);
        painel.anchorMin = new Vector2(0.5f, 1f);
        painel.offsetMin = new Vector2(0, -212);
        painel.offsetMax = new Vector2(-36, -80);
        var direita = new Vector2(1, 0.5f);
        var imagem = Picture("Lâmpada", painel, direita, direita, Vector2.zero, new Vector2(86, 86));
        imagem.sprite = lampada;
        mensagem = Text("Mensagem", painel, "", 36, Creme, Vector2.zero);
        mensagem.rectTransform.anchorMin = Vector2.zero;
        mensagem.rectTransform.anchorMax = Vector2.one;
        mensagem.rectTransform.offsetMin = Vector2.zero;
        mensagem.rectTransform.offsetMax = new Vector2(-108, 0);
        mensagem.alignment = TextAlignmentOptions.MidlineRight;
        mensagem.textWrappingMode = TextWrappingModes.Normal;
        mensagem.outlineColor = new Color(0.2f, 0.1f, 0.05f);
        mensagem.outlineWidth = 0.18f;
        painel.gameObject.SetActive(false);
        return painel.gameObject;
    }

    void Atualizar()
    {
        if (interfaceItem == null) return;
        bool exibir = inventario.isActiveAndEnabled && inventario.mostrarControles && (vida == null || !vida.EstaMorto);
        interfaceItem.SetActive(exibir);
        if (!exibir) return;
        var resolucao = new Vector2(Screen.width, Screen.height);
        var segura = Screen.safeArea;
        if (resolucao != ultimaResolucao || segura != ultimaAreaSegura)
        {
            areaSegura.anchorMin = new Vector2(segura.xMin / Mathf.Max(1, Screen.width), segura.yMin / Mathf.Max(1, Screen.height));
            areaSegura.anchorMax = new Vector2(segura.xMax / Mathf.Max(1, Screen.width), segura.yMax / Mathf.Max(1, Screen.height));
            areaSegura.offsetMin = areaSegura.offsetMax = Vector2.zero;
            ultimaResolucao = resolucao;
            ultimaAreaSegura = segura;
        }
        var item = inventario.ItemNaMao;
        painelItem.gameObject.SetActive(item != InventarioEndy.TipoItemNaMao.Nenhum);
        iconeDiario.gameObject.SetActive(inventario.temDiario && diario != null);
        dicaDiario.SetActive(inventario.temDiario);
        // Sem tempo limite: ícone e comandos acompanham o item até ele sair da mão.
        acoes.alpha = inventario.ExecutandoAcao || Time.timeScale == 0 ? 0.4f : 1f;
        if (item == anterior) return;
        anterior = item;
        bool pocao = item == InventarioEndy.TipoItemNaMao.PocaoVerde || item == InventarioEndy.TipoItemNaMao.PocaoAzul;
        segundaLinha.gameObject.SetActive(pocao);
        primeiraLinha.anchoredPosition = new Vector2(0, pocao ? 28 : 0);
        teclaPrincipal.text = pocao ? "R" : "F";
        acaoPrincipal.text = pocao ? "Beber" : item == InventarioEndy.TipoItemNaMao.Trompete ? "Tocar" : item == InventarioEndy.TipoItemNaMao.Radio ? "Colocar" : "Arremessar";
        switch (item)
        {
            case InventarioEndy.TipoItemNaMao.PocaoVerde: icone.sprite = pocaoVerde; break;
            case InventarioEndy.TipoItemNaMao.PocaoAzul: icone.sprite = pocaoAzul; break;
            case InventarioEndy.TipoItemNaMao.Pirulito: icone.sprite = pirulito; break;
            case InventarioEndy.TipoItemNaMao.Trompete: icone.sprite = trompete; break;
            case InventarioEndy.TipoItemNaMao.Radio: icone.sprite = radio; break;
            default: icone.sprite = null; break;
        }
    }

    RectTransform Row(Transform parent, string name, float y, string key, string action, out TMP_Text keyText, out TMP_Text actionText)
    {
        var row = Rect(name, parent, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, y), new Vector2(270, 44));
        var border = Picture("Tecla", row, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(44, 44));
        border.color = Marrom;
        var face = Picture("Fundo da tecla", border.transform, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(40, 40));
        face.color = Creme;
        keyText = Text("Letra", face.transform, key, 29, Marrom, new Vector2(40, 40));
        keyText.alignment = TextAlignmentOptions.Center;
        actionText = Text("Ação", row, action, 30, Creme, new Vector2(210, 48));
        var rect = actionText.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0, 0.5f);
        rect.pivot = new Vector2(0, 0.5f);
        rect.anchoredPosition = new Vector2(60, 0);
        actionText.alignment = TextAlignmentOptions.MidlineLeft;
        actionText.outlineColor = Marrom;
        actionText.outlineWidth = 0.18f;
        return row;
    }

    TMP_Text Text(string name, Transform parent, string value, float size, Color color, Vector2 dimensions)
    {
        var rect = Rect(name, parent, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, dimensions);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = fonte;
        text.fontSize = size;
        text.color = color;
        text.text = value;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    static Image Picture(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var image = Rect(name, parent, anchor, pivot, position, size).gameObject.AddComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    void OnDisable() { if (interfaceItem != null) interfaceItem.SetActive(false); }
    void OnDestroy() { if (interfaceItem != null) Destroy(interfaceItem); }
}
