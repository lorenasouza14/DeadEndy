using System;
using System.Collections;
using UnityEngine;
using StarterAssets;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-30)]
public class InundacaoKraken : MonoBehaviour
{
    public enum EtapaMare { Repouso, Avisando, Mergulhando, Subindo, Cheia, Baixando, Emergindo }
    [Header("Referencias")]
    public Transform superficieAgua;
    public Renderer malhaAgua;
    public GameObject[] marcasRefugios;
    public Transform[] pontosSeguros;
    public LineRenderer ondaMergulho;
    public Transform centroKraken;
    [Header("Tempos para subir nas rochas")]
    [Min(.1f)] public float tempoAviso = 7f;
    [Min(.1f)] public float tempoMergulho = 3f;
    [Min(.1f)] public float tempoSubida = 4f;
    [Min(.1f)] public float tempoCheia = 8f;
    [Min(.1f)] public float tempoDescida = 4f;
    [Min(.1f)] public float tempoRetorno = 3f;
    [Header("Alturas mundiais")]
    public float nivelMaximo = -6f;
    [Min(1)] public float profundidadeMergulho = 125f;
    [Header("Agua perigosa durante a inundacao")]
    [Min(1)] public int danoAgua = 8;
    [Min(.85f)] public float intervaloDano = 1.1f;
    [Range(.1f, 1)] public float velocidadeNaAgua = .55f;
    [Min(0)] public float profundidadeParaLentidao = .3f;
    [Range(.1f, 1)] public float fracaoDaCapsulaParaDano = .55f;

    public EtapaMare Fase { get; private set; }
    public bool EmExecucao { get; private set; }
    public bool Pendente => isActiveAndEnabled && pendentes > 0;
    public int SolicitacoesPendentes => pendentes;
    public float MergulhoNormalizado { get; private set; }
    public float PreparacaoNormalizada { get; private set; }
    public float TempoRestante { get; private set; }
    public float NivelInicial { get; private set; }
    public float NivelAtual => NivelInicial + (superficieAgua ? superficieAgua.position.y - posicaoInicial.y : 0);
    public bool JogadorNaAgua { get; private set; }
    public bool AguaCausaDano => EmExecucao && (Fase == EtapaMare.Subindo || Fase == EtapaMare.Cheia || Fase == EtapaMare.Baixando);
    public string Mensagem
    {
        get
        {
            if (Fase == EtapaMare.Avisando) return "O KRAKEN VAI AFUNDAR!\nSuba nas rochas marcadas: " + Mathf.CeilToInt(TempoRestante) + "s";
            if (Fase == EtapaMare.Mergulhando) return "ELE ESTA MERGULHANDO!\nSuba nas rochas: a agua vai subir!";
            if (Fase == EtapaMare.Subindo) return "A AGUA ESTA SUBINDO!\nFique no alto das rochas marcadas.";
            if (Fase == EtapaMare.Cheia) return "AGUA PERIGOSA! Fique nas rochas.\nA cheia dura mais " + Mathf.CeilToInt(TempoRestante) + "s";
            if (Fase == EtapaMare.Baixando) return "A agua esta baixando.\nEspere para descer das rochas.";
            if (Fase == EtapaMare.Emergindo) return "O Kraken esta voltando!";
            return "";
        }
    }
    Vector3 posicaoInicial;
    Bounds limites;
    int pendentes;
    bool cancelado, iniciado;
    float proximoDano;
    ThirdPersonController jogador;
    VidaJogadorKraken vida;
    CharacterController capsula;

    void Awake()
    {
        if (!superficieAgua || !malhaAgua) { enabled = false; return; }
        posicaoInicial = superficieAgua.position;
        NivelInicial = malhaAgua.bounds.center.y;
        limites = malhaAgua.bounds;
        iniciado = true;
        MostrarRefugios(false);
        if (ondaMergulho) ondaMergulho.enabled = false;
    }
    public void Solicitar()
    {
        if (isActiveAndEnabled && iniciado) pendentes++;
    }
    public IEnumerator Executar()
    {
        if (!iniciado || !isActiveAndEnabled || EmExecucao || pendentes <= 0) yield break;
        pendentes--; cancelado = false; EmExecucao = true; proximoDano = Time.time;
        MostrarRefugios(true);
        try
        {
            yield return Transicao(EtapaMare.Avisando, tempoAviso, p => PreparacaoNormalizada = p);
            if (cancelado) yield break;
            yield return Transicao(EtapaMare.Mergulhando, tempoMergulho, p => MergulhoNormalizado = p);
            if (cancelado) yield break;
            yield return Transicao(EtapaMare.Subindo, tempoSubida, p => DefinirNivel(Mathf.Lerp(NivelInicial, nivelMaximo, p)));
            if (cancelado) yield break;
            yield return Transicao(EtapaMare.Cheia, tempoCheia, p => { });
            if (cancelado) yield break;
            yield return Transicao(EtapaMare.Baixando, tempoDescida, p => DefinirNivel(Mathf.Lerp(nivelMaximo, NivelInicial, p)));
            if (cancelado) yield break;
            yield return Transicao(EtapaMare.Emergindo, tempoRetorno, p => { MergulhoNormalizado = 1 - p; PreparacaoNormalizada = 1 - p; });
        }
        finally { RestaurarAmbiente(); }
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
    void DefinirNivel(float y)
    {
        if (superficieAgua) superficieAgua.position = posicaoInicial + Vector3.up * (y - NivelInicial);
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
        if (!iniciado) return;
        LocalizarJogador();
        bool dentro = false;
        float profundidade = 0, altura = 2;
        if (jogador && capsula && vida && !vida.Morto)
        {
            Vector3 pes = jogador.transform.TransformPoint(capsula.center - Vector3.up * capsula.height * .5f);
            altura = capsula.height * Mathf.Abs(jogador.transform.lossyScale.y);
            dentro = pes.x >= limites.min.x && pes.x <= limites.max.x && pes.z >= limites.min.z && pes.z <= limites.max.z;
            profundidade = NivelAtual - pes.y;
        }
        JogadorNaAgua = AguaCausaDano && dentro && profundidade > profundidadeParaLentidao;
        if (jogador) jogador.DefinirMultiplicadorAgua(JogadorNaAgua ? velocidadeNaAgua : 1);
        if (AguaCausaDano && dentro && profundidade > altura * fracaoDaCapsulaParaDano && Time.time >= proximoDano)
        {
            if (vida.ReceberDano(danoAgua)) proximoDano = Time.time + intervaloDano;
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
    void MostrarRefugios(bool mostrar) { if (marcasRefugios != null) foreach (var marca in marcasRefugios) if (marca) marca.SetActive(mostrar); }
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
    public void CancelarERestaurar() { cancelado = true; pendentes = 0; RestaurarAmbiente(); }
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
