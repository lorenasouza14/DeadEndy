using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using StarterAssets;

[DisallowMultipleComponent]
public class BatalhaKraken : MonoBehaviour
{
    public enum Etapa { Aguardando, Mirando, Avisando, Golpeando, Recuperando, ToucaCaindo, Derrotado, Inundando, Capturando, Sabao }
    [Header("Referencias da fase 4")]
    public Transform origemTentaculo;
    public TentaculoAtaqueKraken tentaculo;
    public AtaqueTentaculoRigKraken tentaculoDoCorpo;
    public CapturaJogadorKraken captura;
    public PilarKraken[] pilares;
    public ToucaKraken touca;
    public InundacaoKraken inundacao;
    public Material materialAviso;
    public AtaqueSabaoKraken sabao;
    [Min(1)] public int golpesEntreSabao = 3;
    [Header("Captura garantida apos os golpes")]
    [Min(1)] public int golpesEntreCapturas = 4;
    int golpesDesdeSabao, golpesDesdeCaptura;
    public bool CapturaPendente => golpesDesdeCaptura >= Mathf.Max(1, golpesEntreCapturas);
    [Header("Aceleracao gradual por pilar derrubado")]
    [Range(0, .2f)] public float aumentoVelocidadePorPilar = .08f;
    public float MultiplicadorVelocidade => 1f + Mathf.Clamp(aumentoVelocidadePorPilar, 0, .2f)
        * Mathf.Min(PilaresQuebrados, pilares != null ? Mathf.Max(0, pilares.Length - 1) : 0);
    // A captura conserva o ritmo original; apenas os outros ataques aceleram.
    public float MultiplicadorVelocidadeAtaques => 1.35f * MultiplicadorVelocidade;
    public float DuracaoAtaque(float duracao) => Mathf.Max(0, duracao) / MultiplicadorVelocidadeAtaques;
    [Header("Ataque - unidades do mundo")]
    [Min(0)] public float esperaInicial = 3f;
    [Min(.1f)] public float tempoMirando = .6f;
    [Min(.1f)] public float tempoParaDesviar = 1.4f;
    [Min(.1f)] public float duracaoGolpe = .24f;
    [Min(.1f)] public float intervalo = 2.2f;
    [Min(.1f)] public float raioAtaque = 5f;
    [Min(1)] public int dano = 25;
    [Min(1)] public float alcance = 180f;
    [Header("Desaparecimento apos os quatro pilares")]
    [Min(0)] public float tempoAposVitoria = 3f;
    public GameObject krakenVisual;
    public GameObject objetoParaOcultarNaVitoria;
    [Header("Tremor do desmoronamento apos a vitoria")]
    [Min(0)] public float deslocamentoTremor = .045f;
    [Min(0)] public float rotacaoTremor = .25f;
    [Min(.1f)] public float frequenciaTremor = 10f;
    [Min(0)] public float entradaSuaveTremor = 1.2f;
    public UnityEvent aoDerrotar = new UnityEvent();
    public Etapa Estado { get; private set; }
    public int PilaresQuebrados => quebrados.Count;
    public Vector3 PontoDoGolpe { get; private set; }
    public Vector3 PontoDeContato { get; private set; }
    public int ImpactosExecutados { get; private set; }
    public float ProgressoDoGolpe { get; private set; }
    readonly HashSet<PilarKraken> quebrados = new HashSet<PilarKraken>();
    ThirdPersonController jogador;
    VidaJogadorKraken vida;
    LineRenderer aviso;
    Vector3 ponta;
    bool vitoria, desapareceu;
    GUIStyle texto;
    [SerializeField] Font fonteInterface;
    readonly List<TremorDesmoronamento> tremores = new List<TremorDesmoronamento>();

