using UnityEngine;
using UnityEngine.SceneManagement;
using StarterAssets;

[DisallowMultipleComponent]
public class VidaJogadorKraken : MonoBehaviour
{
    [Min(1)] public int vidaMaxima = 100;
    [Min(0)] public float invulnerabilidade = .8f;
    [Header("Recuperacao na caverna")]
    [Min(0f)] public float esperaSemDano = 5f;
    [Min(.05f)] public float intervaloRecuperacao = 1f;
    [Min(1)] public int vidaPorRecuperacao = 10;
    public int VidaAtual { get; private set; }
    public bool Morto { get; private set; }
    float podeReceberEm;
    float tempoAteRecuperar;
    string cenaParaReiniciar;
    string cenaCheckpointVitoria;
    Pose checkpointVitoria;

    public void RegistrarCheckpointVitoria(Pose ponto)
    {
        cenaCheckpointVitoria = SceneManager.GetActiveScene().path;
        checkpointVitoria = ponto;
    }
    public bool TentarObterCheckpointVitoria(Scene cena, out Pose ponto)
    {
        ponto = checkpointVitoria;
        return !string.IsNullOrEmpty(cenaCheckpointVitoria) && cena.path == cenaCheckpointVitoria;
    }
    void OnEnable() { SceneManager.sceneLoaded += LimparCheckpointAoSair; }
    void LimparCheckpointAoSair(Scene cena, LoadSceneMode modo)
    {
        if (modo == LoadSceneMode.Single && cena.path != cenaCheckpointVitoria)
            cenaCheckpointVitoria = null;
    }
    void OnDisable() { SceneManager.sceneLoaded -= LimparCheckpointAoSair; }

