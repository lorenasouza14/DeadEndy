using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// Movimento do fantasma e morte apos exposicao continua ao foco da lanterna.
/// </summary>
public class FantasmaCaverna : MonoBehaviour
{
    // Colisao horizontal propria: os fantasmas se separam sem bloquear nas paredes.
    private static readonly List<FantasmaCaverna> presentes = new List<FantasmaCaverna>();
    private static int ultimoFrameDeSeparacao = -1;
    private float raioDeSeparacao = .6f;
    private void OnEnable() { if (!presentes.Contains(this)) presentes.Add(this); }

    private Transform jogador;
    private GerenciadorFantasmasCaverna gerenciador;
    private float velocidadeBase;
    private float aumentoDeVelocidadePorPedra;
    [Header("Resistencia a lanterna")]
    [SerializeField, Min(.1f)] private float segundosNoFeixeParaMorrer = 1f;
    private float tempoNoFeixe;
    private float duracaoDaMorte = .35f;
    private float tempoDeMorte;
    private Vector3 escalaAntesDaMorte;
    public bool EmMorte { get; private set; }
    private Renderer[] renderizadores;
    private CharacterController controlador;
    [Header("Apoio exclusivo dos fantasmas")]
    [Min(0)] public float alturaSobrePiso = .4f;
    private readonly RaycastHit[] acertosDeApoio = new RaycastHit[64];
    private readonly Dictionary<Collider, bool> obstaculosSemPiso = new Dictionary<Collider, bool>();
    private RotasChaoFantasmas rotas;
    private NavMeshPath caminho;
    private Vector3[] cantos = new Vector3[0];
    private int proximoCanto;
    private float recalcularEm;
    private Vector3 destinoDaRota;
    public bool SeguindoRota => rotas && rotas.Pronto && cantos.Length > 0;

    private void Awake()
    {
        renderizadores = GetComponentsInChildren<Renderer>(true);
        controlador = GetComponent<CharacterController>();
        if (controlador != null)
            controlador.enabled = false;
        // O movimento deste inimigo e controlado por este script, sem gravidade do exemplo.
        foreach (var exemplo in GetComponentsInChildren<Sample.GhostScript>(true))
            exemplo.enabled = false;
        foreach (var anim in GetComponentsInChildren<Animator>(true))
            anim.applyRootMotion = false;
        foreach (var corpo in GetComponentsInChildren<Rigidbody>(true))
        {
            corpo.isKinematic = true;
            corpo.useGravity = false;
        }
        int camadaFantasma = LayerMask.NameToLayer("Fantasma");
        if (camadaFantasma >= 0)
            foreach (var t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = camadaFantasma;
        GarantirColliderParaOLuz();
        if (renderizadores.Length > 0)
        {
            Bounds corpo = renderizadores[0].bounds;
            foreach (var r in renderizadores) if (r) corpo.Encapsulate(r.bounds);
            raioDeSeparacao = Mathf.Clamp(Mathf.Max(corpo.extents.x, corpo.extents.z), .45f, 1.2f);
        }
    }

    public void Configurar(
        Transform alvo,
        GerenciadorFantasmasCaverna origem,
        float novaVelocidadeBase,
        float novoAumentoPorPedra,
        float novaDuracaoDaMorte)
    {
        jogador = alvo;
        gerenciador = origem;
        velocidadeBase = novaVelocidadeBase;
        aumentoDeVelocidadePorPedra = novoAumentoPorPedra;
        duracaoDaMorte = Mathf.Clamp(novaDuracaoDaMorte, .05f, 1f);
        if (Application.isPlaying && gerenciador)
        {
            rotas = RotasChaoFantasmas.Obter(gameObject.scene);
            caminho = new NavMeshPath();
            // Corrige tambem marcadores que ficaram alguns metros sob o terreno.
            if (rotas.Amostrar(transform.position, 20f, out var pisoInicial))
                transform.position = pisoInicial + Vector3.up * alturaSobrePiso;
        }
        if (TentarEncontrarPiso(transform.position, out float altura))
        {
            Vector3 posicao = transform.position;
            posicao.y = altura + alturaSobrePiso;
            transform.position = posicao;
        }
    }

    private void Update()
    {
        if (EmMorte)
        {
            tempoDeMorte += Time.deltaTime;
            float progresso = Mathf.Clamp01(tempoDeMorte / duracaoDaMorte);
            float tamanho = 1f - Mathf.SmoothStep(0f, 1f, progresso);
            transform.localScale = escalaAntesDaMorte * Mathf.Max(.001f, tamanho);
            if (progresso >= 1f) Destroy(gameObject);
            return;
        }
        if (AtingidoPelaLanterna())
        {
            tempoNoFeixe += Time.deltaTime;
            if (tempoNoFeixe >= segundosNoFeixeParaMorrer)
            {
                IniciarMorte();
                return;
            }
        }
        else tempoNoFeixe = 0f;
        if (jogador == null)
            return;

        destinoDaRota = DestinoNoPiso();
        Vector3 direcao = destinoDaRota - transform.position;
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
            // A orientacao visual suaviza; a translacao segue os cantos sem cortar as curvas.
            movimentoHorizontal = direcao.normalized * Mathf.Min(velocidade,
                direcao.magnitude / Mathf.Max(.0001f, Time.deltaTime));
        }

        MoverPeloChao(movimentoHorizontal, Time.deltaTime);

    }

