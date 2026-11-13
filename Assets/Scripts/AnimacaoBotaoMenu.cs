using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class AnimacaoBotaoMenu : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    private bool sobre, selecionado, pressionado;
    private Vector3 escalaOriginal;
    private Graphic texto;
    private Color corOriginal;

    void Awake()
    {
        escalaOriginal = transform.localScale;
        texto = GetComponentInChildren<TMPro.TMP_Text>();
        if (texto != null) corOriginal = texto.color;
    }

    void Update()
    {
        float mistura = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
        float escala = pressionado ? 0.97f : sobre || selecionado ? 1.05f : 1f;
        transform.localScale = Vector3.Lerp(transform.localScale, escalaOriginal * escala, mistura);
        if (texto != null)
            texto.color = Color.Lerp(texto.color,
                sobre || selecionado ? new Color(1f, 0.88f, 0.6f, corOriginal.a) : corOriginal, mistura);
    }

    void OnDisable()
    {
        sobre = selecionado = pressionado = false;
        transform.localScale = escalaOriginal;
        if (texto != null) texto.color = corOriginal;
    }

    public void OnPointerEnter(PointerEventData data) { sobre = true; }
    public void OnPointerExit(PointerEventData data) { sobre = pressionado = false; }
    public void OnPointerDown(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) pressionado = true; }
    public void OnPointerUp(PointerEventData data) { pressionado = false; }
    public void OnSelect(BaseEventData data) { selecionado = true; }
    public void OnDeselect(BaseEventData data) { selecionado = pressionado = false; }
}