    void Awake() { VidaAtual = vidaMaxima; tempoAteRecuperar = esperaSemDano; }
    void Start() { AtualizarEfeitoVida(false); }
    void Update() { AtualizarRecuperacao(Time.deltaTime); }
    void AtualizarRecuperacao(float delta)
    {
        // O mesmo jogador persiste na fase 4, onde a cura continua sendo por pilares.
        if (SceneManager.GetActiveScene().path != ProgressoJogo.CenaCaverna)
        {
            tempoAteRecuperar = Mathf.Max(0f, esperaSemDano);
            return;
        }
        if (Morto || VidaAtual >= vidaMaxima || delta <= 0f) return;
        tempoAteRecuperar -= delta;
        while (tempoAteRecuperar <= 0f && VidaAtual < vidaMaxima)
        {
            RecuperarDano(Mathf.Max(1, vidaPorRecuperacao));
            tempoAteRecuperar += Mathf.Max(.05f, intervaloRecuperacao);
        }
    }
    public void RestaurarVida(int quantidade)
    {
        VidaAtual = Mathf.Clamp(quantidade, 1, vidaMaxima);
        tempoAteRecuperar = Mathf.Max(0f, esperaSemDano);
        AtualizarEfeitoVida(false);
    }
    public void RecuperarDano(int quantidade)
    {
        if (Morto || quantidade <= 0) return;
        VidaAtual = Mathf.Min(vidaMaxima, VidaAtual + quantidade);
        AtualizarEfeitoVida(false);
    }
    public bool ReceberDano(int dano, bool danoContinuo = false)
    {
        if (Morto || dano <= 0 || Time.timeScale <= 0f || (!danoContinuo && Time.time < podeReceberEm)) return false;
        VidaAtual = Mathf.Max(0, VidaAtual - dano);
        tempoAteRecuperar = Mathf.Max(0f, esperaSemDano);
        if (!danoContinuo) podeReceberEm = Time.time + invulnerabilidade;
        AtualizarEfeitoVida(true);
        if (VidaAtual == 0) Morrer();
        return true;
    }
    public void Morrer()
    {
        if (Morto) return;
        Morto = true;
        VidaAtual = 0;
        cenaParaReiniciar = SceneManager.GetActiveScene().path;
        if (cenaParaReiniciar == ProgressoJogo.CenaCaverna)
            PedraBrilhante.RestaurarProgresso(0);
        SceneManager.sceneLoaded += AoReiniciarCena;
        var pause = FindAnyObjectByType<GerenciadorPause>();
        if (pause != null) pause.Retornar();
        var diario = GetComponent<DiarioEndy>();
        if (diario != null && diario.Aberto) diario.Fechar();
        // A inundacao roda na coroutine da batalha: interromper apenas a agua
        // permite que o ataque seguinte continue durante a morte.
        foreach (var batalha in FindObjectsByType<BatalhaKraken>(FindObjectsSortMode.None))
        {
            batalha.StopAllCoroutines();
            batalha.enabled = false;
        }
        foreach (var captura in FindObjectsByType<CapturaJogadorKraken>(FindObjectsSortMode.None))
            captura.Cancelar();
        foreach (var agua in FindObjectsByType<InundacaoKraken>(FindObjectsSortMode.None))
            agua.CancelarERestaurar();
        var controle = GetComponent<ThirdPersonController>();
        if (controle)
        {
            controle.DefinirMultiplicadorAgua(1);
            controle.enabled = false;
        }
        AtualizarEfeitoVida(false);
        var animator = GetComponent<VidaEndy>()?.animEndy;
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (animator) { animator.ResetTrigger("TomarDano"); animator.SetTrigger("Morrer"); }
        StartCoroutine(MostrarMorteAposAnimacao());
    }
    void AtualizarEfeitoVida(bool dano)
    {
        var efeito = GetComponent<VidaEndy>();
        if (!efeito) efeito = gameObject.AddComponent<VidaEndy>();
        efeito.AtualizarVidaDaBatalha((float)VidaAtual / Mathf.Max(1, vidaMaxima), dano);
    }
    public void ConfigurarAvisoVidaBaixa(int danoDoGolpe)
    {
        AtualizarEfeitoVida(false);
        var efeito = GetComponent<VidaEndy>();
        // Com golpes de 34, sobram 32 pontos antes do golpe fatal: 30% nunca avisaria.
        efeito.limiteVidaBaixa = Mathf.Clamp(Mathf.Max(.3f,
            (float)danoDoGolpe / Mathf.Max(1, vidaMaxima)), .05f, 1f);
    }
    System.Collections.IEnumerator MostrarMorteAposAnimacao()
    {
        var efeito = GetComponent<VidaEndy>();
        yield return new WaitForSeconds(efeito ? Mathf.Max(.1f, efeito.tempoAnimacaoMorte) : 2.5f);
        GerenciadorGameOver.MostrarNaFase();
    }
    void AoReiniciarCena(Scene cena, LoadSceneMode modo)
    {
        if (cena.path != cenaParaReiniciar) return;
        SceneManager.sceneLoaded -= AoReiniciarCena;
        // TrocarMundo mantem o jogador vivo entre cenas; ele precisa de reset proprio.
        var controle = GetComponent<ThirdPersonController>();
        var capsula = GetComponent<CharacterController>();
        if (capsula) capsula.enabled = false;
        // Na caverna, TrocarMundo já posiciona o jogador no checkpoint da pedra.
        if (TentarObterCheckpointVitoria(cena, out var checkpoint))
            transform.SetPositionAndRotation(checkpoint.position, checkpoint.rotation);
        else if (cena.path != ProgressoJogo.CenaCaverna || !TryGetComponent<TrocarMundo>(out _))
        {
            foreach (var raiz in cena.GetRootGameObjects())
                foreach (var entrada in raiz.GetComponentsInChildren<Transform>(true))
                    if (entrada.name == "PontoEntrada" && entrada.gameObject.activeInHierarchy)
                        transform.SetPositionAndRotation(entrada.position, Quaternion.Euler(0, entrada.eulerAngles.y, 0));
        }
        Physics.SyncTransforms();
        if (capsula) capsula.enabled = true;
        if (controle)
        {
            controle.DefinirMultiplicadorAgua(1);
            controle.PrepararTrocaDeMundo(Camera.main);
            controle.enabled = true;
        }
        foreach (var camera in FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsSortMode.None))
            camera.PreviousStateIsValid = false;
        VidaAtual = vidaMaxima;
        tempoAteRecuperar = Mathf.Max(0f, esperaSemDano);
        podeReceberEm = Time.time + invulnerabilidade;
        Morto = false;
        AtualizarEfeitoVida(false);
        var animator = GetComponentInChildren<Animator>();
        if (animator) { animator.Rebind(); animator.Update(0f); }
    }
    void OnDestroy() { SceneManager.sceneLoaded -= AoReiniciarCena; }
}
