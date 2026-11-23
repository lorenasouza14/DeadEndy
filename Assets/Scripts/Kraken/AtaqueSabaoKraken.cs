using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StarterAssets;

[DisallowMultipleComponent]
[DefaultExecutionOrder(250)]
public class AtaqueSabaoKraken : MonoBehaviour
{
    public BatalhaKraken batalha;
    public Transform tampa, pontoPressao, saidaBolhas;
    public Transform pontaDiscreta, pontoEntradaJogador;
    public Material materialBolha;
    [Min(.01f)] public float cursoPressao = .8f;
    [Min(.1f)] public float tempoAproximacao = 1.4f, tempoPressao = .65f, tempoRetorno = 1f;
    [Min(1)] public int quantidadeBolhas = 15;
    [Min(.1f)] public float raioBolha = 4f, velocidadeBolha = 10.5f;
    [Min(.1f)] public float alturaArco = 9f;
    [Header("Plataformas onde as bolhas pousam")]
    public Transform[] chaoDasBolhas;
    [Min(0)] public float variacaoDoPouso = 12f;
    [Min(0)] public float desvioLateralBolhas = 1.4f;
    SuperficiesPousoBolhas superficies;
    public bool TemChaoDefinido => chaoDasBolhas != null && chaoDasBolhas.Length > 0;
    public SuperficiesPousoBolhas Superficies
    {
        get { if (superficies == null) superficies = new SuperficiesPousoBolhas(chaoDasBolhas); return superficies; }
    }
    [Tooltip("Superficies voltadas para cima; paredes, gatilhos, jogador e Kraken sao ignorados.")]
    public LayerMask camadasChao = Physics.DefaultRaycastLayers & ~(1 << 4);
    [Min(1)] public float vidaDaBolha = 28;
    [Min(.1f)] public float duracaoEmbacamento = 5f;
    [Min(.1f)] public float duracaoEscorregadio = 10f;
    public bool EmExecucao { get; private set; }
    public int BolhasAtivas
    {
        get
        {
            bolhas.RemoveAll(b => !b || !b.gameObject.activeInHierarchy);
            return bolhas.Count;
        }
    }
    public float AlturaDoVoo { get; private set; }
    public float ajusteAlturaBolhas = -1.5f;
    public bool PodeManterBolhas => isActiveAndEnabled && batalha && batalha.isActiveAndEnabled
        && batalha.Estado != BatalhaKraken.Etapa.Derrotado && batalha.Estado != BatalhaKraken.Etapa.ToucaCaindo;
    public int BolhasLancadas { get; private set; }
    readonly List<BolhaSabaoKraken> bolhas = new List<BolhaSabaoKraken>();
    Vector3 tampaInicial;
    Transform baseBraco, dobraBraco;
    Vector3 contatoNaDobra;
    Quaternion baseSemGesto, dobraSemGesto;
    bool cancelado, gestoAplicado, alturaPreparada;
    bool proximaRajadaComAcertoGarantido;
    float pesoGesto;
    void Awake() { if (tampa) tampaInicial = tampa.localPosition; }
    void Start()
    {
        var jogador = FindAnyObjectByType<ThirdPersonController>();
        if (jogador) PrepararAltura(jogador);
        PrepararBraco();
    }
    void PrepararAltura(ThirdPersonController jogador)
    {
        if (alturaPreparada) return;
        var capsula = jogador.GetComponent<CharacterController>();
        float centro = capsula ? jogador.transform.TransformVector(capsula.center).y : 1;
        AlturaDoVoo = (pontoEntradaJogador ? pontoEntradaJogador.position.y : jogador.transform.position.y) + centro + ajusteAlturaBolhas;
        alturaPreparada = true;
    }
    void PrepararBraco()
    {
        var golpe = batalha ? batalha.tentaculoDoCorpo : null;
        if (!pontaDiscreta || !golpe || !golpe.nado || golpe.nado.tentaculos == null) return;
        var bracos = golpe.nado.tentaculos;
        for (int i = 0; i < bracos.Length; i++)
        {
            var ossos = bracos[i].ossos;
            if (i == golpe.indiceDoTentaculo || ossos == null || System.Array.IndexOf(ossos, pontaDiscreta) < 0) continue;
            var ultimo = ossos[ossos.Length - 1];
            Transform marcador = null;
            for (int j = 0; j < ultimo.childCount; j++)
                if (ultimo.GetChild(j).name.EndsWith("_end")) { marcador = ultimo.GetChild(j); break; }
            if (!marcador) return;
            baseBraco = ossos[0];
            dobraBraco = ossos[1];
            contatoNaDobra = dobraBraco.InverseTransformPoint(PontaVisivel(golpe.malhaDoKraken, ultimo, marcador));
            return;
        }
    }

