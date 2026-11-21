using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StarterAssets;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-30)]
public class InundacaoKraken : MonoBehaviour
{
    public enum EtapaMare { Repouso, Avisando, Mergulhando, Subindo, Cheia, Baixando, Emergindo, AguardandoSubida }
    [Header("Referencias")]
    public Transform superficieAgua;
    public Renderer malhaAgua;
    public GameObject[] marcasRefugios;
    public Transform[] pontosSeguros;
    public LineRenderer ondaMergulho;
    public Transform centroKraken;
    [Header("Tempos para subir nas rochas")]
    [Min(.1f)] public float tempoAviso = 3f;
    [Min(.1f)] public float tempoMergulho = 1.8f;
    [Tooltip("Espera apos o Kraken terminar de afundar, antes da agua subir.")]
    [Min(0)] public float atrasoAposMergulho = 3.5f;
    [Min(.1f)] public float tempoSubida = 4f;
    [Min(.1f)] public float tempoCheia = 8f;
    [Min(.1f)] public float tempoDescida = 4f;
    [Min(.1f)] public float tempoRetorno = 3f;
    [Header("Alturas mundiais")]
    public float nivelMaximo = -6f;
    [Min(1)] public float profundidadeMergulho = 125f;
    [Header("Agua perigosa em toda a fase")]
    [Min(1)] public int danoAgua = 8;
    [Min(0)] public float profundidadeParaDano = .12f;
    [Min(.85f)] public float intervaloDano = 1.1f;
    [Range(.1f, 1)] public float velocidadeNaAgua = .55f;
    [Min(0)] public float profundidadeParaLentidao = .3f;
    [Range(.1f, 1)] public float fracaoDaCapsulaParaDano = .55f;

    public EtapaMare Fase { get; private set; }
    public bool EmExecucao { get; private set; }
    public bool EmFuga { get; private set; }
    public bool Pendente => isActiveAndEnabled && pendentes > 0;
    public int SolicitacoesPendentes => pendentes;
    public float MergulhoNormalizado { get; private set; }
    public float PreparacaoNormalizada { get; private set; }
    public float TempoRestante { get; private set; }
    public float NivelInicial { get; private set; }
    public float NivelAtual => NivelInicial + (superficieAgua ? superficieAgua.position.y - posicaoInicial.y : 0);
    public bool JogadorNaAgua { get; private set; }
    public bool AguaCausaDano => iniciado && isActiveAndEnabled;
    public string Mensagem
    {
        get
        {
            if (EmFuga) return Fase == EtapaMare.Subindo ? "A CAVERNA ESTA DESMORONANDO!\nA agua esta subindo: corra e escale as pedras!" : "A agua parou de subir.\nO patamar de descanso esta acima da agua.";
            if (Fase == EtapaMare.Avisando) return "O KRAKEN VAI AFUNDAR!\nSuba nas rochas: " + Mathf.CeilToInt(TempoRestante) + "s";
            if (Fase == EtapaMare.Mergulhando) return "ELE ESTA MERGULHANDO!\nSuba nas rochas: a agua vai subir!";
            if (Fase == EtapaMare.AguardandoSubida) return "O KRAKEN AFUNDOU!\nA agua vai subir: fique no alto das rochas.";
            if (Fase == EtapaMare.Subindo) return "A AGUA ESTA SUBINDO!\nFique no alto das rochas.";
            if (Fase == EtapaMare.Cheia) return "AGUA PERIGOSA! Fique nas rochas.\nA cheia dura mais " + Mathf.CeilToInt(TempoRestante) + "s";
            if (Fase == EtapaMare.Baixando) return "A agua esta baixando.\nEspere para descer das rochas.";
            if (Fase == EtapaMare.Emergindo) return "O Kraken esta voltando!";
            return "";
        }
    }
    Vector3 posicaoInicial;
    int pendentes;
    bool cancelado, iniciado;
    float proximoDano;
    float inicioFuga, duracaoFuga, nivelInicioFuga, nivelFimFuga;
    ThirdPersonController jogador;
    VidaJogadorKraken vida;
    CharacterController capsula;
    readonly List<Renderer> aguasDaFase = new List<Renderer>();
    readonly List<float> niveisDeRepouso = new List<float>();