    private void LateUpdate()
    {
        if (ultimoFrameDeSeparacao == Time.frameCount) return;
        ultimoFrameDeSeparacao = Time.frameCount;
        // Resolve depois que todos perseguiram o jogador, inclusive spawns coincidentes.
        for (int passo = 0; passo < 12; passo++)
        for (int i = 0; i < presentes.Count; i++)
        for (int j = i + 1; j < presentes.Count; j++)
        {
            var a = presentes[i]; var b = presentes[j];
            if (!a || !b || a.EmMorte || b.EmMorte || a.gameObject.scene != b.gameObject.scene) continue;
            if (Mathf.Abs(a.transform.position.y - b.transform.position.y) > 2f) continue;
            Vector3 diferenca = a.transform.position - b.transform.position;
            diferenca.y = 0f;
            float minimo = a.raioDeSeparacao + b.raioDeSeparacao;
            float distancia = diferenca.magnitude;
            if (distancia >= minimo) continue;
            Vector3 direcao = distancia > .0001f ? diferenca / distancia
                : Quaternion.Euler(0f, (i * 137 + j * 73) % 360, 0f) * Vector3.right;
            Vector3 ajuste = direcao * ((minimo - distancia) * .5f);
            a.DeslocarSeparacao(ajuste);
            b.DeslocarSeparacao(-ajuste);
        }
    }

    private void DeslocarSeparacao(Vector3 ajuste)
    {
        Vector3 posicao = transform.position + ajuste;
        if (TentarEncontrarPiso(posicao, out float altura)) posicao.y = altura + alturaSobrePiso;
        transform.position = posicao;
    }

