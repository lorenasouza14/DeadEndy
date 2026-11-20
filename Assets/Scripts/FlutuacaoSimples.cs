using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FlutuacaoSimples : MonoBehaviour
{
    [Header("Configura��es da �gua")]
    [Tooltip("Arraste o objeto/plano da �gua aqui para rastrear a altura (Y) dinamicamente.")]
    public Transform planoDaAgua;

    [Header("F�sica de Flutua��o")]
    public float forcaFlutuacao = 15f;
    public float atritoNaAgua = 3f; // Desacelera o objeto debaixo d'�gua para n�o quicar infinitamente
    public float atritoAngularNaAgua = 1f;

    private Rigidbody rb;
    private float atritoOriginal;
    private float atritoAngularOriginal;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        atritoOriginal = rb.linearDamping;
        atritoAngularOriginal = rb.angularDamping;
    }

    // A mare desloca o suporte antes de avaliar o dano da agua.
    bool acompanhaMare;
    float nivelMare;
    CharacterController passageiro;
    public void AcompanharSubida(float anterior, float nivel, CharacterController jogador)
    {
        acompanhaMare = true; nivelMare = nivel; passageiro = jogador;
        if (nivel < anterior) return;
        if (!rb) rb = GetComponent<Rigidbody>();
        var colisor = GetComponent<Collider>();
        if (!colisor || !colisor.enabled || colisor.isTrigger) return;
        var limites = colisor.bounds;
        if (limites.min.y > nivel || limites.max.y < anterior - 2f) return;
        bool apoiado = false;
        if (jogador && jogador.enabled)
        {
            Vector3 pes = jogador.transform.TransformPoint(jogador.center - Vector3.up * jogador.height * .5f);
            apoiado = colisor.Raycast(new Ray(pes + Vector3.up * .25f, Vector3.down), out var hit, .5f)
                && hit.normal.y > .5f;
        }
        float subida = Mathf.Max(nivel - anterior, nivel + .25f - limites.max.y);
        if (subida <= 0f) return;
        Vector3 deslocamento = Vector3.up * subida;
        // Move o passageiro antes para o suporte nao invadir a capsula.
        if (apoiado) jogador.Move(deslocamento);
        rb.position += deslocamento;
        Physics.SyncTransforms();
    }

    void FixedUpdate()
    {
        if (planoDaAgua == null) return;
        if (acompanhaMare) AcompanharSubida(nivelMare, nivelMare, passageiro);

        // Verifica se o objeto est� abaixo do n�vel da �gua
        if (transform.position.y < planoDaAgua.position.y)
        {
            // Calcula a profundidade para aplicar uma for�a proporcional
            float profundidade = planoDaAgua.position.y - transform.position.y;

            // Empurra o objeto para cima (Vector3.up)
            rb.AddForce(Vector3.up * forcaFlutuacao * profundidade, ForceMode.Acceleration);

            // Aplica o atrito da �gua para estabilizar a flutua��o e evitar que a t�bua saia voando
            rb.linearDamping = atritoNaAgua;
            rb.angularDamping = atritoAngularNaAgua;
        }
        else
        {
            // Retorna ao atrito normal do ar quando a madeira estiver fora da �gua
            rb.linearDamping = atritoOriginal;
            rb.angularDamping = atritoAngularOriginal;
        }
    }
}