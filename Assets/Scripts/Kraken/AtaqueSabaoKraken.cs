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
    [Range(0, 8)] public float inclinacaoDiscreta = 4f;
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
    [Min(.1f)] public float duracaoEmbacamento = 7f;
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
    Quaternion rotacaoSemGesto;
    bool cancelado, gestoAplicado, alturaPreparada;
    float pesoGesto;
    void Awake() { if (tampa) tampaInicial = tampa.localPosition; }
    void Start()
    {
        var jogador = FindAnyObjectByType<ThirdPersonController>();
        if (jogador) PrepararAltura(jogador);
    }
    void PrepararAltura(ThirdPersonController jogador)
    {
        if (alturaPreparada) return;
        var capsula = jogador.GetComponent<CharacterController>();
        float centro = capsula ? jogador.transform.TransformVector(capsula.center).y : 1;
        AlturaDoVoo = (pontoEntradaJogador ? pontoEntradaJogador.position.y : jogador.transform.position.y) + centro + ajusteAlturaBolhas;
        alturaPreparada = true;
    }
    void RetirarGesto()
    {
        if (gestoAplicado && pontaDiscreta) pontaDiscreta.localRotation = rotacaoSemGesto;
        gestoAplicado = false;
    }
    void Update() { RetirarGesto(); }
    void LateUpdate()
    {
        // Pequeno gesto em outro braco, sem solver, esticamento ou controle da captura.
        if (!EmExecucao || pesoGesto <= 0 || !pontaDiscreta || (batalha.captura && batalha.captura.Ativa)) return;
        rotacaoSemGesto = pontaDiscreta.localRotation;
        pontaDiscreta.localRotation = rotacaoSemGesto * Quaternion.AngleAxis(inclinacaoDiscreta * pesoGesto, Vector3.forward);
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
                pesoGesto = .2f * Mathf.SmoothStep(0, 1, t / tempoAproximacao); yield return null;
            }
            for (float t = 0; t < tempoPressao; t += Time.deltaTime * ritmo)
            {
                if (!Valido(vida)) yield break;
                float p = Mathf.SmoothStep(0, 1, t / tempoPressao);
                Apertar(p); pesoGesto = Mathf.Lerp(.2f, 1, p); yield return null;
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
                Apertar(1 - p); pesoGesto = 1 - p; yield return null;
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
        foreach (var bolha in bolhas) if (bolha) bolha.Estourar();
        bolhas.Clear();
        foreach (var efeito in FindObjectsByType<VisaoEnsaboada>()) efeito.Limpar();
    }
    void OnDisable() { Cancelar(); }
}