    private bool AtingidoPelaLanterna()
    {
        var lanterna = LanternaJogador.Instancia;
        if (!lanterna || !lanterna.Ligada) return false;
        if (lanterna.EstaIluminando(PontoDeMira(), transform)) return true;
        // Tambem detecta a borda do corpo entrando no foco, sem exigir o centro.
        foreach (var r in renderizadores)
        {
            if (!r || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            var b = r.bounds;
            Vector3 e = b.extents * .8f;
            if (lanterna.EstaIluminando(b.center + Vector3.right * e.x, transform)
                || lanterna.EstaIluminando(b.center - Vector3.right * e.x, transform)
                || lanterna.EstaIluminando(b.center + Vector3.up * e.y, transform)
                || lanterna.EstaIluminando(b.center - Vector3.up * e.y, transform)
                || lanterna.EstaIluminando(b.center + Vector3.forward * e.z, transform)
                || lanterna.EstaIluminando(b.center - Vector3.forward * e.z, transform)) return true;
        }
        return false;
    }

    private void IniciarMorte()
    {
        if (EmMorte) return;
        EmMorte = true;
        tempoDeMorte = 0f;
        escalaAntesDaMorte = transform.localScale;
        foreach (var colisor in GetComponentsInChildren<Collider>(true)) colisor.enabled = false;
        foreach (var animador in GetComponentsInChildren<Animator>(true)) animador.enabled = false;
    }

    private Vector3 DestinoNoPiso()
    {
        if (!rotas || !rotas.Pronto || caminho == null) return jogador.position;
        Vector3 pes = transform.position - Vector3.up * alturaSobrePiso;
        bool emPiso = rotas.Amostrar(pes, 1.5f, out var local)
            && Vector3.ProjectOnPlane(local - pes, Vector3.up).sqrMagnitude < .5f;
        if (Time.time >= recalcularEm && (emPiso || cantos.Length == 0))
        {
            recalcularEm = Time.time + .7f + (GetHashCode() & 7) * .025f;
            if (rotas.Calcular(pes, jogador.position, caminho))
            {
                cantos = caminho.corners;
                float alturaAtual = pes.y;
                for (int i = 0; i < cantos.Length; i++)
                {
                    var referencia = new Vector3(cantos[i].x, alturaAtual, cantos[i].z);
                    if (rotas.Altura(referencia, out float y)) alturaAtual = y;
                    cantos[i].y = alturaAtual;
                }
                proximoCanto = cantos.Length > 1 ? 1 : 0;
            }
        }
        if (cantos.Length == 0) return jogador.position;
        while (proximoCanto < cantos.Length - 1
            && Vector3.ProjectOnPlane(cantos[proximoCanto] - pes, Vector3.up).sqrMagnitude < .09f)
            proximoCanto++;
        return cantos[proximoCanto] + Vector3.up * alturaSobrePiso;
    }

    private void MoverPeloChao(Vector3 movimentoHorizontal, float deltaTime)
    {
        Vector3 posicaoAnterior = transform.position;
        Vector3 seguinte = posicaoAnterior + movimentoHorizontal * deltaTime;
        if (SeguindoRota)
        {
            // Nas pequenas ligacoes entre pecas, interpola a altura dos dois pisos.
            float distancia = Vector3.ProjectOnPlane(destinoDaRota - posicaoAnterior, Vector3.up).magnitude;
            seguinte.y = Mathf.Lerp(posicaoAnterior.y, destinoDaRota.y,
                Mathf.Clamp01(movimentoHorizontal.magnitude * deltaTime / Mathf.Max(.001f, distancia)));
        }
        transform.position = seguinte;
        if (TentarEncontrarPiso(transform.position, out float altura))
        {
            Vector3 posicao = transform.position;
            posicao.y = altura + alturaSobrePiso;
            transform.position = posicao;
        }
        // Fora de qualquer apoio, conserva a altura: nao despenca nos vaos.
    }

    private bool TentarEncontrarPiso(Vector3 ponto, out float altura)
    {
        float referencia = ponto.y - alturaSobrePiso;
        bool temRota = rotas && rotas.Pronto;
        Vector3 pisoNavegavel = default;
        bool pisoDaRota = temRota && rotas.Amostrar(ponto - Vector3.up * alturaSobrePiso, 2f, out pisoNavegavel)
            && Vector3.ProjectOnPlane(pisoNavegavel - ponto, Vector3.up).sqrMagnitude < .5f;
        if (pisoDaRota) { altura = pisoNavegavel.y; return true; }
        // Procura acima das elevacoes; escolhe o apoio proximo da altura da rota, nao o teto.
        Vector3 origem = new Vector3(ponto.x, referencia + (pisoDaRota ? 2f : 8f), ponto.z);
        int pisoReal = Physics.DefaultRaycastLayers & ~LayerMask.GetMask("PonteFantasma", "Fantasma", "Personagem", "Water");
        int quantidade = Physics.RaycastNonAlloc(origem, Vector3.down, acertosDeApoio,
            pisoDaRota ? 5f : 60f, pisoReal, QueryTriggerInteraction.Ignore);
        altura = float.NegativeInfinity;
        float menorDiferenca = float.PositiveInfinity;
        for (int i = 0; i < quantidade; i++)
        {
            var acerto = acertosDeApoio[i];
            if (acerto.normal.y <= .4f || acerto.transform.IsChildOf(transform) || IgnorarComoPiso(acerto.collider)) continue;
            if (acerto.collider.GetComponentInParent<StarterAssets.ThirdPersonController>() != null) continue;
            if (acerto.collider.GetComponentInParent<FantasmaCaverna>() != null) continue;
            if (acerto.collider.bounds.min.y > referencia + 1f) continue;
            float diferenca = Mathf.Abs(acerto.point.y - referencia);
            if (diferenca < menorDiferenca) { menorDiferenca = diferenca; altura = acerto.point.y; }
        }
        // O piso visivel tem prioridade, mesmo quando fica abaixo da ponte invisivel.
        if (!float.IsNegativeInfinity(altura)) return true;
        if (pisoDaRota) { altura = referencia; return true; }
        // A rota mantem a altura entre as bordas; nao cai na ponte plana ou na agua.
        if (temRota && SeguindoRota) return false;

        int caminhos = LayerMask.GetMask("PonteFantasma");
        quantidade = Physics.RaycastNonAlloc(ponto + Vector3.up * 5f, Vector3.down, acertosDeApoio,
            40f, caminhos, QueryTriggerInteraction.Collide);
        for (int i = 0; i < quantidade; i++)
            if (acertosDeApoio[i].normal.y > .45f)
                altura = Mathf.Max(altura, acertosDeApoio[i].point.y);
        return !float.IsNegativeInfinity(altura);
    }

    private bool IgnorarComoPiso(Collider colisor)
    {
        if (obstaculosSemPiso.TryGetValue(colisor, out bool ignorar)) return ignorar;
        string nome = colisor.name.ToLowerInvariant();
        ignorar = nome.Contains("water") || nome.Contains("agua") || nome.Contains("ceiling")
            || nome.Contains("teto") || nome.Contains("wall") || nome.Contains("parede")
            || nome.Contains("door") || nome.Contains("porta");
        obstaculosSemPiso[colisor] = ignorar;
        return ignorar;
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
        bool possuiArea = false;
        foreach (var colisor in GetComponentsInChildren<Collider>(true))
        {
            if (colisor is CharacterController) { colisor.enabled = false; continue; }
            colisor.isTrigger = true;
            if (colisor.enabled) possuiArea = true;
        }
        if (possuiArea || renderizadores.Length == 0) return;

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

    private void OnDisable()
    {
        presentes.Remove(this);
        if (!EmMorte) tempoNoFeixe = 0f;
    }

    private void OnDestroy()
    {
        gerenciador?.RemoverFantasma(this);
    }
}
