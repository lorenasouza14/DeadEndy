using UnityEngine;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(-10)]
public class EscolhaTesouroFinal : MonoBehaviour
{
    static int frameEscolha = -1;
    public static bool BloqueiaItens => frameEscolha == Time.frameCount;
    public BatalhaKraken batalha;
    public GameObject quadro, bau;
    public Transform pontoRetorno;
    public float alcance = 5f;
    public string Escolha { get; private set; }
    InventarioEndy jogador;
    GameObject painel;
    TMP_Text confirmacao;
    float confirmarAte;

    void Update()
    {
        if (!jogador) jogador = FindAnyObjectByType<InventarioEndy>();
        if (!jogador) return;
        var vida = jogador.GetComponent<VidaJogadorKraken>();
        bool liberado = batalha && batalha.Estado == BatalhaKraken.Etapa.Derrotado
            && Time.timeScale > 0 && !MenuManager.EstaAberto && (!vida || !vida.Morto)
            && jogador.mostrarControles;
        if (!string.IsNullOrEmpty(Escolha))
        {
            if (painel) painel.SetActive(liberado && Time.time < confirmarAte);
            return;
        }
        bool perto = liberado && quadro && bau &&
            (Distancia(quadro) <= alcance || Distancia(bau) <= alcance);
        if (!perto) { if (painel) painel.SetActive(false); return; }
        if (!painel)
        {
            var indicador = jogador.GetComponent<IndicadorItemEndy>();
            if (!indicador) return;
            painel = indicador.CriarEscolhaFinal(out confirmacao);
        }
        painel.SetActive(true);
        frameEscolha = Time.frameCount;
#if ENABLE_INPUT_SYSTEM
        var teclado = Keyboard.current;
        if (teclado != null && teclado.qKey.wasPressedThisFrame) Escolher(true);
        else if (teclado != null && teclado.fKey.wasPressedThisFrame) Escolher(false);
#else
        if (Input.GetKeyDown(KeyCode.Q)) Escolher(true);
        else if (Input.GetKeyDown(KeyCode.F)) Escolher(false);
#endif
    }

    float Distancia(GameObject objeto)
    {
        var capsula = jogador.GetComponent<CharacterController>();
        Vector3 origem = capsula ? capsula.bounds.center : jogador.transform.position;
        float distancia = float.PositiveInfinity;
        foreach (var visual in objeto.GetComponentsInChildren<Renderer>())
            distancia = Mathf.Min(distancia, Vector3.Distance(origem, visual.bounds.ClosestPoint(origem)));
        return float.IsPositiveInfinity(distancia) ? Vector3.Distance(origem, objeto.transform.position) : distancia;
    }

    public void Escolher(bool pegarQuadro)
    {
        if (!painel || !painel.activeInHierarchy || !string.IsNullOrEmpty(Escolha)) return;
        Escolha = pegarQuadro ? "quadro" : "bau";
        (pegarQuadro ? quadro : bau).SetActive(false);
        foreach (Transform filho in painel.transform) filho.gameObject.SetActive(false);
        confirmacao.gameObject.SetActive(true);
        confirmacao.text = pegarQuadro ? "Você escolheu levar o quadro!" : "Você escolheu levar o baú do tesouro!";
        confirmarAte = Time.time + 6f;
        painel.SetActive(false);
        confirmarAte = 0;
        CutsceneInicio.IniciarFinal(pegarQuadro, pegarQuadro ? null : (System.Action)RetornarAoParkour);
    }

    void RetornarAoParkour()
    {
        if (!jogador) return;
        var vida = jogador.GetComponent<VidaJogadorKraken>();
        Pose destino;
        if (vida && vida.TentarObterCheckpointVitoria(gameObject.scene, out var checkpoint))
            destino = checkpoint;
        else if (batalha && batalha.spawnAposVitoria)
            destino = new Pose(batalha.spawnAposVitoria.position, batalha.spawnAposVitoria.rotation);
        else
        {
            Debug.LogError("Retorno do bau: checkpoint do parkour ausente.", this);
            return;
        }
        foreach (var fuga in FindObjectsByType<FugaDesmoronamentoKraken>(FindObjectsSortMode.None))
            fuga.ReiniciarParkour();
        if (vida) vida.RestaurarVida(vida.vidaMaxima);
        if (jogador)
        {
            var capsula = jogador.GetComponent<CharacterController>();
            bool ativa = capsula && capsula.enabled;
            if (capsula) capsula.enabled = false;
            jogador.transform.SetPositionAndRotation(destino.position, destino.rotation);
            Physics.SyncTransforms();
            if (ativa) capsula.enabled = true;
            var controle = jogador.GetComponent<StarterAssets.ThirdPersonController>();
            if (controle)
            {
                controle.DefinirMultiplicadorAgua(1);
                controle.PrepararTrocaDeMundo(Camera.main);
            }
            foreach (var camera in FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsSortMode.None))
                camera.PreviousStateIsValid = false;
            RestaurarEscolhaDoTesouro();
        }
    }

    void RestaurarEscolhaDoTesouro()
    {
        Escolha = null;
        confirmarAte = 0f;
        frameEscolha = -1;
        if (quadro) quadro.SetActive(true);
        if (bau) bau.SetActive(true);
        // A escolha escondeu os filhos do painel. Recria as opcoes ao se aproximar.
        if (painel)
        {
            painel.SetActive(false);
            Destroy(painel);
        }
        painel = null;
        confirmacao = null;
    }

    void OnDisable() { if (painel) painel.SetActive(false); }
    void OnDestroy() { if (painel) Destroy(painel); }
}



