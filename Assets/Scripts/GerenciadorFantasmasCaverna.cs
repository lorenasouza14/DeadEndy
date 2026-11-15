using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawna fantasmas da caverna gradualmente após a ativação da pedra vermelha.
/// </summary>
public class GerenciadorFantasmasCaverna : MonoBehaviour
{
    [Header("Prefab e pontos de spawn")]
    [SerializeField] private GameObject prefabDoFantasma;
    [SerializeField] private Transform[] pontosDeSpawn;

    [Header("Quantidade")]
    [Tooltip("Total criado durante a sequencia; derrotados nao sao repostos.")]
    [SerializeField, Min(1)] private int maximoDeFantasmas = 9;
    [SerializeField, Min(0.5f)] private float intervaloInicialDeSpawn = 16f;
    [SerializeField, Min(0.5f)] private float intervaloMinimoDeSpawn = 7f;
    [SerializeField, Min(0f)] private float reducaoDoIntervaloPorPedra = 1.5f;

    [Header("Horda final apos concluir a sequencia")]
    [SerializeField] private Transform[] pontosDaHordaFinal;
    [SerializeField, Min(1)] private int maximoDaHordaFinal = 25;
    [SerializeField, Min(.5f)] private float intervaloDaHordaFinal = 5f;
    private bool hordaFinalAtiva;
    private Coroutine contagemInicial;
    public bool HordaFinalAtiva => hordaFinalAtiva;
    private int LimiteAtual => hordaFinalAtiva ? maximoDaHordaFinal : maximoDeFantasmas;
    private Transform[] PontosAtuais => hordaFinalAtiva ? pontosDaHordaFinal : pontosDeSpawn;

    [Header("Movimento e lanterna")]
    [SerializeField, Min(0f)] private float velocidadeBase = 1.2f;
    [SerializeField, Min(0f)] private float aumentoDeVelocidadePorPedra = 0.25f;
    [SerializeField, Range(.05f, 1f)] private float duracaoDaMorte = .35f;

    private readonly List<FantasmaCaverna> fantasmasAtivos = new List<FantasmaCaverna>();
    private Transform jogador;
    private int totalCriadosNaSequencia;
    private float proximoSpawn;
    private int proximoPontoDeSpawn;

    // Controle de fluxo da pedra vermelha
    private bool spawnsAtivados = false;
    public bool Despertos => spawnsAtivados || contagemInicial != null;
    private bool PodeCriarFantasma => fantasmasAtivos.Count < LimiteAtual &&
        (hordaFinalAtiva || totalCriadosNaSequencia < maximoDeFantasmas);

    public float VelocidadeAtual
    {
        get
        {
            if (hordaFinalAtiva && jogador != null &&
                jogador.TryGetComponent<StarterAssets.ThirdPersonController>(out var controle))
            {
                // Pouco acima da caminhada e sempre entre caminhada e corrida.
                return Mathf.Lerp(controle.MoveSpeed, controle.SprintSpeed, .2f);
            }
            return velocidadeBase + PedraBrilhante.ProgressoAtual * aumentoDeVelocidadePorPedra;
        }
    }

    private void OnEnable()
    {
        PedraBrilhante.ProgressoAlterado += ConferirHordaFinal;
        ConferirHordaFinal();
    }
    private void OnDisable()
    {
        PedraBrilhante.ProgressoAlterado -= ConferirHordaFinal;
    }
    private void ConferirHordaFinal()
    {
        if (hordaFinalAtiva || PedraBrilhante.ProximaCorEsperada.HasValue) return;
        hordaFinalAtiva = true;
        spawnsAtivados = true;
        proximoPontoDeSpawn = 0;
        if (contagemInicial != null) { StopCoroutine(contagemInicial); contagemInicial = null; }
        proximoSpawn = Time.time + Mathf.Max(.5f, intervaloDaHordaFinal);
    }

    private void Start()
    {
        StarterAssets.ThirdPersonController controlador =
            FindFirstObjectByType<StarterAssets.ThirdPersonController>();
        if (controlador != null)
            jogador = controlador.transform;
        ConferirHordaFinal();
    }

    // Método chamado pelo script da pedra vermelha quando o jogador acerta a sequência
    public void AtivarSpawnsAposPedraVermelha()
    {
        if (!spawnsAtivados && !hordaFinalAtiva && contagemInicial == null)
        {
            contagemInicial = StartCoroutine(ContagemRegressivaParaSpawn());
        }
    }

    private IEnumerator ContagemRegressivaParaSpawn()
    {
        
        yield return new WaitForSeconds(35f);

        contagemInicial = null;
        if (hordaFinalAtiva) yield break;
        spawnsAtivados = true;
        proximoSpawn = Time.time; // Libera o spawn imediato após passar o minuto
    }

    private void Update()
    {
        // Se a pedra vermelha ainda não foi ativada, o gestor não faz nada
        if (!spawnsAtivados)
            return;

        fantasmasAtivos.RemoveAll(fantasma => fantasma == null);

        if (jogador == null)
        {
            StarterAssets.ThirdPersonController controlador =
                FindFirstObjectByType<StarterAssets.ThirdPersonController>();
            if (controlador != null)
                jogador = controlador.transform;
        }

        if (prefabDoFantasma == null || jogador == null || PontosAtuais == null || PontosAtuais.Length == 0)
            return;

        if (PodeCriarFantasma && Time.time >= proximoSpawn)
        {
            if (CriarFantasma())
                proximoSpawn = Time.time + IntervaloAtualDeSpawn();
        }
    }

    private float IntervaloAtualDeSpawn()
    {
        if (hordaFinalAtiva) return Mathf.Max(.5f, intervaloDaHordaFinal);
        return Mathf.Max(
            intervaloMinimoDeSpawn,
            intervaloInicialDeSpawn - PedraBrilhante.ProgressoAtual * reducaoDoIntervaloPorPedra);
    }

    private bool CriarFantasma()
    {
        fantasmasAtivos.RemoveAll(fantasma => fantasma == null);
        var pontos = PontosAtuais;
        if (!prefabDoFantasma || !jogador || pontos == null || pontos.Length == 0
            || !PodeCriarFantasma) return false;

        // Usa todos os pontos em rodizio; referencias vazias ou desativadas nao travam o spawn.
        Transform ponto = null;
        for (int tentativa = 0; tentativa < pontos.Length; tentativa++)
        {
            int indice = proximoPontoDeSpawn % pontos.Length;
            proximoPontoDeSpawn = (indice + 1) % pontos.Length;
            var candidato = pontos[indice];
            if (candidato && candidato.gameObject.activeInHierarchy) { ponto = candidato; break; }
        }
        if (!ponto) return false;

        GameObject objeto = Instantiate(prefabDoFantasma, ponto.position, ponto.rotation);
        FantasmaCaverna fantasma = objeto.GetComponent<FantasmaCaverna>();
        if (fantasma == null)
            fantasma = objeto.AddComponent<FantasmaCaverna>();

        fantasma.Configurar(
            jogador,
            this,
            velocidadeBase,
            aumentoDeVelocidadePorPedra,
            duracaoDaMorte);
        fantasmasAtivos.Add(fantasma);
        if (!hordaFinalAtiva) totalCriadosNaSequencia++;
        return true;
    }

    public void RemoverFantasma(FantasmaCaverna fantasma)
    {
        fantasmasAtivos.Remove(fantasma);
    }
}