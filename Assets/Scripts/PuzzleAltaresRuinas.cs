using StarterAssets;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class PuzzleAltaresRuinas : MonoBehaviour
{
    [HideInInspector] public string idSave;
    public AltarRuinas[] altares;
    public MonolitoDesmoronavel monolito;
    public ThirdPersonController jogador;
    [Min(0.1f)] public float alcanceInteracao = 2.3f;
    [Min(0.1f)] public float duracaoGiro = 0.65f;
    public bool Resolvido { get; private set; }
    public bool Ocupado => indiceGirando >= 0;
    public AltarRuinas AltarProximo { get; private set; }
    private int indiceGirando = -1;
    private float tempoGiro;
    private Quaternion inicioPrimeiro;
    private Quaternion inicioSegundo;
    private VidaEndy vida;
    private bool preparado;

    private void Awake()
    {
        if (string.IsNullOrEmpty(idSave)) idSave = AutosaveEndy.Identificar(this);
    }

    public DadosRuinas CapturarProgresso()
    {
        if (!preparado) return null;
        return new DadosRuinas { id = idSave, resolvido = Resolvido,
            passos = System.Array.ConvertAll(altares, a => a.Passos) };
    }

    public void RestaurarProgresso(DadosRuinas dados)
    {
        if (!preparado || dados == null || dados.passos == null || dados.passos.Length != altares.Length) return;
        indiceGirando = -1;
        AltarProximo = null;
        for (int i = 0; i < altares.Length; i++) altares[i].RestaurarPassos(dados.passos[i]);
        Resolvido = dados.resolvido;
        if (Resolvido) monolito.RestaurarConcluido();
    }

    private void Start()
    {
        if (altares == null || altares.Length != 3 || monolito == null || monolito.corpoInteiro == null)
        {
            Debug.LogError("PuzzleAltaresRuinas: configure os tres altares e o monolito.", this);
            enabled = false;
            return;
        }
        foreach (var altar in altares)
        {
            if (altar == null || altar.pivo == null || altar.alavanca == null || altar.caboAlavanca == null ||
                altar.emissor == null || altar.feixe == null)
            {
                Debug.LogError("PuzzleAltaresRuinas: altar com referencias incompletas.", this);
                enabled = false;
                return;
            }
            altar.Preparar();
        }
        if (jogador == null) jogador = FindAnyObjectByType<ThirdPersonController>();
        if (jogador != null) vida = jogador.GetComponentInChildren<VidaEndy>();
        preparado = true;
    }

    private void Update()
    {
        AltarProximo = null;
        if (!preparado || Time.deltaTime <= 0f || Resolvido) return;
        if (Ocupado)
        {
            AtualizarGiro(Time.deltaTime);
            return;
        }
        if (!JogadorDisponivel()) return;
        float distancia = alcanceInteracao * alcanceInteracao;
        foreach (var altar in altares)
        {
            float candidata = (altar.alavanca.position - jogador.transform.position).sqrMagnitude;
            if (candidata >= distancia || !TemAcesso(altar)) continue;
            distancia = candidata;
            AltarProximo = altar;
        }
#if ENABLE_INPUT_SYSTEM
        bool apertou = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        bool apertou = Input.GetKeyDown(KeyCode.E);
#endif
        if (apertou && AltarProximo != null) Acionar(AltarProximo);
    }

    private bool JogadorDisponivel()
    {
        return jogador != null && jogador.isActiveAndEnabled && !jogador.EstaArremessado &&
            !jogador.IsDodging && (vida == null || !vida.EstaMorto) && !InteracaoBau.PuzzleAtivo;
    }

    private bool TemAcesso(AltarRuinas altar)
    {
        Vector3 inicio = jogador.transform.position + Vector3.up * 0.8f;
        Vector3 diferenca = altar.alavanca.position + Vector3.up * 0.25f - inicio;
        foreach (var hit in Physics.RaycastAll(inicio, diferenca.normalized, diferenca.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(jogador.transform) || hit.transform.IsChildOf(altar.transform)) continue;
            return false;
        }
        return true;
    }

    public bool Acionar(AltarRuinas altar)
    {
        if (!isActiveAndEnabled || !preparado || Resolvido || Ocupado || Time.timeScale <= 0f || !JogadorDisponivel()) return false;
        int indice = System.Array.IndexOf(altares, altar);
        if (indice < 0 || !altar.isActiveAndEnabled ||
            (altar.alavanca.position - jogador.transform.position).sqrMagnitude > alcanceInteracao * alcanceInteracao || !TemAcesso(altar)) return false;
        indiceGirando = indice;
        tempoGiro = 0f;
        var proximo = altares[(indice + 1) % 3];
        inicioPrimeiro = altar.pivo.localRotation;
        inicioSegundo = proximo.pivo.localRotation;
        altar.Avancar();
        proximo.Avancar();
        altar.MostrarFeixe(false);
        proximo.MostrarFeixe(false);
        AltarProximo = null;
        return true;
    }

    private void AtualizarGiro(float delta)
    {
        tempoGiro += delta;
        float t = Mathf.Clamp01(tempoGiro / duracaoGiro);
        float suave = Mathf.SmoothStep(0f, 1f, t);
        var primeiro = altares[indiceGirando];
        var segundo = altares[(indiceGirando + 1) % 3];
        primeiro.pivo.localRotation = Quaternion.Slerp(inicioPrimeiro, primeiro.RotacaoDestino, suave);
        segundo.pivo.localRotation = Quaternion.Slerp(inicioSegundo, segundo.RotacaoDestino, suave);
        primeiro.AnimarAlavanca(t);
        if (t >= 1f)
        {
            indiceGirando = -1;
            AutosaveEndy.Instancia?.SolicitarAutosave();
        }
    }

    private void LateUpdate()
    {
        if (!preparado) return;
        bool todos = true;
        for (int i = 0; i < altares.Length; i++)
        {
            bool girando = Ocupado && (i == indiceGirando || i == (indiceGirando + 1) % 3);
            if (monolito.Desabou) altares[i].MostrarFeixe(false);
            else altares[i].AtualizarFeixe(monolito.corpoInteiro, girando);
            todos &= altares[i].Alinhado;
        }
        if (todos && !Ocupado && !Resolvido && Time.timeScale > 0f)
        {
            Resolvido = true;
            AltarProximo = null;
            monolito.Desmoronar();
            AutosaveEndy.Instancia?.SolicitarAutosave();
        }
    }

    private void OnDisable()
    {
        AltarProximo = null;
        if (!preparado) return;
        foreach (var altar in altares)
        {
            altar.pivo.localRotation = altar.RotacaoDestino;
            altar.AnimarAlavanca(0f);
            altar.MostrarFeixe(false);
        }
        indiceGirando = -1;
    }
}