    void Start()
    {
        var go = new GameObject("Aviso do golpe - alvo cinza escuro");
        go.transform.SetParent(transform, false);
        aviso = go.AddComponent<LineRenderer>();
        aviso.sharedMaterial = materialAviso;
        aviso.loop = true; aviso.useWorldSpace = true; aviso.positionCount = 128;
        aviso.widthMultiplier = .22f; aviso.enabled = false;
        IndicadoresSuavesKraken.Aplicar(aviso);
        ponta = origemTentaculo.position + Vector3.up * 4f;
        StartCoroutine(Lutar());
    }
    void LocalizarJogador()
    {
        if (jogador) return;
        jogador = FindAnyObjectByType<ThirdPersonController>();
        if (jogador)
        {
            vida = jogador.GetComponent<VidaJogadorKraken>();
            if (!vida) vida = jogador.gameObject.AddComponent<VidaJogadorKraken>();
        }
    }
    bool PodeAtacar()
    {
        LocalizarJogador();
        return jogador && vida && !vida.Morto && (!captura || !captura.Reagindo) && Vector3.Distance(jogador.transform.position, origemTentaculo.position) < alcance;
    }
    Vector3 Chao(Vector3 p)
    {
        // O raio parte perto dos pes para nao escolher o teto de uma caverna.
        var hits = Physics.RaycastAll(p + Vector3.up * 2f, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore);
        float distancia = float.PositiveInfinity;
        Vector3 resultado = p;
        foreach (var h in hits)
        {
            if (h.collider.GetComponentInParent<ThirdPersonController>() || h.normal.y < .5f) continue;
            if (h.distance < distancia) { distancia = h.distance; resultado = h.point; }
        }
        return resultado;
    }
    void AtualizarAviso()
    {
        for (int i = 0; i < aviso.positionCount; i++)
        {
            float a = i * Mathf.PI * 2 / aviso.positionCount;
            Vector3 p = PontoDoGolpe + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * raioAtaque;
            aviso.SetPosition(i, Chao(p) + Vector3.up * .12f);
        }
    }
    IEnumerator Lutar()
    {
        Estado = Etapa.Aguardando;
        yield return new WaitForSeconds(DuracaoAtaque(esperaInicial));
        while (!vitoria)
        {
            // A animacao de um especial e a reacao da captura bloqueiam as batidas.
            if ((captura && (captura.Ativa || captura.Reagindo))
                || (sabao && sabao.EmExecucao) || (inundacao && inundacao.EmExecucao))
            { yield return null; continue; }
            bool bolhasNoCenario = sabao && sabao.BolhasAtivas > 0;
            if (!bolhasNoCenario && inundacao && inundacao.Pendente)
            {
                aviso.enabled = false;
                ponta = origemTentaculo.position + Vector3.up * 3f;
                Estado = Etapa.Inundando;
                yield return inundacao.Executar(MultiplicadorVelocidadeAtaques);
                if (vitoria) break;
                Estado = Etapa.Aguardando;
                yield return new WaitForSeconds(DuracaoAtaque(1.5f));
                continue;
            }
            LocalizarJogador();
            if (!jogador || !vida || vida.Morto) { yield return null; continue; }
            if (!bolhasNoCenario && sabao && sabao.isActiveAndEnabled && golpesDesdeSabao >= golpesEntreSabao)
            {
                Estado = Etapa.Sabao; aviso.enabled = false;
                yield return sabao.Executar(jogador, vida);
                if (vitoria) break;
                golpesDesdeSabao = 0;
                Estado = Etapa.Recuperando;
                yield return new WaitForSeconds(DuracaoAtaque(1f));
                continue;
            }
            // A captura independe de acerto, dano, esquiva e alcance.
            // As bolhas bloqueiam os especiais, sem perder os grupos de quatro golpes.
            if (!bolhasNoCenario && CapturaPendente)
            {
                aviso.enabled = false;
                Estado = Etapa.Aguardando;
                if (!captura || !captura.TentarCapturar(vida)) { yield return null; continue; }
                golpesDesdeCaptura -= Mathf.Max(1, golpesEntreCapturas);
                Estado = Etapa.Capturando;
                while ((captura.Ativa || captura.Reagindo) && !vitoria && !vida.Morto) yield return null;
                if (vitoria) captura.Cancelar();
                if (vitoria || vida.Morto) yield break;
                Estado = Etapa.Recuperando;
                yield return new WaitForSeconds(Mathf.Max(0, intervalo) / MultiplicadorVelocidade);
                continue;
            }
            if (!PodeAtacar()) { yield return null; continue; }
            Estado = Etapa.Mirando; aviso.enabled = true;
            for (float t = 0; t < tempoMirando && !vitoria; t += Time.deltaTime * MultiplicadorVelocidadeAtaques)
            {
                if (!PodeAtacar()) break;
                PontoDoGolpe = Chao(jogador.transform.position);
                var capsulaAlvo = jogador.GetComponent<CharacterController>();
                PontoDeContato = capsulaAlvo != null ? capsulaAlvo.bounds.center : jogador.transform.position + Vector3.up;
                ponta = Vector3.Lerp(ponta, PontoDoGolpe + Vector3.up * 16f, Time.deltaTime * 8f);
                AtualizarAviso(); yield return null;
            }
            if (vitoria) break;
            if (!PodeAtacar()) { aviso.enabled = false; Estado = Etapa.Aguardando; continue; }
            // O alvo fica TRAVADO. O golpe nao persegue o jogador durante a esquiva.
            Estado = Etapa.Avisando;
            Vector3 alto = PontoDoGolpe + Vector3.up * 16f;
            ponta = alto;
            yield return new WaitForSeconds(DuracaoAtaque(tempoParaDesviar));
            if (vitoria) break;
            if (!PodeAtacar()) { aviso.enabled = false; continue; }
            Estado = Etapa.Golpeando;
            impactoNosPilaresAplicado = false;
            ProgressoDoGolpe = 0;
            for (float t = 0; t < duracaoGolpe && !vitoria; t += Time.deltaTime * MultiplicadorVelocidadeAtaques)
            {
                ProgressoDoGolpe = Mathf.Clamp01(t / duracaoGolpe);
                ponta = Vector3.Lerp(alto, PontoDoGolpe + Vector3.up * .6f, ProgressoDoGolpe);
                yield return null;
            }
            if (vitoria) break;
            ProgressoDoGolpe = 1;
            ponta = PontoDoGolpe + Vector3.up * .6f;
            AplicarImpacto(PontoDoGolpe);
            golpesDesdeSabao++;
            golpesDesdeCaptura++;
            aviso.enabled = false;
            if (vitoria) break;
            if (inundacao && inundacao.Pendente && (!sabao || sabao.BolhasAtivas == 0)) continue;
            Estado = Etapa.Recuperando;
            Vector3 baixo = ponta;
            for (float t = 0; t < intervalo && !vitoria; t += Time.deltaTime * MultiplicadorVelocidadeAtaques)
            {
                ponta = Vector3.Lerp(baixo, origemTentaculo.position + Vector3.up * 4, Mathf.Clamp01(t / intervalo));
                yield return null;
            }
        }
    }
    IEnumerator EncerrarBatalha()
    {
        if (sabao) sabao.Cancelar();
        aviso.enabled = false;
        if (inundacao) inundacao.CancelarERestaurar();
        Estado = Etapa.ToucaCaindo;
        if (captura) captura.Cancelar();
        if (touca) touca.Soltar();
        // A espera tem duracao fixa, mesmo se a touca nao conseguir encaixar.
        yield return new WaitForSeconds(tempoAposVitoria);
        desapareceu = true;
        if (tentaculo) tentaculo.gameObject.SetActive(false);
        if (touca) touca.gameObject.SetActive(false);
        if (krakenVisual) krakenVisual.SetActive(false);
        if (objetoParaOcultarNaVitoria) objetoParaOcultarNaVitoria.SetActive(false);
        Estado = Etapa.Derrotado;
        IniciarDesmoronamento();
        aoDerrotar.Invoke();
    }
    void IniciarDesmoronamento()
    {
        foreach (var raiz in gameObject.scene.GetRootGameObjects())
            foreach (var camera in raiz.GetComponentsInChildren<Unity.Cinemachine.CinemachineCamera>(true))
            {
                var tremor = camera.GetComponent<TremorDesmoronamento>();
                if (!tremor) tremor = camera.gameObject.AddComponent<TremorDesmoronamento>();
                tremor.Iniciar(deslocamentoTremor, rotacaoTremor, frequenciaTremor, entradaSuaveTremor);
                tremores.Add(tremor);
            }
    }
    void OnDisable()
    {
        if (sabao) sabao.Cancelar();
        foreach (var tremor in tremores) if (tremor) tremor.Parar();
        tremores.Clear();
    }
    bool impactoNosPilaresAplicado;
    public void AplicarImpacto(Vector3 ponto)
    {
        if (!Application.isPlaying || vitoria || Estado == Etapa.Inundando) return;
        ImpactosExecutados++;
        var atingidos = Physics.OverlapSphere(ponto + Vector3.up * 1.4f, raioAtaque, ~0, QueryTriggerInteraction.Ignore);
        var vidas = new HashSet<VidaJogadorKraken>();
        var suportes = new HashSet<PilarKraken>();
        foreach (var c in atingidos)
        {
            var v = c.GetComponentInParent<VidaJogadorKraken>(); if (v) vidas.Add(v);
            var p = c.GetComponentInParent<PilarKraken>(); if (p) suportes.Add(p);
        }
        if (tentaculoDoCorpo)
        {
            // A esquiva e o raio de dano continuam iguais. So corrige a ponta visual
            // para encostar no corpo de quem ja foi atingido pela area do golpe.
            VidaJogadorKraken alvoContato = null;
            float menorDistancia = float.PositiveInfinity;
            foreach (var v in vidas)
            {
                float distancia = (v.transform.position-ponto).sqrMagnitude;
                if (distancia < menorDistancia) { menorDistancia=distancia; alvoContato=v; }
            }
            if (alvoContato != null)
            {
                var capsula = alvoContato.GetComponent<CharacterController>();
                Vector3 contato = capsula != null ? capsula.bounds.center : alvoContato.transform.position + Vector3.up;
                tentaculoDoCorpo.MarcarImpacto(contato, true);
            }
            else tentaculoDoCorpo.MarcarImpacto(ponto);
        }
        foreach (var v in vidas)
        {
            v.ReceberDano(dano);
        }
        if (Estado == Etapa.Golpeando && ProgressoDoGolpe >= 1f && !impactoNosPilaresAplicado)
        {
            impactoNosPilaresAplicado = true;
            foreach (var p in suportes) p.ReceberGolpe(origemTentaculo.position);
        }
    }
    // No maximo uma mare por golpe, mesmo quando a area toca dois pilares.
    int ultimoImpactoComMare = -1;
    public void RegistrarImpactoPilar(PilarKraken pilar)
    {
        if (vitoria || !pilar || System.Array.IndexOf(pilares, pilar) < 0
            || ultimoImpactoComMare == ImpactosExecutados) return;
        ultimoImpactoComMare = ImpactosExecutados;
        if (inundacao) inundacao.Solicitar();
    }
    public void RegistrarPilar(PilarKraken pilar)
    {
        if (vitoria || !pilar || System.Array.IndexOf(pilares, pilar) < 0 || !quebrados.Add(pilar)) return;
        if (quebrados.Count >= pilares.Length)
        {
            vitoria = true;
            StartCoroutine(EncerrarBatalha());
        }
    }
    void LateUpdate()
    {
        if (desapareceu) return;
        if (tentaculo)
        {
            var visual = tentaculo.GetComponent<Renderer>();
            if (visual) visual.enabled = !inundacao || !inundacao.EmExecucao || inundacao.MergulhoNormalizado < .25f;
        }
        if (tentaculo && origemTentaculo) tentaculo.Desenhar(origemTentaculo.position, ponta, Estado == Etapa.Golpeando ? 10f : 25f);
    }
    void OnGUI()
    {
        if (texto == null) texto = new GUIStyle(GUI.skin.label) { font = fonteInterface, fontSize = 18, wordWrap = true };
        bool mare = inundacao && inundacao.EmExecucao;
        GUI.Box(new Rect(16, 16, 440, mare ? 190 : 130), "");
        string mensagem = Estado == Etapa.Derrotado ? "Kraken derrotado! A touquinha caiu." : Estado == Etapa.ToucaCaindo ? "As correntes se soltaram!" : Estado == Etapa.Avisando || Estado == Etapa.Golpeando ? "DESVIE! Saia do circulo cinza." : "Atraia o golpe para perto de um pilar.";
        if (Estado == Etapa.Sabao) mensagem = "O KRAKEN VAI APERTAR O SABONETE!\nDesvie das bolhas gigantes!";
        if (captura && captura.Ativa) mensagem = captura.Mensagem;
        if (mare) mensagem = inundacao.Mensagem + (inundacao.JogadorNaAgua ? "\nNa agua: movimento reduzido!" : "");
        GUI.Label(new Rect(28, 24, 415, mare ? 170 : 110), "VIDA: " + (vida ? vida.VidaAtual : 100) + " / " + (vida ? vida.vidaMaxima : 100) + "\nPILARES: " + quebrados.Count + " / " + pilares.Length + "\n" + mensagem, texto);
    }
}
