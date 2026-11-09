using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AlavancaTriangulo : MonoBehaviour
{
    [HideInInspector] public string idSave;
    public bool Concluida { get; private set; }
    private Quaternion rotacaoInicialHaste;
    [Header("Conexões")]
    [Tooltip("Arraste o objeto PuzzleTriangulo_Centro aqui")]
    public GerenciadorPuzzleTriangulo gerenciadorCentral;

    [Tooltip("Qual parte do triângulo esta alavanca acende? (0, 1 ou 2)")]
    public int indiceDaLuz = 0;

    [Tooltip("Arraste o seu Prefab AvisoTeclaE aqui")]
    public GameObject iconeTeclaE;

    [Header("Animação (Opcional)")]
    public Animator animAlavanca;
    public string nomeAnimacao = "Pull"; // Troque pelo nome da sua animação

    [Header("Movimento da haste")]
    [Tooltip("Somente a peça móvel (Switch_handle), nunca a base da alavanca.")]
    public Transform haste;
    public Vector3 rotacaoAoPuxar = new Vector3(45f, 0f, 0f);
    [Min(0f)] public float atrasoDoGesto = 0.2f;
    [Min(0.01f)] public float duracaoMovimento = 0.4f;

    private bool puxada = false;
    private bool jogadorPerto = false;
    private Animator animEndy;
    private InventarioEndy inventarioEndy;
    private readonly HashSet<Collider> contatos = new HashSet<Collider>();
    public bool MostrarDica => isActiveAndEnabled && jogadorPerto && !puxada;

    void Awake()
    {
        if (string.IsNullOrEmpty(idSave)) idSave = AutosaveEndy.Identificar(this);
        if (iconeTeclaE != null) iconeTeclaE.SetActive(false);
        if (haste == null)
            foreach (var filho in GetComponentsInChildren<Transform>(true))
                if (filho.name == "Switch_handle") { haste = filho; break; }
        if (haste != null) rotacaoInicialHaste = haste.localRotation;
    }

    void OnTriggerEnter(Collider other)
    {
        var inventario = other.GetComponentInParent<InventarioEndy>();
        if ((other.CompareTag("Player") || inventario != null) && !puxada)
        {
            contatos.Add(other);
            inventarioEndy = inventario;
            animEndy = inventario != null ? inventario.animEndy : other.GetComponentInChildren<Animator>();
            jogadorPerto = true;
            if (iconeTeclaE != null) iconeTeclaE.SetActive(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (contatos.Remove(other) && contatos.Count == 0)
        {
            jogadorPerto = false;
            animEndy = null;
            inventarioEndy = null;
            if (iconeTeclaE != null) iconeTeclaE.SetActive(false);
        }
    }

    void OnTriggerStay(Collider other) { OnTriggerEnter(other); }

    void OnDisable()
    {
        contatos.Clear();
        jogadorPerto = false;
        animEndy = null;
        inventarioEndy = null;
        if (iconeTeclaE != null) iconeTeclaE.SetActive(false);
    }

    void Update()
    {
        if (Time.timeScale == 0) return;
        // Se estiver perto, não foi puxada e o jogador apertar E
        if (jogadorPerto && !puxada)
        {
            bool apertouE = Input.GetKeyDown(KeyCode.E);
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
                apertouE |= UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame;
#endif

            if (apertouE)
            {
                PuxarAlavanca();
            }
        }
    }

    void PuxarAlavanca()
    {
        if (puxada) return;
        puxada = true; // Tranca a alavanca para não puxar duas vezes
        if (iconeTeclaE != null) iconeTeclaE.SetActive(false);

        AnimacaoInteracaoEndy.Tocar(inventarioEndy, animEndy);
        StartCoroutine(AnimarHaste());
    }

    IEnumerator AnimarHaste()
    {
        yield return new WaitForSeconds(atrasoDoGesto);
        // O pivô importado fica no encaixe da haste. A base e o gatilho permanecem imóveis.
        if (haste != null && haste != transform)
        {
            var inicio = haste.localRotation;
            var fim = inicio * Quaternion.Euler(rotacaoAoPuxar);
            float duracao = Mathf.Max(0.01f, duracaoMovimento);
            for (float tempo = 0f; tempo < duracao; tempo += Time.deltaTime)
            {
                haste.localRotation = Quaternion.Slerp(inicio, fim, Mathf.SmoothStep(0f, 1f, tempo / duracao));
                yield return null;
            }
            haste.localRotation = fim;
        }
        else if (animAlavanca != null) animAlavanca.Play(nomeAnimacao);

        // Avisa o centro para acender a luz!
        if (gerenciadorCentral != null)
        {
            gerenciadorCentral.AcenderParteDoTriangulo(indiceDaLuz);
        }
        Concluida = true;
        AutosaveEndy.Instancia?.SolicitarAutosave();
    }

    public void RestaurarPuxada()
    {
        StopAllCoroutines();
        puxada = Concluida = true;
        if (iconeTeclaE != null) iconeTeclaE.SetActive(false);
        if (haste != null && haste != transform) haste.localRotation = rotacaoInicialHaste * Quaternion.Euler(rotacaoAoPuxar);
        if (gerenciadorCentral != null) gerenciadorCentral.AcenderParteDoTriangulo(indiceDaLuz);
    }
}
