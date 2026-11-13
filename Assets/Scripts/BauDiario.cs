using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class BauDiario : MonoBehaviour
{
    public InventarioEndy endy;
    public Animator animBau;
    public GameObject diarioVisual;
    [Min(0.5f)] public float alcance = 3.5f;
    [Min(0.1f)] public float tempoAbrir = 0.65f;
    public bool Aberto { get; private set; }
    public bool Coletado { get; private set; }
    bool abrindo;
    Vector3 posicaoAnim;
    BrilhoColetavel brilhoDiario;

    public string Dica => PodeInteragir ? (Aberto ? "[E] Pegar diário" : "[E] Abrir baú do diário") : null;
    bool PodeInteragir
    {
        get
        {
            if (!isActiveAndEnabled || endy == null || !endy.isActiveAndEnabled || Coletado || abrindo ||
                Time.timeScale == 0 || InteracaoBau.PuzzleAtivo || endy.ExecutandoAcao) return false;
            var vida = endy.GetComponent<VidaEndy>();
            if (vida != null && vida.EstaMorto) return false;
            Vector3 delta = endy.transform.position - transform.position;
            return new Vector2(delta.x, delta.z).sqrMagnitude <= alcance * alcance && Mathf.Abs(delta.y) < 4f;
        }
    }

    void Awake()
    {
        if (diarioVisual != null)
        {
            diarioVisual.SetActive(false);
            brilhoDiario = new BrilhoColetavel(diarioVisual);
        }
        if (animBau == null) return;
        posicaoAnim = animBau.transform.localPosition;
        animBau.applyRootMotion = false;
        animBau.Play("Chest_Open_Close", 0, 0);
        animBau.speed = 0;
    }

    void LateUpdate()
    {
        if (animBau != null) animBau.transform.localPosition = posicaoAnim;
        if (Aberto && !Coletado) brilhoDiario?.Desenhar(Camera.main);
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        bool interagir = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        bool interagir = Input.GetKeyDown(KeyCode.E);
#endif
        if (interagir) Interagir();
    }

    public bool Interagir()
    {
        if (!PodeInteragir) return false;
        AnimacaoInteracaoEndy.Tocar(endy);
        if (!Aberto) StartCoroutine(Abrir());
        else
        {
            endy.ColetarItem(false, true);
            Coletado = true;
            if (diarioVisual != null) diarioVisual.SetActive(false);
            AutosaveEndy.Instancia?.SolicitarAutosave();
        }
        return true;
    }

    IEnumerator Abrir()
    {
        abrindo = true;
        if (animBau != null)
        {
            animBau.speed = 1;
            animBau.Play("Chest_Open_Close", 0, 0);
        }
        yield return new WaitForSeconds(tempoAbrir);
        if (animBau != null) animBau.speed = 0;
        if (diarioVisual != null) diarioVisual.SetActive(true);
        Aberto = true;
        abrindo = false;
        AutosaveEndy.Instancia?.SolicitarAutosave();
    }

    public void RestaurarEstado(bool aberto, bool coletado)
    {
        StopAllCoroutines();
        abrindo = false;
        Aberto = aberto || coletado;
        Coletado = coletado;
        if (animBau != null)
        {
            animBau.speed = 1;
            animBau.Play("Chest_Open_Close", 0, 0);
            animBau.Update(Aberto ? tempoAbrir : 0);
            animBau.speed = 0;
        }
        if (diarioVisual != null) diarioVisual.SetActive(Aberto && !Coletado);
    }

    void OnDisable()
    {
        StopAllCoroutines();
        if (abrindo)
        {
            abrindo = false;
            if (animBau != null) { animBau.Play("Chest_Open_Close", 0, 0); animBau.speed = 0; }
        }
    }
}
