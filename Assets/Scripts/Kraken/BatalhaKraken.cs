using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using StarterAssets;

[DisallowMultipleComponent]
public class BatalhaKraken : MonoBehaviour
{
    public enum Etapa { Aguardando, Mirando, Avisando, Golpeando, Recuperando, ToucaCaindo, Derrotado, Inundando }
    [Header("Referencias da fase 4")]
    public Transform origemTentaculo;
    public TentaculoAtaqueKraken tentaculo;
    public PilarKraken[] pilares;
    public ToucaKraken touca;
    public InundacaoKraken inundacao;
    public Material materialAviso;
    [Header("Ataque - unidades do mundo")]
    [Min(0)] public float esperaInicial = 3f;
    [Min(.1f)] public float tempoMirando = .6f;
    [Min(.1f)] public float tempoParaDesviar = 1.4f;
    [Min(.1f)] public float duracaoGolpe = .24f;
    [Min(.1f)] public float intervalo = 2.2f;
    [Min(.1f)] public float raioAtaque = 5f;
    [Min(1)] public int dano = 25;
    [Min(1)] public float alcance = 180f;
    public UnityEvent aoDerrotar = new UnityEvent();
    public Etapa Estado { get; private set; }
    public int PilaresQuebrados => quebrados.Count;
    public Vector3 PontoDoGolpe { get; private set; }
    public int ImpactosExecutados { get; private set; }
    readonly HashSet<PilarKraken> quebrados = new HashSet<PilarKraken>();
    ThirdPersonController jogador;
    VidaJogadorKraken vida;
    LineRenderer aviso;
    Vector3 ponta;
    bool vitoria;
    GUIStyle texto;

    void Start()
    {
        var go = new GameObject("Aviso do golpe - circulo vermelho");
        go.transform.SetParent(transform, false);
        aviso = go.AddComponent<LineRenderer>();
        aviso.sharedMaterial = materialAviso;
        aviso.loop = true; aviso.useWorldSpace = true; aviso.positionCount = 64;
        aviso.widthMultiplier = .22f; aviso.enabled = false;
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
        return jogador && vida && !vida.Morto && Vector3.Distance(jogador.transform.position, origemTentaculo.position) < alcance;
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
        yield return new WaitForSeconds(esperaInicial);
        while (!vitoria)
        {
            if (inundacao && inundacao.Pendente)
            {
                aviso.enabled = false;
                ponta = origemTentaculo.position + Vector3.up * 3f;
                Estado = Etapa.Inundando;
                yield return inundacao.Executar();
                if (vitoria) break;
                Estado = Etapa.Aguardando;
                yield return new WaitForSeconds(1.5f);
                continue;
            }
            if (!PodeAtacar()) { yield return null; continue; }
            Estado = Etapa.Mirando; aviso.enabled = true;
            for (float t = 0; t < tempoMirando && !vitoria; t += Time.deltaTime)
            {
                if (!PodeAtacar()) break;
                PontoDoGolpe = Chao(jogador.transform.position);
                ponta = Vector3.Lerp(ponta, PontoDoGolpe + Vector3.up * 16f, Time.deltaTime * 8f);
                AtualizarAviso(); yield return null;
            }
            if (vitoria) break;
            if (!PodeAtacar()) { aviso.enabled = false; Estado = Etapa.Aguardando; continue; }
            // O alvo fica TRAVADO. O golpe nao persegue o jogador durante a esquiva.
            Estado = Etapa.Avisando;
            Vector3 alto = PontoDoGolpe + Vector3.up * 16f;
            ponta = alto;
            yield return new WaitForSeconds(tempoParaDesviar);
            if (vitoria) break;
            if (!PodeAtacar()) { aviso.enabled = false; continue; }
            Estado = Etapa.Golpeando;
            for (float t = 0; t < duracaoGolpe && !vitoria; t += Time.deltaTime)
            {
                ponta = Vector3.Lerp(alto, PontoDoGolpe + Vector3.up * .6f, Mathf.Clamp01(t / duracaoGolpe));
                yield return null;
            }
            if (vitoria) break;
            ponta = PontoDoGolpe + Vector3.up * .6f;
            AplicarImpacto(PontoDoGolpe);
            aviso.enabled = false;
            if (vitoria) break;
            if (inundacao && inundacao.Pendente) continue;
            Estado = Etapa.Recuperando;
            Vector3 baixo = ponta;
            for (float t = 0; t < intervalo && !vitoria; t += Time.deltaTime)
            {
                ponta = Vector3.Lerp(baixo, origemTentaculo.position + Vector3.up * 4, Mathf.Clamp01(t / intervalo));
                yield return null;
            }
        }
        aviso.enabled = false;
        if (inundacao) inundacao.CancelarERestaurar();
        Estado = Etapa.ToucaCaindo;
        if (touca) { touca.Soltar(); while (touca && !touca.Encaixou) yield return null; }
        Estado = Etapa.Derrotado;
        aoDerrotar.Invoke();
    }
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
        foreach (var v in vidas) v.ReceberDano(dano);
        foreach (var p in suportes) p.ReceberGolpe(origemTentaculo.position);
    }
    public void RegistrarPilar(PilarKraken pilar)
    {
        if (!pilar || System.Array.IndexOf(pilares, pilar) < 0 || !quebrados.Add(pilar)) return;
        if (quebrados.Count >= pilares.Length)
        {
            vitoria = true;
            if (inundacao) inundacao.CancelarERestaurar();
        }
        else if (inundacao) inundacao.Solicitar();
    }
    void LateUpdate()
    {
        if (tentaculo)
        {
            var visual = tentaculo.GetComponent<Renderer>();
            if (visual) visual.enabled = !inundacao || !inundacao.EmExecucao || inundacao.MergulhoNormalizado < .25f;
        }
        if (tentaculo && origemTentaculo) tentaculo.Desenhar(origemTentaculo.position, ponta, Estado == Etapa.Golpeando ? 10f : 25f);
    }
    void OnGUI()
    {
        if (texto == null) texto = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
        bool mare = inundacao && inundacao.EmExecucao;
        GUI.Box(new Rect(16, 16, 440, mare ? 190 : 130), "");
        string mensagem = Estado == Etapa.Derrotado ? "Kraken derrotado! A touquinha caiu." : Estado == Etapa.ToucaCaindo ? "As correntes se soltaram!" : Estado == Etapa.Avisando || Estado == Etapa.Golpeando ? "DESVIE! Saia do circulo vermelho." : "Atraia o golpe para perto de um pilar.";
        if (mare) mensagem = inundacao.Mensagem + (inundacao.JogadorNaAgua ? "\nNa agua: movimento reduzido!" : "");
        GUI.Label(new Rect(28, 24, 415, mare ? 170 : 110), "VIDA: " + (vida ? vida.VidaAtual : 100) + " / " + (vida ? vida.vidaMaxima : 100) + "\nPILARES: " + quebrados.Count + " / " + pilares.Length + "\n" + mensagem, texto);
    }
}
