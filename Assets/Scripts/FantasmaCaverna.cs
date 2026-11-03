using UnityEngine;

/// <summary>
/// Movimento do fantasma. Ele some depois de ficar tres segundos no foco da lanterna.
/// </summary>
public class FantasmaCaverna : MonoBehaviour
{
    private Transform jogador;
    private GerenciadorFantasmasCaverna gerenciador;
    private float velocidadeBase;
    private float aumentoDeVelocidadePorPedra;
    private float segundosNoFeixeParaSumir;
    private float tempoNoFeixe;
    private Renderer[] renderizadores;
    private CharacterController controlador;

    private void Awake()
    {
        renderizadores = GetComponentsInChildren<Renderer>(true);
        controlador = GetComponent<CharacterController>();
        if (controlador != null)
            controlador.enabled = false;
        GarantirColliderParaOLuz();
    }

    public void Configurar(
        Transform alvo,
        GerenciadorFantasmasCaverna origem,
        float novaVelocidadeBase,
        float novoAumentoPorPedra,
        float novoTempoNoFeixe)
    {
        jogador = alvo;
        gerenciador = origem;
        velocidadeBase = novaVelocidadeBase;
        aumentoDeVelocidadePorPedra = novoAumentoPorPedra;
        segundosNoFeixeParaSumir = novoTempoNoFeixe;
    }

    private void Update()
    {
        if (jogador == null)
            return;

        Vector3 direcao = jogador.position - transform.position;
        direcao.y = 0f;
        Vector3 movimentoHorizontal = Vector3.zero;

        if (direcao.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(direcao),
                Time.deltaTime * 4f);

            float velocidade = velocidadeBase +
                PedraBrilhante.ProgressoAtual * aumentoDeVelocidadePorPedra;
            movimentoHorizontal = transform.forward * velocidade;
        }

        MoverPeloChao(movimentoHorizontal);

        LanternaJogador lanterna = LanternaJogador.Instancia;
        if (lanterna != null && lanterna.EstaIluminando(PontoDeMira(), transform))
            tempoNoFeixe += Time.deltaTime;
        else
            tempoNoFeixe = Mathf.Max(0f, tempoNoFeixe - Time.deltaTime * 0.5f);

        if (tempoNoFeixe >= segundosNoFeixeParaSumir)
        {
            gerenciador?.RemoverFantasma(this);
            Destroy(gameObject);
        }
    }

    private void MoverPeloChao(Vector3 movimentoHorizontal)
    {
        // Fantasmas atravessam o cenário: o movimento não usa colisões.
        // O raycast abaixo serve apenas para manter a altura acompanhando o piso.
        transform.position += movimentoHorizontal * Time.deltaTime;
        Vector3 origemDoRaio = transform.position + Vector3.up * 5f;
        if (Physics.Raycast(origemDoRaio, Vector3.down, out RaycastHit acerto, 20f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            Vector3 posicao = transform.position;
            posicao.y = Mathf.Lerp(posicao.y, acerto.point.y, Time.deltaTime * 12f);
            transform.position = posicao;
        }
    }

    private Vector3 PontoDeMira()
    {
        if (renderizadores == null || renderizadores.Length == 0)
            return transform.position + Vector3.up;

        Bounds limites = renderizadores[0].bounds;
        for (int i = 1; i < renderizadores.Length; i++)
            limites.Encapsulate(renderizadores[i].bounds);
        return limites.center;
    }

    private void GarantirColliderParaOLuz()
    {
        if (GetComponentInChildren<Collider>() != null || renderizadores.Length == 0)
            return;

        Bounds limites = renderizadores[0].bounds;
        for (int i = 1; i < renderizadores.Length; i++)
            limites.Encapsulate(renderizadores[i].bounds);

        SphereCollider colliderDoFantasma = gameObject.AddComponent<SphereCollider>();
        colliderDoFantasma.isTrigger = true;
        colliderDoFantasma.center = transform.InverseTransformPoint(limites.center);
        float maiorEscala = Mathf.Max(
            Mathf.Abs(transform.lossyScale.x),
            Mathf.Abs(transform.lossyScale.y),
            Mathf.Abs(transform.lossyScale.z));
        colliderDoFantasma.radius = limites.extents.magnitude / Mathf.Max(maiorEscala, 0.0001f);
    }

    private void OnDestroy()
    {
        gerenciador?.RemoverFantasma(this);
    }
}
