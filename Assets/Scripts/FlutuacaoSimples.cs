using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FlutuacaoSimples : MonoBehaviour
{
    [Header("Configuracoes da agua")]
    public Transform planoDaAgua;
    [Header("Fisica de flutuacao")]
    public float forcaFlutuacao = 15f;
    public float atritoNaAgua = 3f;
    public float atritoAngularNaAgua = 1f;
    [Tooltip("Folga da face de apoio acima da agua, inclusive nas pontas inclinadas.")]
    [Min(.1f)] public float folgaAcimaDaAgua = .35f;

    Rigidbody rb;
    float atritoOriginal, atritoAngularOriginal;
    bool gravidadeOriginal, cinematicoOriginal;
    RigidbodyInterpolation interpolacaoOriginal;
    bool acompanhaMare, suporteDaMare;
    float alturaRepouso, posicaoYRepouso, correcaoInicial, correcaoRestante;
    int frameCorrecao = -1;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        atritoOriginal = rb.linearDamping;
        atritoAngularOriginal = rb.angularDamping;
        gravidadeOriginal = rb.useGravity;
        cinematicoOriginal = rb.isKinematic;
        interpolacaoOriginal = rb.interpolation;
    }

    // Chamado pela inundacao do Kraken. As outras fases continuam usando empuxo fisico.
    public void AcompanharSubida(float anterior, float nivel, CharacterController jogador)
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0f) return;
        acompanhaMare = true;
        var colisor = GetComponent<Collider>();
        if (!colisor || !colisor.enabled || colisor.isTrigger) return;
        if (!suporteDaMare)
        {
            if (colisor.bounds.min.y > nivel) return;
            alturaRepouso = AlturaMinimaDeApoio(colisor);
            posicaoYRepouso = rb.position.y;
            correcaoInicial = correcaoRestante = Mathf.Max(0, nivel + folgaAcimaDaAgua - alturaRepouso);
            if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            rb.useGravity = false;
            rb.isKinematic = true;
            // A mare e atualizada por quadro; nao interpolar uma segunda pose de fisica.
            rb.interpolation = RigidbodyInterpolation.None;
            suporteDaMare = true;
        }

        if (frameCorrecao != Time.frameCount)
        {
            correcaoRestante = Mathf.MoveTowards(correcaoRestante, 0, Mathf.Max(1, correcaoInicial / .3f) * Time.deltaTime);
            frameCorrecao = Time.frameCount;
        }
        float apoioDestino = Mathf.Max(alturaRepouso, nivel + folgaAcimaDaAgua);
        float destinoY = posicaoYRepouso + Mathf.Max(0, apoioDestino - alturaRepouso - correcaoRestante);
        float deslocamentoY = destinoY - rb.position.y;
        if (Mathf.Abs(deslocamentoY) < .00001f) return;
        bool apoiado = false;
        if (jogador && jogador.enabled)
        {
            Vector3 pes = jogador.transform.TransformPoint(jogador.center - Vector3.up * jogador.height * .5f);
            apoiado = colisor.Raycast(new Ray(pes + Vector3.up * .25f, Vector3.down), out var hit, .5f)
                && hit.normal.y > .5f;
        }
        Vector3 deslocamento = Vector3.up * deslocamentoY;
        // Na subida libera espaco para a madeira; na descida retira o piso antes de levar o jogador.
        if (apoiado && deslocamentoY > 0) jogador.Move(deslocamento);
        rb.position = new Vector3(rb.position.x, destinoY, rb.position.z);
        // Sincroniza tambem a malha neste quadro, sem esperar o proximo passo fisico.
        rb.transform.position = rb.position;
        Physics.SyncTransforms();
        if (apoiado && deslocamentoY < 0) jogador.Move(deslocamento);
    }

    static float AlturaMinimaDeApoio(Collider colisor)
    {
        Bounds local;
        if (colisor is BoxCollider caixa) local = new Bounds(caixa.center, caixa.size);
        else if (colisor is MeshCollider malha && malha.sharedMesh != null) local = malha.sharedMesh.bounds;
        else return colisor.bounds.max.y;
        float altura = float.PositiveInfinity;
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
        // Nao mistura teletransporte da mare, gravidade e empuxo no mesmo corpo.
        if (planoDaAgua == null || acompanhaMare) return;
        if (transform.position.y < planoDaAgua.position.y)
        {
            float profundidade = planoDaAgua.position.y - transform.position.y;
            rb.AddForce(Vector3.up * forcaFlutuacao * profundidade, ForceMode.Acceleration);
            rb.linearDamping = atritoNaAgua;
            rb.angularDamping = atritoAngularNaAgua;
        }
        else
        {
            rb.linearDamping = atritoOriginal;
            rb.angularDamping = atritoAngularOriginal;
        }
    }

    void OnDisable()
    {
        if (rb && suporteDaMare)
        {
            rb.isKinematic = cinematicoOriginal;
            rb.useGravity = gravidadeOriginal;
            rb.interpolation = interpolacaoOriginal;
            rb.linearDamping = atritoOriginal;
            rb.angularDamping = atritoAngularOriginal;
        }
        acompanhaMare = suporteDaMare = false;
        correcaoRestante = 0;
        frameCorrecao = -1;
    }
}