    Vector3 PontaVisivel(SkinnedMeshRenderer pele, Transform ultimo, Transform marcador)
    {
        if (!pele || !pele.sharedMesh || !pele.sharedMesh.isReadable) return marcador.position;
        var malha = pele.sharedMesh;
        var ossos = pele.bones;
        int indiceFinal = System.Array.IndexOf(ossos, ultimo);
        var pesos = malha.boneWeights;
        // O vertice mais influenciado pelo ultimo osso marca a ponta da malha desta fase.
        float maiorPeso = 0;
        int vertice = -1;
        for (int i = 0; i < pesos.Length; i++)
        {
            if (pesos[i].boneIndex0 != indiceFinal || pesos[i].weight0 <= maiorPeso) continue;
            maiorPeso = pesos[i].weight0;
            vertice = i;
        }
        if (vertice < 0) return marcador.position;
        Vector3 ponto = malha.vertices[vertice];
        var poses = malha.bindposes;
        Vector3 Posicao(int indice) => ossos[indice].TransformPoint(poses[indice].MultiplyPoint3x4(ponto));
        var w = pesos[vertice];
        return Posicao(w.boneIndex0) * w.weight0 + Posicao(w.boneIndex1) * w.weight1
            + Posicao(w.boneIndex2) * w.weight2 + Posicao(w.boneIndex3) * w.weight3;
    }

    void RetirarGesto()
    {
        if (!gestoAplicado) return;
        baseBraco.localRotation = baseSemGesto;
        dobraBraco.localRotation = dobraSemGesto;
        gestoAplicado = false;
    }
    void Update() { RetirarGesto(); }

