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
    [Tooltip("Folga da face de apoio acima da agua, inclusive nas pontas inclinadas.")]
    [Min(.1f)] public float folgaAcimaDaAgua = .35f;

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
        float subida = Mathf.Max(nivel - anterior, nivel + folgaAcimaDaAgua - AlturaMinimaDeApoio(colisor));
        if (subida <= 0f) return;
        Vector3 deslocamento = Vector3.up * subida;
        // Move o passageiro antes para o suporte nao invadir a capsula.
        if (apoiado) jogador.Move(deslocamento);
        rb.position += deslocamento;
        // Evita a velocidade acumulada de queda afundar a madeira novamente na proxima simulacao.
        if (!rb.isKinematic && rb.linearVelocity.y < 0f)
        {
            var velocidade = rb.linearVelocity;
            velocidade.y = 0f;
            rb.linearVelocity = velocidade;
        }
        Physics.SyncTransforms();
    }

    static float AlturaMinimaDeApoio(Collider colisor)
    {
        Bounds local;
        if (colisor is BoxCollider caixa) local = new Bounds(caixa.center, caixa.size);
        else if (colisor is MeshCollider malha && malha.sharedMesh != null) local = malha.sharedMesh.bounds;
        else return colisor.bounds.max.y;

        float altura = float.PositiveInfinity;
        // O topo do AABB mundial e a ponta mais alta, nao a parte onde o jogador pisa.
        for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 canto = local.center + new Vector3(local.extents.x * x, local.extents.y, local.extents.z * z);
                altura = Mathf.Min(altura, colisor.transform.TransformPoint(canto).y);
            }
        return altura;
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