    void Awake()
    {
        if (!superficieAgua || !malhaAgua) { enabled = false; return; }
        posicaoInicial = superficieAgua.position;
        NivelInicial = malhaAgua.bounds.center.y;
        foreach (var raiz in gameObject.scene.GetRootGameObjects())
            foreach (var agua in raiz.GetComponentsInChildren<Renderer>(true))
                if (agua.name.StartsWith("SM_Env_Water_Plane_01"))
                {
                    aguasDaFase.Add(agua);
                    niveisDeRepouso.Add(agua.bounds.center.y);
                }
        if (!aguasDaFase.Contains(malhaAgua))
        {
            aguasDaFase.Add(malhaAgua);
            niveisDeRepouso.Add(NivelInicial);
        }
        iniciado = true;
        MostrarRefugios(false);
        if (ondaMergulho) ondaMergulho.enabled = false;
    }
    public void Solicitar()
    {
        if (isActiveAndEnabled && iniciado && !EmFuga) pendentes++;
    }
    public IEnumerator Executar(float multiplicadorVelocidade = 1f)
    {
        if (!iniciado || !isActiveAndEnabled || EmExecucao || EmFuga || pendentes <= 0) yield break;
        float ritmo = Mathf.Max(1f, multiplicadorVelocidade);
        pendentes--; cancelado = false; EmExecucao = true; proximoDano = Time.time;
        MostrarRefugios(true);
        try
        {
            yield return Transicao(EtapaMare.Avisando, tempoAviso / ritmo, p => PreparacaoNormalizada = p);
            if (cancelado) yield break;
            yield return Transicao(EtapaMare.Mergulhando, tempoMergulho / ritmo, p => MergulhoNormalizado = p);
            if (cancelado) yield break;
            // A espera so comeca depois que o mergulho chegou a 100%.
            yield return Transicao(EtapaMare.AguardandoSubida, atrasoAposMergulho / ritmo, p => { });
            if (cancelado) yield break;
            yield return Transicao(EtapaMare.Subindo, tempoSubida / ritmo, p => DefinirNivel(Mathf.Lerp(NivelInicial, nivelMaximo, p)));
            if (cancelado) yield break;
            yield return Transicao(EtapaMare.Cheia, tempoCheia / ritmo, p => { });
            if (cancelado) yield break;
            yield return Transicao(EtapaMare.Baixando, tempoDescida / ritmo, p => DefinirNivel(Mathf.Lerp(nivelMaximo, NivelInicial, p)));
            if (cancelado) yield break;
            yield return Transicao(EtapaMare.Emergindo, tempoRetorno / ritmo, p => { MergulhoNormalizado = 1 - p; PreparacaoNormalizada = 1 - p; });
        }
        finally { if (!EmFuga) RestaurarAmbiente(); }
    }
    public bool IniciarSubidaDeFuga(float nivelDestino, float duracao)
    {
        if (!iniciado || !isActiveAndEnabled || EmFuga || nivelDestino <= NivelAtual) return false;
        CancelarERestaurar();
        EmFuga = true; EmExecucao = true; Fase = EtapaMare.Subindo;
        inicioFuga = Time.time; duracaoFuga = Mathf.Max(1, duracao);
        nivelInicioFuga = NivelAtual; nivelFimFuga = nivelDestino;
        TempoRestante = duracaoFuga; proximoDano = Time.time;
        return true;
    }
    IEnumerator Transicao(EtapaMare fase, float duracao, Action<float> atualizar)
    {
        Fase = fase;
        for (float t = 0; t < duracao && !cancelado && isActiveAndEnabled; t += Time.deltaTime)
        {
            TempoRestante = Mathf.Max(0, duracao - t);
            atualizar(Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / duracao)));
            yield return null;
        }
        if (!cancelado && isActiveAndEnabled) atualizar(1);
        TempoRestante = 0;
    }
    FlutuacaoSimples[] plataformasFlutuantes;
    void DefinirNivel(float y)
    {
        if (!superficieAgua) return;
        float anterior = NivelAtual;
        superficieAgua.position = posicaoInicial + Vector3.up * (y - NivelInicial);
        AtualizarPlataformas(anterior, NivelAtual);
    }
    void AtualizarPlataformas(float anterior, float nivel)
    {
        if (plataformasFlutuantes == null)
            plataformasFlutuantes = FindObjectsByType<FlutuacaoSimples>(FindObjectsSortMode.None);
        LocalizarJogador();
        foreach (var plataforma in plataformasFlutuantes)
            if (plataforma && plataforma.isActiveAndEnabled && plataforma.planoDaAgua == superficieAgua)
                plataforma.AcompanharSubida(anterior, nivel, capsula);
    }
    void LocalizarJogador()
    {
        if (jogador) return;
        jogador = FindAnyObjectByType<ThirdPersonController>();
        if (!jogador) return;
        capsula = jogador.GetComponent<CharacterController>();
        vida = jogador.GetComponent<VidaJogadorKraken>();
        if (!vida) vida = jogador.gameObject.AddComponent<VidaJogadorKraken>();
    }
    void Update()
    {
        if (!iniciado || Time.timeScale <= 0f) return;
        LocalizarJogador();
        // Corrige a flutuacao antes de avaliar dano, inclusive antes da primeira mare.
        AtualizarPlataformas(NivelAtual, NivelAtual);
        if (EmFuga)
        {
            float progresso = Mathf.Clamp01((Time.time - inicioFuga) / duracaoFuga);
            DefinirNivel(Mathf.Lerp(nivelInicioFuga, nivelFimFuga, progresso));
            TempoRestante = Mathf.Max(0, duracaoFuga - (Time.time - inicioFuga));
            Fase = progresso < 1 ? EtapaMare.Subindo : EtapaMare.Cheia;
        }
        bool dentro = false;
        float profundidade = 0, profundidadeNoMar = 0, altura = 2;
        if (jogador && capsula && vida && !vida.Morto)
        {
            Vector3 pes = jogador.transform.TransformPoint(capsula.center - Vector3.up * capsula.height * .5f);
            altura = capsula.height * Mathf.Abs(jogador.transform.lossyScale.y);
            for (int i = 0; i < aguasDaFase.Count; i++)
            {
                var agua = aguasDaFase[i];
                if (!agua || !agua.enabled || !agua.gameObject.activeInHierarchy) continue;
                var area = agua.bounds;
                if (pes.x < area.min.x || pes.x > area.max.x || pes.z < area.min.z || pes.z > area.max.z) continue;
                dentro = true;
                profundidade = Mathf.Max(profundidade, area.center.y - pes.y);
                profundidadeNoMar = Mathf.Max(profundidadeNoMar, niveisDeRepouso[i] - pes.y);
            }
        }
        // Cair no mar original do buraco mata imediatamente. A mare sobre as
        // plataformas conserva seu dano gradual durante o ataque de inundacao.
        bool sendoCarregado = jogador && jogador.GetComponent<StarterAssets.ThirdPersonController>() &&
            FindAnyObjectByType<CapturaJogadorKraken>() is CapturaJogadorKraken captura && captura.Ativa;
        if (dentro && profundidadeNoMar > .05f && vida && !vida.Morto && !sendoCarregado)
        {
            JogadorNaAgua = false;
            if (jogador) jogador.DefinirMultiplicadorAgua(1);
            vida.Morrer();
            return;
        }
        JogadorNaAgua = AguaCausaDano && dentro && profundidade > profundidadeParaLentidao;
        if (jogador) jogador.DefinirMultiplicadorAgua(JogadorNaAgua ? velocidadeNaAgua : 1);
        if (AguaCausaDano && dentro && profundidade > profundidadeParaDano && Time.time >= proximoDano)
        {
            if (vida.ReceberDano(danoAgua, true)) proximoDano = Time.time + intervaloDano;
        }
        AtualizarOndas();
    }
    void AtualizarOndas()
    {
        if (!ondaMergulho || !centroKraken) return;
        bool mostrar = Fase == EtapaMare.Mergulhando || Fase == EtapaMare.Emergindo || Fase == EtapaMare.Avisando;
        ondaMergulho.enabled = EmExecucao && mostrar;
        if (!ondaMergulho.enabled) return;
        float raio = 16 + Mathf.Repeat(Time.time * 13f, 32f);
        for (int i = 0; i < ondaMergulho.positionCount; i++)
        {
            float a = i * Mathf.PI * 2 / ondaMergulho.positionCount;
            ondaMergulho.SetPosition(i, new Vector3(centroKraken.position.x + Mathf.Cos(a)*raio, NivelAtual+.16f, centroKraken.position.z + Mathf.Sin(a)*raio));
        }
    }
    void MostrarRefugios(bool mostrar) { if (marcasRefugios != null) foreach (var marca in marcasRefugios) if (marca) marca.SetActive(false); }
    void RestaurarAmbiente()
    {
        if (!iniciado) return;
        DefinirNivel(NivelInicial);
        MergulhoNormalizado = 0; PreparacaoNormalizada = 0;
        EmExecucao = false; Fase = EtapaMare.Repouso; TempoRestante = 0;
        JogadorNaAgua = false;
        if (jogador) jogador.DefinirMultiplicadorAgua(1);
        MostrarRefugios(false);
        if (ondaMergulho) ondaMergulho.enabled = false;
    }
    public void CancelarERestaurar() { EmFuga = false; cancelado = true; pendentes = 0; RestaurarAmbiente(); }
    void OnDisable() { CancelarERestaurar(); }
    void OnGUI()
    {
        if (!JogadorNaAgua) return;
        Color antes = GUI.color;
        GUI.color = new Color(.1f, .6f, .8f, .12f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = antes;
    }
}
