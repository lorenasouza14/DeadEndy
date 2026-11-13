using StarterAssets;
using UnityEngine;

[DisallowMultipleComponent]
public partial class GorilaGuardiao : MonoBehaviour
{
    [Header("Referencias")]
    public Animator animGorila;
    public VidaEndy alvoEndy;
    [Tooltip("Opcional: ponto fora da caverna para onde o golpe empurra. Sem ele, empurra para longe do gorila.")]
    public Transform pontoDeExpulsao;

    [Header("Guarda da caverna")]
    public bool guardandoCaverna = true;
    [Tooltip("Mede o alcance a partir do corpo do gorila, inclusive se o prefab estiver ampliado.")]
    public bool detectarPeloCollider = true;
    [Tooltip("Sem collider: deslocamento do centro em unidades de mundo, sem multiplicar pela escala do prefab.")]
    public Vector3 centroLocal = new Vector3(0f, 1f, 0f);
    [Min(0.1f)] public float alcanceAtaque = 3f;
    [Min(0f)] public float intervaloEntreAtaques = 1.5f;
    [Tooltip("Camadas que bloqueiam o golpe, como paredes e pedras. Triggers sao ignorados.")]
    public LayerMask obstaculos = ~0;

    [Header("Animacao original Suriyun")]
    public string parametroAnimacao = "animation";
    public string estadoAtaque = "Base Layer.Animal_Gorilla@Attack";
    public string estadoParado = "Base Layer.Idle";
    public int valorAtaque = 6;
    public int valorParado = 0;
    [Tooltip("Fracao da animacao em que a mao acerta: 0.4 = 40%. Ajuste no Play Mode.")]
    [Range(0.01f, 0.99f)] public float momentoImpacto = 0.4f;

    [Header("Arremesso do Endy")]
    [Tooltip("Cada impacto retira um terco da vida maxima do Endy. Desmarcado: apenas arremessa.")]
    public bool causarDano = true;
    [Min(0f)] public float velocidadeArremesso = 12f;
    [Min(0f)] public float impulsoVertical = 5f;
    [Min(0.01f)] public float duracaoArremesso = 0.8f;
    [Min(0f)] public float desaceleracao = 8f;

    public bool EstaAtacando { get; private set; }
    public bool DocilPelaSopa { get; private set; }
    private ThirdPersonController movimentoEndy;
    private CharacterController capsulaEndy;
    private VidaEndy alvoPreparado;
    private int hashAtaque;
    private int hashParado;
    private int hashParametro;
    private bool configurado;
    private bool impactoProcessado;
    private float proximoAtaque;
    private float esperaEstado;
    private Collider[] collidersDoCorpo;
    private Vector3 Centro => transform.position + transform.rotation * centroLocal;

    private void Start()
    {
        // Arrastar o FBX do Project pode preencher este campo com o Animator do asset.
        // Apenas o Animator da instancia da cena consegue animar o guardiao.
        if (animGorila == null || !animGorila.gameObject.scene.IsValid() || !animGorila.transform.IsChildOf(transform))
            animGorila = GetComponentInChildren<Animator>();
        collidersDoCorpo = GetComponentsInChildren<Collider>();
        if (alvoEndy == null) alvoEndy = FindAnyObjectByType<VidaEndy>();
        hashAtaque = Animator.StringToHash(estadoAtaque);
        hashParado = Animator.StringToHash(estadoParado);
        hashParametro = Animator.StringToHash(parametroAnimacao);
        bool temParametro = false;
        if (animGorila != null && animGorila.runtimeAnimatorController != null)
            foreach (var parametro in animGorila.parameters)
                if (parametro.nameHash == hashParametro && parametro.type == AnimatorControllerParameterType.Int)
                    temParametro = true;

        configurado = temParametro && animGorila.HasState(0, hashAtaque) && animGorila.HasState(0, hashParado);
        if (!configurado)
        {
            Debug.LogError("GorilaGuardiao: atribua o Animator com o Gorilla.controller de Animations_Cute_Animals. Confira os nomes dos estados e o parametro inteiro animation.", this);
            enabled = false;
            return;
        }
        animGorila.applyRootMotion = false;
        // O dano acompanha a animacao mesmo se o gorila sair da tela.
        animGorila.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        PrepararAlvo();
        VoltarAoRepouso();
        PrepararSono();
    }

    private void PrepararAlvo()
    {
        alvoPreparado = alvoEndy;
        movimentoEndy = alvoEndy != null ? alvoEndy.GetComponentInParent<ThirdPersonController>() : null;
        if (movimentoEndy == null && alvoEndy != null)
            movimentoEndy = alvoEndy.GetComponentInChildren<ThirdPersonController>();
        capsulaEndy = movimentoEndy != null ? movimentoEndy.GetComponent<CharacterController>() : null;
        if (alvoEndy != null && movimentoEndy == null)
            Debug.LogWarning("GorilaGuardiao: o alvo precisa do ThirdPersonController do Endy para receber o arremesso.", this);
    }

    private bool AlvoDisponivel()
    {
        return alvoEndy != null && alvoEndy.isActiveAndEnabled && !alvoEndy.EstaMorto &&
            movimentoEndy != null && movimentoEndy.isActiveAndEnabled && capsulaEndy != null && capsulaEndy.enabled;
    }

    private bool AlvoAoAlcance()
    {
        if (!AlvoDisponivel()) return false;
        Vector3 destino = capsulaEndy.bounds.center;
        Vector3 origem = OrigemDeteccao(destino);
        if ((destino - origem).sqrMagnitude > alcanceAtaque * alcanceAtaque) return false;
        Vector3 direcao = destino - origem;
        // Ignora o proprio gorila e todos os colliders da hierarquia do jogador.
        foreach (var hit in Physics.RaycastAll(origem, direcao.normalized, direcao.magnitude, obstaculos, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(movimentoEndy.transform)) continue;
            return false;
        }
        return true;
    }

