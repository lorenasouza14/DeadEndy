using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(50)]
public class PatoFlutuante : MonoBehaviour
{
    public InundacaoKraken agua;
    public Renderer corpo;
    [Range(0, .8f)] public float fracaoSubmersa = .28f;
    [Min(0)] public float alturaDoBalanco = .08f;
    [Min(.01f)] public float frequenciaDoBalanco = .45f;
    [Range(0, 15)] public float inclinacao = 3f;
    float alturaDoPivo, fase;
    Quaternion rotacaoInicial;

    float CalcularAlturaDoPivo()
    {
        if (!corpo) corpo = GetComponentInChildren<Renderer>();
        if (!corpo) return 0;
        return transform.position.y - (corpo.bounds.min.y + corpo.bounds.size.y * fracaoSubmersa);
    }
    public void AjustarAoNivel(float nivel)
    {
        alturaDoPivo = CalcularAlturaDoPivo();
        var posicao = transform.position;
        posicao.y = nivel + alturaDoPivo;
        transform.position = posicao;
    }
    void Awake()
    {
        alturaDoPivo = CalcularAlturaDoPivo();
        rotacaoInicial = transform.rotation;
        fase = transform.position.x * .13f + transform.position.z * .17f;
        var fisica = GetComponent<Rigidbody>();
        if (fisica) { fisica.isKinematic = true; fisica.useGravity = false; }
    }
    void LateUpdate()
    {
        if (!agua || !agua.isActiveAndEnabled || !agua.superficieAgua) return;
        float t = Time.time * frequenciaDoBalanco * Mathf.PI * 2 + fase;
        var posicao = transform.position;
        posicao.y = agua.NivelAtual + alturaDoPivo + Mathf.Sin(t) * alturaDoBalanco;
        transform.SetPositionAndRotation(posicao, rotacaoInicial * Quaternion.Euler(
            Mathf.Sin(t * .83f) * inclinacao, 0, Mathf.Sin(t * .67f + 1.1f) * inclinacao));
    }
}
