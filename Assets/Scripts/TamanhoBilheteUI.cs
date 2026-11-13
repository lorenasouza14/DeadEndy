using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class TamanhoBilheteUI : MonoBehaviour
{
    public Vector2 tamanhoMaximo = new Vector2(568.581f, 836.2978f);
    private RectTransform rect;
    void Awake() { rect = GetComponent<RectTransform>(); }
    void OnEnable() { Ajustar(); }
    void LateUpdate() { Ajustar(); }
    void Ajustar()
    {
        if (rect == null) rect = GetComponent<RectTransform>();
        var pai = rect.parent as RectTransform;
        if (pai == null || pai.rect.height <= 0 || pai.rect.width <= 0) return;
        float escala = Mathf.Min(1, pai.rect.width * 0.9f / tamanhoMaximo.x, pai.rect.height * 0.86f / tamanhoMaximo.y);
        rect.sizeDelta = tamanhoMaximo * escala;
    }
}
