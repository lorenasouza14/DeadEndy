using TMPro;
using UnityEngine;

// Usa o aviso criado no projeto e mantém seu tamanho legível mesmo nos itens pequenos.
[DefaultExecutionOrder(150)]
public class DicaInteracao : MonoBehaviour
{
    public GameObject modeloAviso;
    private Canvas canvas;
    private TMP_Text texto;
    private ItemColetavel item;
    private InteracaoBilhete bilhete;
    private Renderer[] malhas;
    public bool Visivel => canvas != null && canvas.gameObject.activeSelf;

    void Awake()
    {
        item = GetComponent<ItemColetavel>();
        bilhete = GetComponent<InteracaoBilhete>();
        malhas = GetComponentsInChildren<Renderer>(true);
        var go = new GameObject("Aviso de interação", typeof(RectTransform), typeof(Canvas));
        go.transform.SetParent(transform, false);
        go.layer = 5;
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        ((RectTransform)go.transform).sizeDelta = new Vector2(360, 90);
        GameObject aviso = modeloAviso != null ? Instantiate(modeloAviso, go.transform, false) :
            new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
        aviso.transform.SetParent(go.transform, false);
        aviso.SetActive(true);
        var rect = aviso.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        texto = aviso.GetComponent<TMP_Text>();
        texto.alignment = TextAlignmentOptions.Center;
        texto.fontSize = 24;
        texto.raycastTarget = false;
        texto.outlineWidth = 0.15f;
        go.SetActive(false);
    }

    void LateUpdate()
    {
        string dica = item != null ? item.ObterDica() : bilhete != null && bilhete.MostrarDica ? "[E] Ler bilhete" : null;
        if (canvas == null) return;
        canvas.gameObject.SetActive(!string.IsNullOrEmpty(dica));
        if (!canvas.gameObject.activeSelf) return;
        texto.text = dica;
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        foreach (var mesh in malhas)
            if (mesh != null && mesh.enabled && mesh.gameObject.activeInHierarchy) bounds.Encapsulate(mesh.bounds);
        canvas.transform.position = new Vector3(bounds.center.x, bounds.max.y + 0.55f, bounds.center.z);
        Vector3 escala = transform.lossyScale;
        canvas.transform.localScale = new Vector3(0.007f / Mathf.Max(0.001f, Mathf.Abs(escala.x)),
            0.007f / Mathf.Max(0.001f, Mathf.Abs(escala.y)), 0.007f / Mathf.Max(0.001f, Mathf.Abs(escala.z)));
        var camera = Camera.main;
        if (camera != null) canvas.transform.rotation = camera.transform.rotation;
    }

    void OnDisable() { if (canvas != null) canvas.gameObject.SetActive(false); }
}
