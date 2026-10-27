using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class InteracaoBau : MonoBehaviour
{
    [Header("Configurações do Puzzle")]
    [Tooltip("A barra (Slider) que criamos dentro do Canvas do baú")]
    public Slider barraPuzzle;

    [Tooltip("Velocidade com que o ponteiro vai de um lado pro outro")]
    public float velocidadePonteiro = 1.5f;

    [Tooltip("Ponto mínimo da zona de acerto (0.0 a 1.0)")]
    public float zonaVerdeMin = 0.4f;
    [Tooltip("Ponto máximo da zona de acerto (0.0 a 1.0)")]
    public float zonaVerdeMax = 0.6f;

    [Header("Dificuldade a cada acerto")]
    [Range(0.1f, 1f)] public float fatorLarguraPorAcerto = 0.7f;
    [Range(0.01f, 1f)] public float larguraMinimaZona = 0.06f;
    [Min(0f)] public float aumentoVelocidadePorAcerto = 0.5f;
    private float zonaInicialMin, zonaInicialMax;
    private RectTransform marcadorZona;

    [Header("Feedback de erro")]
    [Min(0.1f)] public float duracaoPiscarErro = 0.6f;
    public Color corErroPuzzle = new Color(1f, 0.06f, 0.03f, 1f);
    private Coroutine rotinaErro;
    private Graphic[] graficosErro;
    private Color[] coresAntesDoErro;

    [Header("Configurações do Combate")]
    public CaranguejoChefe chefe;
    public int trancasParaAbrir = 3;
    public Animator animBau;

    [Tooltip("O objeto do Slider inteiro")]
    public GameObject objetoSlider;

    [Tooltip("O texto flutuante [ E ]")]
    public GameObject iconeTeclaE;

    private int trancasQuebradas = 0;
    private bool fazendoPuzzle = false;
    private bool jogadorPerto = false;
    private bool bauAberto = false;
    private static bool combateIniciado = false;
    private bool podeApertar = false;
    private readonly HashSet<Collider> contatos = new HashSet<Collider>();
    private float velocidadeInicial, fasePonteiro;
    private static InteracaoBau puzzleAtual;
    private StarterAssets.ThirdPersonController movimentoJogador;
    public static bool PuzzleAtivo => puzzleAtual != null;
    public bool FazendoPuzzle => fazendoPuzzle;
    public bool BauAberto => bauAberto;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ReiniciarCombate()
    {
        combateIniciado = false;
        puzzleAtual = null;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= AoCarregarCena;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += AoCarregarCena;
    }
    static void AoCarregarCena(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (mode != UnityEngine.SceneManagement.LoadSceneMode.Single) return;
        combateIniciado = false;
        puzzleAtual = null;
    }

    // === AS VARIÁVEIS DA ÂNCORA ===
    private Vector3 posicaoFixaPai;
    private Vector3 posicaoFixaAnim;

    void Awake()
    {
        if (barraPuzzle == null) barraPuzzle = GetComponentInChildren<Slider>(true);
        if (barraPuzzle != null)
        {
            objetoSlider = barraPuzzle.gameObject;
            var canvas = barraPuzzle.GetComponentInParent<Canvas>(true);
            if (canvas != null) canvas.gameObject.SetActive(true);
            barraPuzzle.minValue = 0;
            barraPuzzle.maxValue = 1;
            barraPuzzle.wholeNumbers = false;
            barraPuzzle.interactable = false;
        }
        if (animBau == null) animBau = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
        velocidadeInicial = velocidadePonteiro;
        zonaInicialMin = zonaVerdeMin;
        zonaInicialMax = zonaVerdeMax;
        if (barraPuzzle != null)
            foreach (var rect in barraPuzzle.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == "Zona de acerto" || rect.name == "MarcadorVerde") { marcadorZona = rect; break; }
        AtualizarDificuldade();
        // Garante que ambos começam invisíveis, mas o Canvas (o pai) fica ligado!
        if (objetoSlider != null) objetoSlider.SetActive(false);
        if (iconeTeclaE != null) iconeTeclaE.SetActive(false);

        posicaoFixaPai = transform.position;
        if (animBau != null)
        {
            posicaoFixaAnim = animBau.transform.position;
            animBau.applyRootMotion = false;
            animBau.speed = 0f;
            animBau.Play("Chest_Open_Close", 0, 0);
        }
    }

    // === O BLOCO QUE IMPEDE A FLUTUAÇÃO ===
    void LateUpdate()
    {
        transform.position = posicaoFixaPai;
        if (animBau != null)
        {
            animBau.transform.position = posicaoFixaAnim;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled) return;
        if (other.GetComponentInParent<InventarioEndy>() != null && !bauAberto)
        {
            contatos.Add(other);
            movimentoJogador = other.GetComponentInParent<StarterAssets.ThirdPersonController>();
            jogadorPerto = true;
            if (iconeTeclaE != null) iconeTeclaE.SetActive(!fazendoPuzzle);
        }
    }
    void OnTriggerStay(Collider other) { OnTriggerEnter(other); }

    void OnTriggerExit(Collider other)
    {
        if (contatos.Remove(other) && contatos.Count == 0)
        {
            jogadorPerto = false;
            if (iconeTeclaE != null) iconeTeclaE.SetActive(false); // Apaga o [ E ]
            if (fazendoPuzzle) CancelarPuzzle();
        }
    }

    void Update()
    {
        if (Time.timeScale == 0) return;

#if ENABLE_INPUT_SYSTEM
        bool iniciar = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        bool confirmar = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame; // Mudamos para E aqui!
#else
        bool iniciar = Input.GetKeyDown(KeyCode.E);
        bool confirmar = Input.GetKeyDown(KeyCode.E); // E aqui também!
#endif

#if ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
        iniciar |= Input.GetKeyDown(KeyCode.E);
        confirmar |= Input.GetKeyDown(KeyCode.E);
#endif

        if (jogadorPerto && !bauAberto)
        {
            if (!fazendoPuzzle && iniciar)
            {
                IniciarPuzzle();
            }
            // A nossa corrotina EsperaInput() garante que o primeiro clique no E não tente
            // iniciar e acertar o puzzle ao mesmo tempo no mesmo frame!
            else if (fazendoPuzzle && podeApertar)
            {
                fasePonteiro += Time.deltaTime * velocidadePonteiro;
                barraPuzzle.value = Mathf.PingPong(fasePonteiro, 1f);

                if (confirmar)
                {
                    VerificarAcerto();
                }
            }
        }
    }

    void IniciarPuzzle()
    {
        if (barraPuzzle == null || (puzzleAtual != null && puzzleAtual != this)) return;
        PararFeedbackErro();
        puzzleAtual = this;
        if (movimentoJogador != null) movimentoJogador.BloquearPulo = true;
        fazendoPuzzle = true;
        podeApertar = false;
        fasePonteiro = 0;
        barraPuzzle.value = 0;

        // Troca os visuais: Some a dica, aparece o jogo!
        if (iconeTeclaE != null) iconeTeclaE.SetActive(false);
        if (objetoSlider != null) objetoSlider.SetActive(true);

        StartCoroutine(EsperaInput());

        if (!combateIniciado && chefe != null)
        {
            combateIniciado = true;
            chefe.gameObject.SetActive(true);
            chefe.AcordarChefe();
        }
    }

    void VerificarAcerto()
    {
        if (barraPuzzle.value >= zonaVerdeMin && barraPuzzle.value <= zonaVerdeMax)
        {
            trancasQuebradas++;

            if (trancasQuebradas >= trancasParaAbrir)
            {
                AbrirBau();
            }
            else
            {
                StartCoroutine(TremerBauRapido());
                Debug.Log("BOM! Acertou o ritmo (" + trancasQuebradas + "/" + trancasParaAbrir + ")");
                AtualizarDificuldade(true);
            }
        }
        else
        {
            Debug.Log("ERROU! Faíscas saíram do baú! As trancas zeraram!");
            trancasQuebradas = 0;
            velocidadePonteiro = velocidadeInicial;

            if (chefe != null) chefe.InvestidaPunitiva(false);

            CancelarPuzzle();
            if (objetoSlider != null && isActiveAndEnabled)
                rotinaErro = StartCoroutine(PiscarErroSlider());
        }
    }

    IEnumerator PiscarErroSlider()
    {
        graficosErro = objetoSlider.GetComponentsInChildren<Graphic>(true);
        coresAntesDoErro = new Color[graficosErro.Length];
        for (int i = 0; i < graficosErro.Length; i++) coresAntesDoErro[i] = graficosErro[i].color;
        objetoSlider.SetActive(true);
        float duracao = Mathf.Max(0.1f, duracaoPiscarErro);
        float tempo = 0f;
        while (tempo < duracao)
        {
            // Duas piscadas, cada uma com entrada vermelha e retorno suave a cor original.
            float fase = Mathf.Repeat(tempo / duracao * 2f, 1f);
            float peso = Mathf.Clamp01(1f - fase * 1.4f);
            for (int i = 0; i < graficosErro.Length; i++)
            {
                if (graficosErro[i] == null) continue;
                Color vermelho = corErroPuzzle;
                vermelho.a = coresAntesDoErro[i].a;
                graficosErro[i].color = Color.Lerp(coresAntesDoErro[i], vermelho, peso);
            }
            yield return null;
            tempo += Time.deltaTime;
        }
        rotinaErro = null;
        RestaurarCoresPuzzle();
        if (objetoSlider != null && !fazendoPuzzle) objetoSlider.SetActive(false);
    }

    void RestaurarCoresPuzzle()
    {
        if (graficosErro == null) return;
        for (int i = 0; i < graficosErro.Length; i++)
            if (graficosErro[i] != null) graficosErro[i].color = coresAntesDoErro[i];
        graficosErro = null;
        coresAntesDoErro = null;
    }

    void PararFeedbackErro()
    {
        if (rotinaErro != null) StopCoroutine(rotinaErro);
        rotinaErro = null;
        RestaurarCoresPuzzle();
        if (objetoSlider != null && !fazendoPuzzle) objetoSlider.SetActive(false);
    }

    void CancelarPuzzle()
    {
        PararFeedbackErro();
        if (movimentoJogador != null && puzzleAtual == this) movimentoJogador.BloquearPulo = false;
        StopAllCoroutines();
        if (puzzleAtual == this) puzzleAtual = null;
        fazendoPuzzle = false;
        podeApertar = false;
        trancasQuebradas = 0;
        AtualizarDificuldade();
        if (animBau != null && !bauAberto)
        {
            animBau.Play("Chest_Open_Close", 0, 0);
            animBau.speed = 0;
        }

        // CORRIGIDO AQUI!
        if (objetoSlider != null) objetoSlider.SetActive(false);
        if (iconeTeclaE != null) iconeTeclaE.SetActive(jogadorPerto && !bauAberto);

        Debug.Log("Você largou o baú para fugir!");
    }

    void AbrirBau()
    {
        PararFeedbackErro();
        if (movimentoJogador != null) movimentoJogador.BloquearPulo = false;
        StopAllCoroutines();
        if (puzzleAtual == this) puzzleAtual = null;
        bauAberto = true;
        fazendoPuzzle = false;

        // CORRIGIDO AQUI!
        if (objetoSlider != null) objetoSlider.SetActive(false);
        if (iconeTeclaE != null) iconeTeclaE.SetActive(false);

        Debug.Log("CLIQUE! O baú destrancou!");

        StartCoroutine(AbrirERevelarItem());
    }

    void AtualizarDificuldade(bool mudarPosicao = false)
    {
        float larguraInicial = Mathf.Max(0.01f, zonaInicialMax - zonaInicialMin);
        float largura = Mathf.Clamp(larguraInicial * Mathf.Pow(fatorLarguraPorAcerto, trancasQuebradas),
            Mathf.Min(larguraMinimaZona, larguraInicial), larguraInicial);
        float centro = (zonaInicialMin + zonaInicialMax) * 0.5f;
        if (mudarPosicao)
        {
            float centroAnterior = (zonaVerdeMin + zonaVerdeMax) * 0.5f;
            float limiteMin = largura * 0.5f;
            float limiteMax = 1f - limiteMin;
            // Sorteia uma posição distante da anterior, mantendo toda a faixa dentro da barra.
            centro = centroAnterior >= 0.5f
                ? Random.Range(limiteMin, Mathf.Clamp(centroAnterior - 0.15f, limiteMin, limiteMax))
                : Random.Range(Mathf.Clamp(centroAnterior + 0.15f, limiteMin, limiteMax), limiteMax);
        }
        zonaVerdeMin = centro - largura * 0.5f;
        zonaVerdeMax = centro + largura * 0.5f;
        velocidadePonteiro = velocidadeInicial + aumentoVelocidadePorAcerto * trancasQuebradas;
        if (marcadorZona == null) return;
        marcadorZona.anchorMin = new Vector2(zonaVerdeMin, marcadorZona.anchorMin.y);
        marcadorZona.anchorMax = new Vector2(zonaVerdeMax, marcadorZona.anchorMax.y);
        marcadorZona.offsetMin = new Vector2(0, marcadorZona.offsetMin.y);
        marcadorZona.offsetMax = new Vector2(0, marcadorZona.offsetMax.y);
    }

    IEnumerator AbrirERevelarItem()
    {
        if (animBau != null)
        {
            animBau.speed = 1;
            animBau.Play("Chest_Open_Close", 0, 0f);
            yield return new WaitForSeconds(0.65f);
            animBau.speed = 0; // Mantém a tampa aberta antes da parte de fechar do clipe.
        }

        Transform localFundo = transform.Find("Chest_Animated/Chest_Bottom");
        if (localFundo == null) localFundo = transform.Find("Chest_Bottom");
        if (localFundo == null) localFundo = transform;
        if (GerenciadorDeBaus.Instancia != null) GerenciadorDeBaus.Instancia.FazerItemNascer(localFundo);
    }

    IEnumerator TremerBauRapido()
    {
        if (animBau != null)
        {
            animBau.Play("Chest_Shake", 0, 0f);
            animBau.speed = 1;
            yield return new WaitForSeconds(0.3f);
            if (!bauAberto) { animBau.Play("Chest_Open_Close", 0, 0); animBau.speed = 0; }
        }
    }

    IEnumerator EsperaInput()
    {
        yield return new WaitForSeconds(0.2f);
        podeApertar = true;
    }
    void OnDisable()
    {
        PararFeedbackErro();
        if (movimentoJogador != null && puzzleAtual == this) movimentoJogador.BloquearPulo = false;
        if (puzzleAtual == this) puzzleAtual = null;
        StopAllCoroutines();
        contatos.Clear();
        jogadorPerto = false;
        if (!bauAberto) CancelarPuzzle();
    }
}
