using UnityEngine;
using System.Collections.Generic;

/// <summary>Movimento aditivo sobre a pose original do esqueleto, sem alterar o ataque.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public class KrakenNadando : MonoBehaviour
{
    [System.Serializable]
    public class Tentaculo
    {
        public Transform[] ossos;
        [Range(0, 80)] public float elevacaoDaRaiz;
    }
    [Header("Esqueleto e batalha")]
    public Transform armadura;
    public Transform cabeca;
    public Tentaculo[] tentaculos;
    public BatalhaKraken batalha;
    [Header("Corpo nadando - unidades do mundo")]
    [Min(0)] public float balancoLateral = 3.5f;
    [Min(0)] public float flutuacao = 1.8f;
    [Range(0, 15)] public float inclinacaoCorpo = 6f;
    [Min(.1f)] public float velocidadeCorpo = .7f;
    [Header("Expressividade do nado")]
    [Range(1f, 2.5f)] public float expressividadeNado = 1.7f;
    [Header("Ondas nos tentaculos - graus")]
    [Range(0, 30)] public float amplitudeRaiz = 12f;
    [Range(0, 50)] public float amplitudePonta = 29f;
    [Min(.1f)] public float velocidadeOndas = 1.9f;
    [Range(.2f, 2)] public float atrasoEntreOssos = .85f;
    [Range(0, 1)] public float movimentoLateral = .5f;
    [Min(.1f)] public float entradaSuave = 1.5f;
    [Min(.1f)] public float paradaSuave = 1.2f;
    [Range(0, 1)] public float intensidade = 1f;

    [Header("Balanco leve dos dois tentaculos laterais")]
    [Range(0, 8)] public float amplitudeBalancoTentaculos = 4.5f;
    [Min(.1f)] public float velocidadeBalancoTentaculos = 1.12f;

    [Header("Ondulacao fluida do braco ate a ponta")]
    [Range(0, 8)] public float amplitudeOndulacaoCorpo = 4f;
    [Range(0, 10)] public float amplitudeEnrolarPontas = 9f;
    [Min(.1f)] public float velocidadeEnrolarPontas = .95f;
    [Range(.1f, 1.5f)] public float atrasoOndaPontas = .55f;

    [Header("Acompanhar o personagem com atraso")]
    public bool acompanharJogador = true;
    [Tooltip("Opcional: se vazio, localiza o personagem da fase automaticamente.")]
    public Transform jogador;
    [Min(0)] public float atrasoParaAcompanhar = .2f;
    [Min(.01f)] public float suavidadeDoGiro = .5f;
    [Min(1)] public float velocidadeMaximaDoGiro = 48f;
    [Tooltip("Desvio maximo do alvo em relacao ao giro atual. O corpo pode continuar virando para acompanhar o jogador por toda a arena.")]
    [Range(0, 180)] public float limiteDoGiro = 70f;

    struct AmostraDeOlhar
    {
        public float tempo, angulo;
        public AmostraDeOlhar(float tempo, float angulo) { this.tempo = tempo; this.angulo = angulo; }
    }
    readonly Queue<AmostraDeOlhar> historicoDeOlhar = new Queue<AmostraDeOlhar>();
    AmostraDeOlhar olharAnterior;
    Vector3 frenteInicialDoOlhar;
    float giroDeOlhar, velocidadeDeOlhar, proximaBuscaDoJogador;
    bool olharPreparado;

    int atacante, segundo = -1;
    Vector3 frenteLocal, lateralAtacanteLocal;
    Quaternion[][] pose;
    Vector3[][] eixoDobra, eixoLateral;
    Vector3[] eixoBalancoVisivel;
    float recolhimento;
    Vector3 posicaoBase, escalaBase, eixoBalanco;
    Quaternion rotacaoBase, cabecaBase;
    float tempo, peso, energia = 1;
    bool preparado;
    float pesoRepouso;
    public bool EmRepouso => isActiveAndEnabled && (!batalha ||
        ((batalha.Estado == BatalhaKraken.Etapa.Aguardando ||
          batalha.Estado == BatalhaKraken.Etapa.Recuperando) &&
         !(batalha.captura && (batalha.captura.Ativa || batalha.captura.Reagindo))));

    void Awake()
    {
        if (!armadura || tentaculos == null) { enabled = false; return; }
        posicaoBase = armadura.localPosition; rotacaoBase = armadura.localRotation; escalaBase = armadura.localScale;
        if (cabeca) cabecaBase = cabeca.localRotation;
        if (!jogador)
        {
            var player = FindAnyObjectByType<StarterAssets.ThirdPersonController>();
            if (player) jogador = player.transform;
        }
        Vector3 frente = jogador ? jogador.position - armadura.position : transform.forward;
        frente = Vector3.ProjectOnPlane(frente, Vector3.up).normalized;
        if (frente.sqrMagnitude < .1f) frente = Vector3.forward;
        // A frente do modelo e fixa: nunca recalibrar com a posicao do jogador
        // ao terminar uma captura ou ao reativar o componente.
        var captura = batalha ? batalha.captura : GetComponent<CapturaJogadorKraken>();
        Transform rosto = captura ? (captura.centroDoRosto ? captura.centroDoRosto : captura.olho) : null;
        frenteInicialDoOlhar = rosto ? Vector3.ProjectOnPlane(rosto.forward, Vector3.up).normalized : frente;
        if (frenteInicialDoOlhar.sqrMagnitude < .1f) frenteInicialDoOlhar = frente;
        eixoBalanco = Vector3.Cross(Vector3.up, frente).normalized;
        atacante = batalha && batalha.tentaculoDoCorpo ? batalha.tentaculoDoCorpo.indiceDoTentaculo : 1;
        if (atacante >= 0 && atacante < tentaculos.Length && tentaculos[atacante].ossos.Length > 1)
        {
            var principal = tentaculos[atacante].ossos;
            Vector3 lateral = Vector3.Cross(Vector3.up, frente).normalized;
            if (Vector3.Dot(principal[1].position - armadura.position, lateral) < 0) lateral = -lateral;
            frenteLocal = armadura.InverseTransformDirection(frente);
            lateralAtacanteLocal = armadura.InverseTransformDirection(lateral);
            float melhor = float.NegativeInfinity;
            for (int i = 0; i < tentaculos.Length; i++)
            {
                var ossos = tentaculos[i].ossos;
                if (i == atacante || ossos.Length < 2) continue;
                // Escolhe o braco oposto pela direcao, sem favorecer os bracos compridos submersos.
                float distancia = Vector3.Dot((ossos[1].position - ossos[0].position).normalized, -lateral);
                if (distancia > melhor) { segundo = i; melhor = distancia; }
            }
        }
        eixoBalancoVisivel = new Vector3[tentaculos.Length];
        pose = new Quaternion[tentaculos.Length][];
        eixoDobra = new Vector3[tentaculos.Length][];
        eixoLateral = new Vector3[tentaculos.Length][];
        for (int i = 0; i < tentaculos.Length; i++)
        {
            var ossos = tentaculos[i].ossos;
            if (ossos.Length > 0) eixoBalancoVisivel[i] = ossos[0].InverseTransformDirection(frente).normalized;
            pose[i] = new Quaternion[ossos.Length]; eixoDobra[i] = new Vector3[ossos.Length]; eixoLateral[i] = new Vector3[ossos.Length];
            Vector3 radial = ossos.Length > 1 ? ossos[1].position - ossos[0].position : frente;
            radial = Vector3.ProjectOnPlane(radial, Vector3.up).normalized;
            if (radial.sqrMagnitude < .1f) radial = Quaternion.Euler(0, i * 360f / tentaculos.Length, 0) * frente;
            Vector3 dobrar = Vector3.Cross(radial, Vector3.up).normalized;
            for (int j = 0; j < ossos.Length; j++)
            {
                pose[i][j] = ossos[j].localRotation;
                // Os eixos sao convertidos da pose de repouso para o espaco de cada osso.
                eixoDobra[i][j] = ossos[j].InverseTransformDirection(dobrar).normalized;
                eixoLateral[i][j] = ossos[j].InverseTransformDirection(Vector3.up).normalized;
            }
        }
        foreach (var skin in GetComponentsInChildren<SkinnedMeshRenderer>()) skin.updateWhenOffscreen = true;
        preparado = true;
    }
    void LateUpdate()
    {
        if (!preparado) return;
        // Mantem exatamente a pose do corpo durante a captura e a coceira.
        // O rig do tentaculo continua executando depois, sem disputar com o nado.
        if (batalha && batalha.captura && (batalha.captura.Ativa || batalha.captura.Reagindo))
        {
            historicoDeOlhar.Clear();
            olharAnterior = new AmostraDeOlhar(Time.time, giroDeOlhar);
            velocidadeDeOlhar = 0;
            return;
        }
        tempo += Time.deltaTime;
        bool terminou = batalha && (batalha.Estado == BatalhaKraken.Etapa.ToucaCaindo || batalha.Estado == BatalhaKraken.Etapa.Derrotado);
        float alvoPeso = terminou ? 0 : intensidade;
        peso = Mathf.MoveTowards(peso, alvoPeso, Time.deltaTime / (terminou ? paradaSuave : entradaSuave));
        float alvoEnergia = batalha && batalha.Estado == BatalhaKraken.Etapa.Golpeando ? 1.3f : batalha && batalha.Estado == BatalhaKraken.Etapa.Avisando ? 1.12f : 1f;
        var mare = batalha ? batalha.inundacao : null;
        float mergulho = mare ? mare.MergulhoNormalizado : 0;
        float preparoMergulho = mare && mare.EmExecucao ? mare.PreparacaoNormalizada : 0;
        // O aviso desacelera o nado: nao deve parecer um ataque acelerado.
        if (mare && mare.EmExecucao) alvoEnergia = Mathf.Lerp(1f, .65f, mergulho);
        recolhimento = mare && mare.EmExecucao
            ? (mare.Fase == InundacaoKraken.EtapaMare.Emergindo
                ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.08f, .65f, mergulho))
                : Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.38f, .92f, preparoMergulho)))
            : 0;
        energia = Mathf.Lerp(energia, alvoEnergia, 1 - Mathf.Exp(-Time.deltaTime * 3f));
        float t = tempo * velocidadeCorpo;
        Vector3 deslocamento = eixoBalanco * (Mathf.Sin(t) + .18f * Mathf.Sin(t * 1.73f)) * balancoLateral;
        deslocamento += Vector3.up * (Mathf.Sin(t * 1.5f) * flutuacao);
        deslocamento *= .45f * expressividadeNado * (1 - mergulho * .65f);
        Vector3 deslocamentoFinal = deslocamento * peso;
        if (mare) deslocamentoFinal += Vector3.down * mare.profundidadeMergulho * mergulho;
        armadura.localPosition = posicaoBase + (armadura.parent ? armadura.parent.InverseTransformVector(deslocamentoFinal) : deslocamentoFinal);
        Quaternion giroMundo = Quaternion.AngleAxis(Mathf.Sin(t * .85f) * inclinacaoCorpo * .4f * peso, Vector3.up)
            * Quaternion.AngleAxis(Mathf.Sin(t + .6f) * inclinacaoCorpo * .4f * peso * expressividadeNado, eixoBalanco);
        AtualizarAcompanhamento(Time.time, Time.deltaTime, terminou);
        giroMundo = Quaternion.AngleAxis(giroDeOlhar, Vector3.up) * giroMundo;
        Quaternion giroLocal = armadura.parent ? Quaternion.Inverse(armadura.parent.rotation) * giroMundo * armadura.parent.rotation : giroMundo;
        armadura.localRotation = giroLocal * rotacaoBase;
        // Contracao suave do manto, como uma pulsacao de propulsao.
        float pulso = Mathf.Sin(t * 2f) * .012f * peso * expressividadeNado;
        armadura.localScale = Vector3.Scale(escalaBase, new Vector3(1 - pulso * .5f, 1 + pulso, 1 - pulso * .5f));
        if (cabeca)
        {
            // Dois balancos pequenos, com entrada e saida suaves antes do recolhimento.
            float aviso = mare && mare.EmExecucao && mare.Fase == InundacaoKraken.EtapaMare.Avisando
                ? Mathf.Clamp01(preparoMergulho / .58f) : 0;
            float balanco = Mathf.Sin(aviso * Mathf.PI) * Mathf.Sin(aviso * Mathf.PI * 4f);
            cabeca.localRotation = cabecaBase * Quaternion.Euler(
                (Mathf.Sin(t * 1.3f) * 1.2f + recolhimento * 4f) * peso,
                balanco * 3.5f * peso, (Mathf.Sin(t) * .8f + balanco * 1.2f) * peso);
        }

        // Todos os bracos ondulam. Os rigs de golpe e sabao aplicam seus gestos depois.
        for (int i = 0; i < tentaculos.Length; i++)
            BalancarTentaculo(i);
    }

    void BalancarTentaculo(int indice)
    {
        var ossos = tentaculos[indice].ossos;
        // Conserva a silhueta enrolada. A onda muscular percorre o braco sem mudar comprimentos.
        for (int j = 0; j < ossos.Length; j++)
            ossos[j].localRotation = pose[indice][j];
        if (ossos.Length == 0) return;
        float fase = indice * 1.37f;
        float t = tempo * velocidadeBalancoTentaculos + fase;
        float angulo = (Mathf.Sin(t) + .15f * Mathf.Sin(t * .57f)) / 1.15f
            * amplitudeBalancoTentaculos * peso * expressividadeNado;
        ossos[0].localRotation = pose[indice][0]
            * Quaternion.AngleAxis(angulo, eixoBalancoVisivel[indice]);
        int inicioPonta = Mathf.Max(1, ossos.Length - 3);
        float faseDoBraco = indice * 2.399963f;
        float respiracao = .85f + .15f * Mathf.Sin(tempo * .23f + faseDoBraco);
        for (int j = 1; j < ossos.Length; j++)
        {
            float u = j / (float)Mathf.Max(1, ossos.Length - 1);
            float onda = tempo * velocidadeEnrolarPontas + faseDoBraco - j * atrasoOndaPontas;
            // Curvas em C e S se alternam; o atraso transporta a dobra para a ponta.
            float flexao = (Mathf.Sin(onda) + .18f * Mathf.Sin(onda * .47f + faseDoBraco)) / 1.18f;
            float amplitude = amplitudeOndulacaoCorpo * Mathf.Sin(u * Mathf.PI * .5f);
            if (j >= inicioPonta)
            {
                float ponta = (j - inicioPonta + 1f) / (ossos.Length - inicioPonta);
                amplitude = Mathf.Lerp(amplitudeOndulacaoCorpo, amplitudeEnrolarPontas,
                    Mathf.SmoothStep(0, 1, ponta));
            }
            amplitude *= peso * respiracao * expressividadeNado;
            // Pequena oscilacao fora do plano evita um movimento mecanico de dobradica.
            float lateral = Mathf.Sin(onda * .67f + 1.1f) * amplitude * .16f;
            ossos[j].localRotation = pose[indice][j]
                * Quaternion.AngleAxis(flexao * amplitude, eixoDobra[indice][j])
                * Quaternion.AngleAxis(lateral, eixoLateral[indice][j]);
        }
        // O rig de ataque executa depois e preserva esta curva suave ao alcancar o alvo.
    }

    // Usa a direcao antiga do jogador e depois limita a velocidade da virada.
    // O giro e aplicado ao esqueleto junto com o nado, mantendo a raiz no lugar.
    void AtualizarAcompanhamento(float agora, float deltaTime, bool terminou)
    {
        if (deltaTime <= 0 || terminou) return;
        if (!acompanharJogador)
        {
            historicoDeOlhar.Clear(); olharPreparado = false;
            giroDeOlhar = Mathf.SmoothDampAngle(giroDeOlhar, 0, ref velocidadeDeOlhar,
                Mathf.Max(.01f, suavidadeDoGiro), Mathf.Max(1, velocidadeMaximaDoGiro), deltaTime);
            return;
        }
        if (!jogador && agora >= proximaBuscaDoJogador)
        {
            proximaBuscaDoJogador = agora + 1f;
            var player = FindAnyObjectByType<StarterAssets.ThirdPersonController>();
            if (player) jogador = player.transform;
        }
        if (!jogador) return;
        Vector3 origem = armadura.parent ? armadura.parent.TransformPoint(posicaoBase) : posicaoBase;
        Vector3 direcao = Vector3.ProjectOnPlane(jogador.position - origem, Vector3.up);
        if (direcao.sqrMagnitude < .01f) return;
        direcao.Normalize();
        float angulo = Vector3.SignedAngle(frenteInicialDoOlhar, direcao, Vector3.up);
        if (!olharPreparado)
        {
            giroDeOlhar = angulo;
            velocidadeDeOlhar = 0;
            olharAnterior = new AmostraDeOlhar(agora, angulo);
            historicoDeOlhar.Clear(); olharPreparado = true;
        }
        // Limita cada correcao a partir da orientacao atual, sem prender o Kraken
        // ao setor inicial quando o jogador muda de lado durante o mergulho.
        angulo = giroDeOlhar + Mathf.Clamp(Mathf.DeltaAngle(giroDeOlhar, angulo),
            -limiteDoGiro, limiteDoGiro);
        historicoDeOlhar.Enqueue(new AmostraDeOlhar(agora, angulo));
        float instanteAtrasado = agora - Mathf.Max(0, atrasoParaAcompanhar);
        while (historicoDeOlhar.Count > 0 && historicoDeOlhar.Peek().tempo <= instanteAtrasado)
            olharAnterior = historicoDeOlhar.Dequeue();
        float alvo = olharAnterior.angulo;
        if (historicoDeOlhar.Count > 0)
        {
            var seguinte = historicoDeOlhar.Peek();
            float u = Mathf.InverseLerp(olharAnterior.tempo, seguinte.tempo, instanteAtrasado);
            alvo = Mathf.LerpAngle(olharAnterior.angulo, seguinte.angulo, u);
        }
        giroDeOlhar = Mathf.SmoothDampAngle(giroDeOlhar, alvo, ref velocidadeDeOlhar,
            Mathf.Max(.01f, suavidadeDoGiro), Mathf.Max(1, velocidadeMaximaDoGiro), deltaTime);
    }

    void OnDisable()
    {
        if (!preparado) return;
        armadura.localPosition = posicaoBase; armadura.localRotation = rotacaoBase; armadura.localScale = escalaBase;
        if (cabeca) cabeca.localRotation = cabecaBase;
        for (int i = 0; i < tentaculos.Length; i++) for (int j = 0; j < tentaculos[i].ossos.Length; j++)
            if (tentaculos[i].ossos[j]) tentaculos[i].ossos[j].localRotation = pose[i][j];
        peso = pesoRepouso = 0;
        historicoDeOlhar.Clear(); olharPreparado = false;
        giroDeOlhar = 0; velocidadeDeOlhar = 0;
    }
}
