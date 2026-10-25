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
    [SerializeField, Min(1)] private int maximoDeFantasmas = 3;
    [SerializeField, Min(0.5f)] private float intervaloInicialDeSpawn = 16f;
    [SerializeField, Min(0.5f)] private float intervaloMinimoDeSpawn = 7f;
    [SerializeField, Min(0f)] private float reducaoDoIntervaloPorPedra = 1.5f;

    [Header("Movimento e lanterna")]
    [SerializeField, Min(0f)] private float velocidadeBase = 1.2f;
    [SerializeField, Min(0f)] private float aumentoDeVelocidadePorPedra = 0.25f;
    [SerializeField, Min(0.1f)] private float segundosNoFeixeParaSumir = 3f;

    private readonly List<FantasmaCaverna> fantasmasAtivos = new List<FantasmaCaverna>();
    private Transform jogador;
    private float proximoSpawn;

    // Controle de fluxo da pedra vermelha
    private bool spawnsAtivados = false;

    private void Start()
    {
        StarterAssets.ThirdPersonController controlador =
            FindFirstObjectByType<StarterAssets.ThirdPersonController>();
        if (controlador != null)
            jogador = controlador.transform;
    }

    // Método chamado pelo script da pedra vermelha quando o jogador acerta a sequência
    public void AtivarSpawnsAposPedraVermelha()
    {
        if (!spawnsAtivados)
        {
            StartCoroutine(ContagemRegressivaParaSpawn());
        }
    }

    private IEnumerator ContagemRegressivaParaSpawn()
    {
        
        yield return new WaitForSeconds(35f);

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

        if (prefabDoFantasma == null || jogador == null || pontosDeSpawn == null || pontosDeSpawn.Length == 0)
            return;

        if (fantasmasAtivos.Count < maximoDeFantasmas && Time.time >= proximoSpawn)
        {
            CriarFantasma();
            proximoSpawn = Time.time + IntervaloAtualDeSpawn();
        }
    }

    private float IntervaloAtualDeSpawn()
    {
        return Mathf.Max(
            intervaloMinimoDeSpawn,
            intervaloInicialDeSpawn - PedraBrilhante.ProgressoAtual * reducaoDoIntervaloPorPedra);
    }

    private void CriarFantasma()
    {
        Transform ponto = pontosDeSpawn[Random.Range(0, pontosDeSpawn.Length)];
        if (ponto == null)
            return;

        GameObject objeto = Instantiate(prefabDoFantasma, ponto.position, ponto.rotation);
        FantasmaCaverna fantasma = objeto.GetComponent<FantasmaCaverna>();
        if (fantasma == null)
            fantasma = objeto.AddComponent<FantasmaCaverna>();

        fantasma.Configurar(
            jogador,
            this,
            velocidadeBase,
            aumentoDeVelocidadePorPedra,
            segundosNoFeixeParaSumir);
        fantasmasAtivos.Add(fantasma);
    }

    public void RemoverFantasma(FantasmaCaverna fantasma)
    {
        fantasmasAtivos.Remove(fantasma);
    }
}