using UnityEngine;
using UnityEngine.UI;

// Apenas a borda de perigo, sem painel, barra ou textos de vida.
[DisallowMultipleComponent]
public class IndicadorVidaEndy : MonoBehaviour
{
    private VidaEndy vida;
    private GameObject interfaceVida;
    private VinhetaVidaEndy vinheta;
    private float intensidade;
    private float pulso;
    public bool Visivel => interfaceVida != null && interfaceVida.activeSelf;

    public void Preparar(VidaEndy origem)
    {
        vida = origem;
        if (interfaceVida != null) return;
        interfaceVida = new GameObject("Vida do Endy", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        interfaceVida.transform.SetParent(transform, false);
        interfaceVida.layer = 5;
        var canvas = interfaceVida.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = interfaceVida.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var group = interfaceVida.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        var edges = Rect("Bordas de perigo", interfaceVida.transform);
        edges.anchorMin = Vector2.zero;
        edges.anchorMax = Vector2.one;
        edges.offsetMin = edges.offsetMax = Vector2.zero;
        vinheta = edges.gameObject.AddComponent<VinhetaVidaEndy>();
        vinheta.raycastTarget = false;
        vinheta.color = new Color(0.38f, 0.005f, 0.015f, 1f);
        vinheta.canvasRenderer.SetAlpha(0f);

        Atualizar(0f);
    }

    void LateUpdate() { Atualizar(Time.deltaTime); }

    void Atualizar(float delta)
    {
        if (vida == null || interfaceVida == null) return;
        bool visible = vida.isActiveAndEnabled && !vida.EstaMorto && vida.mostrarIndicadorVida;
        interfaceVida.SetActive(visible);
        if (!visible) return;
        pulso += delta;
        float danger = vida.VidaBaixa ? Mathf.InverseLerp(vida.limiteVidaBaixa, 0f, vida.FracaoVida) : 0f;
        float target = vida.VidaBaixa ? Mathf.Lerp(0.55f, 0.82f, danger) : 0f;
        intensidade = Mathf.MoveTowards(intensidade, target, delta * 0.8f);
        float batida = 0.78f + 0.22f * Mathf.Sin(pulso * Mathf.PI * 2f * 1.1f);
        vinheta.canvasRenderer.SetAlpha(intensidade * batida);
    }

    static RectTransform Rect(string nome, Transform parent)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public void Ocultar()
    {
        intensidade = 0f;
        if (interfaceVida != null) interfaceVida.SetActive(false);
    }
    void OnDisable() { Ocultar(); }
    void OnDestroy() { if (interfaceVida != null) Destroy(interfaceVida); }
}
