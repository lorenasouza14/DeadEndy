using UnityEngine;

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
    [Header("Ondas nos tentaculos - graus")]
    [Range(0, 30)] public float amplitudeRaiz = 12f;
    [Range(0, 50)] public float amplitudePonta = 29f;
    [Min(.1f)] public float velocidadeOndas = 1.9f;
    [Range(.2f, 2)] public float atrasoEntreOssos = .85f;
    [Range(0, 1)] public float movimentoLateral = .5f;
    [Min(.1f)] public float entradaSuave = 1.5f;
    [Min(.1f)] public float paradaSuave = 1.2f;
    [Range(0, 1)] public float intensidade = 1f;

    Quaternion[][] pose;
    Vector3[][] eixoDobra, eixoLateral;
    Vector3 posicaoBase, escalaBase, eixoBalanco;
    Quaternion rotacaoBase, cabecaBase;
    float tempo, peso, energia = 1;
    bool preparado;

    void Awake()
    {
        if (!armadura || tentaculos == null) { enabled = false; return; }
        posicaoBase = armadura.localPosition; rotacaoBase = armadura.localRotation; escalaBase = armadura.localScale;
        if (cabeca) cabecaBase = cabeca.localRotation;
        var player = FindAnyObjectByType<StarterAssets.ThirdPersonController>();
        Vector3 frente = player ? player.transform.position - armadura.position : transform.forward;
        frente = Vector3.ProjectOnPlane(frente, Vector3.up).normalized;
        if (frente.sqrMagnitude < .1f) frente = Vector3.forward;
        eixoBalanco = Vector3.Cross(Vector3.up, frente).normalized;
        pose = new Quaternion[tentaculos.Length][];
        eixoDobra = new Vector3[tentaculos.Length][];
        eixoLateral = new Vector3[tentaculos.Length][];
        for (int i = 0; i < tentaculos.Length; i++)
        {
            var ossos = tentaculos[i].ossos;
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
        tempo += Time.deltaTime;
        bool terminou = batalha && (batalha.Estado == BatalhaKraken.Etapa.ToucaCaindo || batalha.Estado == BatalhaKraken.Etapa.Derrotado);
        float alvoPeso = terminou ? 0 : intensidade;
        peso = Mathf.MoveTowards(peso, alvoPeso, Time.deltaTime / (terminou ? paradaSuave : entradaSuave));
        float alvoEnergia = batalha && batalha.Estado == BatalhaKraken.Etapa.Golpeando ? 1.3f : batalha && batalha.Estado == BatalhaKraken.Etapa.Avisando ? 1.12f : 1f;
        var mare = batalha ? batalha.inundacao : null;
        float mergulho = mare ? mare.MergulhoNormalizado : 0;
        float preparoMergulho = mare && mare.EmExecucao ? mare.PreparacaoNormalizada : 0;
        if (mare && mare.EmExecucao) alvoEnergia = Mathf.Lerp(1.5f, .8f, mergulho);
        energia = Mathf.Lerp(energia, alvoEnergia, 1 - Mathf.Exp(-Time.deltaTime * 3f));
        float t = tempo * velocidadeCorpo;
        Vector3 deslocamento = eixoBalanco * (Mathf.Sin(t) + .18f * Mathf.Sin(t * 1.73f)) * balancoLateral;
        deslocamento += Vector3.up * (Mathf.Sin(t * 1.5f) * flutuacao);
        deslocamento *= 1 - mergulho * .65f;
        Vector3 deslocamentoFinal = deslocamento * peso;
        if (mare) deslocamentoFinal += Vector3.down * mare.profundidadeMergulho * mergulho;
        armadura.localPosition = posicaoBase + (armadura.parent ? armadura.parent.InverseTransformVector(deslocamentoFinal) : deslocamentoFinal);
        Quaternion giroMundo = Quaternion.AngleAxis(Mathf.Sin(t * .85f) * inclinacaoCorpo * peso, Vector3.up)
            * Quaternion.AngleAxis(Mathf.Sin(t + .6f) * inclinacaoCorpo * peso, eixoBalanco);
        Quaternion giroLocal = armadura.parent ? Quaternion.Inverse(armadura.parent.rotation) * giroMundo * armadura.parent.rotation : giroMundo;
        armadura.localRotation = giroLocal * rotacaoBase;
        armadura.localScale = escalaBase * (1 + Mathf.Sin(t * 2f) * .012f * peso);
        if (cabeca) cabeca.localRotation = cabecaBase * Quaternion.Euler((Mathf.Sin(t * 1.3f) * 2.8f + preparoMergulho * 8f) * peso, 0, Mathf.Sin(t) * 2f * peso);

        for (int i = 0; i < tentaculos.Length; i++)
        {
            var braco = tentaculos[i];
            float fase = i * Mathf.PI * 2 / tentaculos.Length;
            for (int j = 0; j < braco.ossos.Length; j++)
            {
                float u = j / (float)Mathf.Max(1, braco.ossos.Length - 1);
                float onda = tempo * velocidadeOndas * energia - j * atrasoEntreOssos + fase;
                float amplitude = Mathf.Lerp(amplitudeRaiz, amplitudePonta, u);
                float dobra = (Mathf.Sin(onda) + .22f * Mathf.Sin(onda * 1.73f + fase)) * amplitude;
                // Os bracos que estavam inteiramente submersos sao elevados para aparecerem na superficie.
                if (j == 0) dobra += braco.elevacaoDaRaiz;
                if (j == 0) dobra += preparoMergulho * 12f + mergulho * 15f;
                float lado = Mathf.Sin(onda * .73f + fase + 1.4f) * amplitude * movimentoLateral;
                braco.ossos[j].localRotation = pose[i][j]
                    * Quaternion.AngleAxis(dobra * peso, eixoDobra[i][j])
                    * Quaternion.AngleAxis(lado * peso, eixoLateral[i][j]);
            }
        }
    }
    void OnDisable()
    {
        if (!preparado) return;
        armadura.localPosition = posicaoBase; armadura.localRotation = rotacaoBase; armadura.localScale = escalaBase;
        if (cabeca) cabeca.localRotation = cabecaBase;
        for (int i = 0; i < tentaculos.Length; i++) for (int j = 0; j < tentaculos[i].ossos.Length; j++)
            if (tentaculos[i].ossos[j]) tentaculos[i].ossos[j].localRotation = pose[i][j];
        peso = 0;
    }
}
