using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent, RequireComponent(typeof(Button))]
public class AnimacaoBotaoMenu : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
{
    Button botao;
    TMP_Text texto;
    Vector3 escalaOriginal;
    Vector2 posicaoOriginal;
    Color corOriginal;
    Selectable.Transition transicaoOriginal;
    bool sobre, selecionado, pressionado;
    float pressionadoAte;

    void Awake()
    {
        botao = GetComponent<Button>();
        texto = GetComponentInChildren<TMP_Text>(true);
        if (texto == null) { enabled = false; return; }
        escalaOriginal = texto.rectTransform.localScale;
        posicaoOriginal = texto.rectTransform.anchoredPosition;
        corOriginal = texto.color;
        transicaoOriginal = botao.transition;
    }

    void OnEnable()
    {
        if (texto != null)
        {
            botao.transition = Selectable.Transition.None;
            texto.canvasRenderer.SetColor(Color.white);
        }
    }

    void Update()
    {
        if (texto == null) return;
        bool ativo = botao.isActiveAndEnabled && botao.IsInteractable();
        bool destaque = ativo && (sobre || selecionado);
        bool clique = ativo && (pressionado || Time.unscaledTime < pressionadoAte);
        float escala = clique ? 0.94f : destaque ? 1.06f : 1f;
        Color cor = !ativo ? new Color(corOriginal.r, corOriginal.g, corOriginal.b, corOriginal.a * 0.35f) :
            destaque || clique ? Color.Lerp(corOriginal, new Color(1f, 0.88f, 0.48f, corOriginal.a), clique ? 0.8f : 0.55f) : corOriginal;
        float suavidade = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
        // Só o texto se move: a área clicável fica fixa, evitando oscilar ao passar pela borda.
        texto.rectTransform.localScale = Vector3.Lerp(texto.rectTransform.localScale, escalaOriginal * escala, suavidade);
        texto.rectTransform.anchoredPosition = Vector2.Lerp(texto.rectTransform.anchoredPosition,
            posicaoOriginal + (destaque && !clique ? new Vector2(5, 2) : Vector2.zero), suavidade);
        texto.color = Color.Lerp(texto.color, cor, suavidade);
    }

    public void OnPointerEnter(PointerEventData evento) { sobre = true; }
    public void OnPointerExit(PointerEventData evento) { sobre = pressionado = false; }
    public void OnPointerDown(PointerEventData evento) { if (evento.button == PointerEventData.InputButton.Left) pressionado = true; }
    public void OnPointerUp(PointerEventData evento) { if (evento.button == PointerEventData.InputButton.Left) pressionado = false; }
    public void OnSelect(BaseEventData evento) { selecionado = true; }
    public void OnDeselect(BaseEventData evento) { selecionado = pressionado = false; }
    public void OnSubmit(BaseEventData evento) { pressionadoAte = Time.unscaledTime + 0.12f; }

    void OnDisable()
    {
        sobre = selecionado = pressionado = false;
        pressionadoAte = 0;
        if (texto == null) return;
        texto.rectTransform.localScale = escalaOriginal;
        texto.rectTransform.anchoredPosition = posicaoOriginal;
        texto.color = corOriginal;
        botao.transition = transicaoOriginal;
    }
}