    private Vector3 OrigemDeteccao(Vector3 destino)
    {
        Vector3 origem = Centro;
        float menorDistancia = float.PositiveInfinity;
        if (detectarPeloCollider && collidersDoCorpo != null)
            foreach (var corpo in collidersDoCorpo)
            {
                if (corpo == null || !corpo.enabled || corpo.isTrigger || !corpo.gameObject.activeInHierarchy) continue;
                Vector3 ponto = corpo is MeshCollider mesh && !mesh.convex
                    ? corpo.bounds.ClosestPoint(destino) : corpo.ClosestPoint(destino);
                float distancia = (destino - ponto).sqrMagnitude;
                if (distancia >= menorDistancia) continue;
                menorDistancia = distancia;
                origem = ponto;
            }
        return origem;
    }

    private void Update()
    {
        if (!configurado || Time.deltaTime <= 0f || DocilPelaSopa) return;
        if (alvoPreparado != alvoEndy)
        {
            CancelarAtaque();
            PrepararAlvo();
        }
        if (!guardandoCaverna || !AlvoDisponivel() || !animGorila.isActiveAndEnabled)
        {
            if (EstaAtacando) CancelarAtaque();
            if (!guardandoCaverna) InterromperSono();
            return;
        }
        if (AtualizarSono(Time.deltaTime)) return;
        if (EstaAtacando)
        {
            AtualizarAtaque(Time.deltaTime);
            return;
        }
        if (!movimentoEndy.EstaArremessado && Time.time >= proximoAtaque && AlvoAoAlcance()) IniciarAtaque();
    }

    private void IniciarAtaque()
    {
        Vector3 direcao = Vector3.ProjectOnPlane(capsulaEndy.bounds.center - transform.position, Vector3.up);
        if (direcao.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(direcao);
        EstaAtacando = true;
        impactoProcessado = false;
        esperaEstado = 0f;
        animGorila.SetInteger(hashParametro, valorAtaque);
        animGorila.Play(hashAtaque, 0, 0f);
    }

    private void AtualizarAtaque(float delta)
    {
        if (!EstaAtacando) return;
        var estado = animGorila.GetCurrentAnimatorStateInfo(0);
        if (estado.fullPathHash != hashAtaque)
        {
            // Play e avaliado pelo Animator depois do Update. Aguarda sua entrada.
            esperaEstado += delta;
            if (esperaEstado > 0.5f) CancelarAtaque();
            return;
        }
        if (!impactoProcessado && estado.normalizedTime >= momentoImpacto)
        {
            impactoProcessado = true; // Um unico golpe, mesmo se houver varios colliders ou loop no clip.
            AplicarImpacto();
        }
        if (estado.normalizedTime >= 1f) CancelarAtaque();
    }

    private void AplicarImpacto()
    {
        if (DocilPelaSopa || !guardandoCaverna || SonoImpedeAtaque || !AlvoAoAlcance()) return;
        Vector3 direcao = pontoDeExpulsao != null
            ? pontoDeExpulsao.position - movimentoEndy.transform.position
            : movimentoEndy.transform.position - transform.position;
        if (Vector3.ProjectOnPlane(direcao, Vector3.up).sqrMagnitude < 0.0001f) direcao = transform.forward;
        if (causarDano) alvoEndy.TomarDano(alvoEndy.vidaMaxima / 3f);
        // A morte existente desliga movimento e capsula; nao reativa nenhum dos dois.
        if (!alvoEndy.EstaMorto)
            movimentoEndy.ReceberArremesso(direcao, velocidadeArremesso, impulsoVertical, duracaoArremesso, desaceleracao);
    }

    private void VoltarAoRepouso()
    {
        if (!configurado || animGorila == null || !animGorila.isActiveAndEnabled) return;
        animGorila.SetInteger(hashParametro, valorParado);
        animGorila.Play(hashParado, 0, 0f);
    }

    private void CancelarAtaque()
    {
        EstaAtacando = false;
        impactoProcessado = true;
        proximoAtaque = Time.time + Mathf.Max(0f, intervaloEntreAtaques);
        VoltarAoRepouso();
    }

    // Ponto de integracao para o futuro puzzle da sopa.
    public void DefinirGuarda(bool ativa)
    {
        if (DocilPelaSopa) return;
        guardandoCaverna = ativa;
        InterromperSono();
        CancelarAtaque();
    }

    public void TornarDocilPelaSopa()
    {
        if (DocilPelaSopa) return;
        DefinirGuarda(false);
        DocilPelaSopa = true;
    }

    private void OnDisable() { InterromperSono(); CancelarAtaque(); }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = guardandoCaverna ? Color.red : Color.green;
        bool desenhouCorpo = false;
        if (detectarPeloCollider)
            foreach (var corpo in GetComponentsInChildren<Collider>())
            {
                if (!corpo.enabled || corpo.isTrigger) continue;
                // Caixa envolvente aproximada do corpo mais a margem de ataque.
                Gizmos.DrawWireCube(corpo.bounds.center, corpo.bounds.size + Vector3.one * (2f * alcanceAtaque));
                desenhouCorpo = true;
            }
        if (!desenhouCorpo) Gizmos.DrawWireSphere(Centro, alcanceAtaque);
        if (pontoDeExpulsao != null) Gizmos.DrawLine(Centro, pontoDeExpulsao.position);
        DesenharAudicao();
    }
}