    void LateUpdate()
    {
        if (!EmExecucao || pesoGesto <= 0 || !pontoPressao || !dobraBraco ||
            (batalha.captura && batalha.captura.Ativa)) return;
        baseSemGesto = baseBraco.localRotation;
        dobraSemGesto = dobraBraco.localRotation;
        Vector3 raiz = baseBraco.position;
        Vector3 primeiro = dobraBraco.position - raiz;
        Vector3 ponta = dobraBraco.TransformPoint(contatoNaDobra);
        Vector3 rumo = Vector3.Lerp(ponta, pontoPressao.position, pesoGesto) - raiz;
        float a = primeiro.magnitude, b = Vector3.Distance(ponta, dobraBraco.position);
        if (rumo.sqrMagnitude < .0001f || a < .001f || b < .001f) return;
        Vector3 direcao = rumo.normalized;
        float d = Mathf.Clamp(rumo.magnitude, Mathf.Abs(a - b) + .0001f, a + b);
        float projecao = (a * a - b * b + d * d) / (2f * d);
        Vector3 lado = Vector3.ProjectOnPlane(primeiro, direcao).normalized;
        if (lado.sqrMagnitude < .5f) lado = Vector3.ProjectOnPlane(dobraBraco.right, direcao).normalized;
        // Apenas duas articulacoes conduzem a ponta. A espiral distal acompanha como uma peca.
        Vector3 dobra = direcao * projecao + lado * Mathf.Sqrt(Mathf.Max(0f, a * a - projecao * projecao));
        baseBraco.rotation = Quaternion.FromToRotation(primeiro, dobra) * baseBraco.rotation;
        Vector3 pontaAtual = dobraBraco.TransformPoint(contatoNaDobra) - dobraBraco.position;
        dobraBraco.rotation = Quaternion.FromToRotation(pontaAtual, raiz + direcao * d - dobraBraco.position)
            * dobraBraco.rotation;
        gestoAplicado = true;
    }
    void Apertar(float fracao)
    {
        if (!tampa) return;
        Vector3 deslocamento = -tampa.up * cursoPressao * fracao;
        tampa.localPosition = tampaInicial + (tampa.parent ? tampa.parent.InverseTransformVector(deslocamento) : deslocamento);
    }
    bool Valido(VidaJogadorKraken vida) => !cancelado && PodeManterBolhas && vida && !vida.Morto
        && (!batalha.captura || !batalha.captura.Ativa);
    public IEnumerator Executar(ThirdPersonController jogador, VidaJogadorKraken vida)
    {
        if (EmExecucao || BolhasAtivas > 0 || !tampa || !saidaBolhas || !materialBolha
            || (batalha && ((batalha.captura && (batalha.captura.Ativa || batalha.captura.Reagindo))
                || (batalha.inundacao && batalha.inundacao.EmExecucao)))) yield break;
        PrepararAltura(jogador);
        float ritmo = batalha ? batalha.MultiplicadorVelocidadeAtaques : 1f;
        EmExecucao = true; cancelado = false;
        try
        {
            for (float t = 0; t < tempoAproximacao; t += Time.deltaTime * ritmo)
            {
                if (!Valido(vida)) yield break;
                pesoGesto = Mathf.SmoothStep(0, 1, t / tempoAproximacao); yield return null;
            }
            for (float t = 0; t < tempoPressao; t += Time.deltaTime * ritmo)
            {
                if (!Valido(vida)) yield break;
                float p = Mathf.SmoothStep(0, 1, t / tempoPressao);
                Apertar(p); pesoGesto = 1f; yield return null;
            }
            Apertar(1); pesoGesto = 1;
            var capsula = jogador.GetComponent<CharacterController>();
            Vector3 rumo = Vector3.ProjectOnPlane(jogador.transform.position - saidaBolhas.position, Vector3.up).normalized;
            if (rumo.sqrMagnitude < .01f) rumo = Vector3.forward;
            for (int i = 0; i < quantidadeBolhas; i++)
            {
                if (!Valido(vida)) yield break;
                Lancar(Quaternion.AngleAxis(quantidadeBolhas > 1 ? Mathf.Lerp(-30, 30, i / (float)(quantidadeBolhas - 1)) : 0, Vector3.up) * rumo, i, vida, capsula);
                for (float t = 0; t < .2f; t += Time.deltaTime * ritmo)
                { if (!Valido(vida)) yield break; yield return null; }
            }
            for (float t = 0; t < tempoRetorno; t += Time.deltaTime * ritmo)
            {
                if (!Valido(vida)) yield break;
                float p = Mathf.SmoothStep(0, 1, t / tempoRetorno);
                Apertar(1f - Mathf.SmoothStep(0, 1, Mathf.Clamp01(p * 3f)));
                pesoGesto = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1f / 3f, 1f, p)); yield return null;
            }
            // A animacao termina aqui. As bolhas continuam voando e bloqueiam
            // apenas os proximos ataques especiais; o tentaculo pode voltar a bater.
        }
        finally { Restaurar(); }
    }
    void Lancar(Vector3 rumo, int indice, VidaJogadorKraken vida, CharacterController capsula)
    {
        bolhas.RemoveAll(b => !b);
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Bolha gigante de sabao";
        // Mantem o diametro em unidades do mundo mesmo se o Kraken tiver escala.
        Vector3 origem = saidaBolhas.position; origem.y = AlturaDoVoo;
        go.transform.position = origem + rumo * raioBolha;
        go.transform.localScale = Vector3.one * raioBolha * 2;
        var colisor = go.GetComponent<Collider>(); colisor.enabled = false; Destroy(colisor);
        var visual = go.GetComponent<Renderer>(); visual.sharedMaterial = materialBolha;
        visual.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; visual.receiveShadows = false;
        var bolha = go.AddComponent<BolhaSabaoKraken>();
        bolha.dono = this; bolha.jogador = vida; bolha.capsula = capsula;
        bolha.raio = raioBolha; bolha.velocidade = velocidadeBolha; bolha.duracao = vidaDaBolha;
        // Cada bolha recebe um pouso fixo na plataforma, sem perseguir o jogador no ar.
        float fase = indice * 2.399963f;
        Vector3 preferido = vida.transform.position + new Vector3(Mathf.Cos(fase), 0, Mathf.Sin(fase))
            * variacaoDoPouso * (indice % 3 == 0 ? .3f : 1f);
        Vector3 alvo = capsula ? capsula.bounds.center : vida.transform.position + Vector3.up;
        if (TemChaoDefinido && Superficies.DestinoProximo(preferido, out var piso))
            alvo = piso + Vector3.up * (raioBolha + .03f);
        float distancia = Vector3.ProjectOnPlane(alvo - go.transform.position, Vector3.up).magnitude;
        rumo = Vector3.ProjectOnPlane(alvo - go.transform.position, Vector3.up).normalized;
        if (rumo.sqrMagnitude < .01f) rumo = Vector3.forward;
        bolha.velocidade = Mathf.Max(.1f, velocidadeBolha * (batalha ? batalha.MultiplicadorVelocidadeAtaques : 1f) * (1 + .1f * Mathf.Sin(fase)));
        float tempoVoo = Mathf.Max(batalha ? batalha.DuracaoAtaque(1.2f) : 1.2f, distancia / bolha.velocidade);
        bolha.velocidade = distancia / tempoVoo;
        float arco = alturaArco * (1 + .18f * Mathf.Sin(fase + .7f));
        bolha.gravidade = 8f * arco / (tempoVoo * tempoVoo);
        bolha.impulsoVertical = (alvo.y - go.transform.position.y) / tempoVoo + .5f * bolha.gravidade * tempoVoo;
        bolha.tempoAtePouso = tempoVoo;
        bolha.desvioLateral = desvioLateralBolhas;
        bolha.duracao = Mathf.Max(vidaDaBolha, tempoVoo + 3f);
        // Alterna por rajada: a primeira permite desviar; a segunda tem uma perseguidora.
        // So avanca a alternancia quando a primeira bolha realmente e lancada.
        bolha.acertoGarantido = indice == 0 && proximaRajadaComAcertoGarantido;
        if (indice == 0) proximaRajadaComAcertoGarantido = !proximaRajadaComAcertoGarantido;
        bolha.tempoAteAcerto = batalha ? batalha.DuracaoAtaque(3f) : 3f;
        if (bolha.acertoGarantido) go.name = "Bolha de sabao perseguidora";
        bolha.Preparar(rumo, indice); bolhas.Add(bolha); BolhasLancadas++;
    }
    void Restaurar()
    {
        if (tampa) tampa.localPosition = tampaInicial;
        RetirarGesto(); pesoGesto = 0; EmExecucao = false;
    }
    public void Cancelar()
    {
        cancelado = true; Restaurar();
        foreach (var bolha in bolhas) if (bolha) bolha.Estourar(false);
        bolhas.Clear();
        foreach (var efeito in FindObjectsByType<VisaoEnsaboada>()) efeito.Limpar();
        foreach (var controle in FindObjectsByType<ThirdPersonController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            controle.LimparSabao();
    }
    void OnDisable() { Cancelar(); }
}
