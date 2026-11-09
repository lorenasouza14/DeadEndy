using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public class VidaEndy : MonoBehaviour
{
    [Header("Configurações de Vida")]
    public int vidaMaxima = 5;
    private int vidaAtual;
    private bool estaMorto = false;
    public bool EstaMorto => estaMorto;
    public int VidaAtual => vidaAtual;
    public float UltimoDanoEm { get; private set; } = float.NegativeInfinity;

    public void RestaurarVidaDoSave(int quantidade)
    {
        vidaAtual = Mathf.Clamp(quantidade, 1, vidaMaxima);
        tempoAteRecuperar = esperaSemDano;
    }
    public float FracaoVida => (float)vidaAtual / Mathf.Max(1, vidaMaxima);
    public bool VidaBaixa => !estaMorto && FracaoVida <= limiteVidaBaixa;
    public bool EstaRegenerando { get; private set; }

    [Header("Recuperacao de vida")]
    public bool regenerarVida = true;
    [Min(0f)] public float esperaSemDano = 5f;
    [Min(0.05f)] public float intervaloRecuperacao = 1f;

    [Header("Aviso visual de vida")]
    public bool mostrarIndicadorVida = true;
    [Range(0.05f, 0.5f)] public float limiteVidaBaixa = 0.3f;
    private float tempoAteRecuperar;
    private float tempoFlash;
    private IndicadorVidaEndy indicador;

    [Header("Componentes")]
    public Animator animEndy;
    public Image telaVermelha;

    [Header("Tempos de Animação")]
    public float tempoAnimacaoMorte = 2.5f;

    [Header("Nascimento - despertar")]
    [Min(0f)] public float tempoTelaPretaAoNascer = 0.35f;
    [Min(0f)] public float duracaoClarearAoNascer = 2f;
    private GameObject telaNascimento;
    private Coroutine rotinaNascimento;

    [Header("Física e Movimento")]
    public MonoBehaviour scriptDeMovimento; // O script que faz ele andar
    public CharacterController controleDeColisao; // A cápsula que o mantém em pé

    [Header("Sistema de Game Over")]
    public GameObject telaGameOverUI; // O Canvas que montamos
    public VideoPlayer videoGameOver; // O Projetor que toca o vídeo

    [Header("Inimigos")]
    public GameObject caranguejoChefe; 


    void Awake()
    {
        vidaMaxima = Mathf.Max(1, vidaMaxima);
        vidaAtual = vidaMaxima;
        // Também integra cenas já abertas no editor antes de o autosave ser instalado.
        if (gameObject.scene.path == ProgressoJogo.CenaJogo && AutosaveEndy.Instancia == null &&
            GetComponent<InventarioEndy>() != null && GetComponent<AutosaveEndy>() == null)
            gameObject.AddComponent<AutosaveEndy>();
        IniciarTransicaoNascimento();
    }

    // Também pode ser chamado por um respawn futuro que reutilize o personagem.
    public void IniciarTransicaoNascimento()
    {
        EncerrarTransicaoNascimento();
        if (!isActiveAndEnabled || estaMorto ||
            (tempoTelaPretaAoNascer <= 0f && duracaoClarearAoNascer <= 0f)) return;

        // Canvas independente da escala do Endy e já preto antes do primeiro frame.
        telaNascimento = new GameObject("Despertar do Endy", typeof(RectTransform), typeof(Canvas));
        telaNascimento.layer = 5;
        var canvas = telaNascimento.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;
        var fundo = new GameObject("Tela preta", typeof(RectTransform), typeof(Image));
        fundo.layer = 5;
        fundo.transform.SetParent(telaNascimento.transform, false);
        var imagem = fundo.GetComponent<Image>();
        imagem.color = Color.black;
        imagem.raycastTarget = false;
        imagem.rectTransform.anchorMin = Vector2.zero;
        imagem.rectTransform.anchorMax = Vector2.one;
        imagem.rectTransform.offsetMin = imagem.rectTransform.offsetMax = Vector2.zero;
        rotinaNascimento = StartCoroutine(ClarearNascimento(imagem));
    }

    System.Collections.IEnumerator ClarearNascimento(Image imagem)
    {
        // Deixa a câmera se posicionar sob o preto antes de revelar o personagem.
        yield return null;
        if (tempoTelaPretaAoNascer > 0f)
            yield return new WaitForSecondsRealtime(tempoTelaPretaAoNascer);

        float duracao = Mathf.Max(0f, duracaoClarearAoNascer);
        for (float tempo = 0f; tempo < duracao; tempo += Time.unscaledDeltaTime)
        {
            float opacidade = 1f - Mathf.SmoothStep(0f, 1f, tempo / duracao);
            imagem.color = new Color(0f, 0f, 0f, opacidade);
            yield return null;
        }

        rotinaNascimento = null;
        RemoverTelaNascimento();
    }

    void RemoverTelaNascimento()
    {
        if (telaNascimento == null) return;
        telaNascimento.SetActive(false);
        Destroy(telaNascimento);
        telaNascimento = null;
    }

    void EncerrarTransicaoNascimento()
    {
        if (rotinaNascimento != null) StopCoroutine(rotinaNascimento);
        rotinaNascimento = null;
        RemoverTelaNascimento();
    }

    void Start()
    {
        if (mostrarIndicadorVida)
        {
            indicador = GetComponent<IndicadorVidaEndy>();
            if (indicador == null) indicador = gameObject.AddComponent<IndicadorVidaEndy>();
            indicador.Preparar(this);
        }
        if (telaVermelha != null) telaVermelha.raycastTarget = false;

    }

    public void TomarDano()
    {
        TomarDano(1);
    }

    public void TomarDano(int quantidade)
    {
        // Se já está morto, ignora golpes extras
        if (estaMorto || quantidade <= 0) return;
        UltimoDanoEm = Time.unscaledTime;

        vidaAtual = Mathf.Max(0, vidaAtual - quantidade);
        tempoAteRecuperar = Mathf.Max(0f, esperaSemDano);
        EstaRegenerando = false;
        tempoFlash = 0.15f;
        AtualizarFlash();
        Debug.Log("Endy tomou dano! Vida restante: " + vidaAtual);

        // Verifica se o golpe foi fatal
        if (vidaAtual <= 0)
        {
            // Dispara APENAS a rotina de morte
            StartCoroutine(RotinaMorte());
        }
        else
        {
            // Se ainda tem vida, toca a animação de tomar dano
            if (animEndy != null) animEndy.SetTrigger("TomarDano");
        }
    }

    void Update()
    {
        AtualizarRecuperacao(Time.deltaTime);
        tempoFlash = Mathf.Max(0f, tempoFlash - Time.deltaTime);
        AtualizarFlash();
    }

    void AtualizarRecuperacao(float delta)
    {
        if (estaMorto || !regenerarVida || vidaAtual >= vidaMaxima)
        {
            EstaRegenerando = false;
            return;
        }
        if (delta <= 0f) return; // Pausa tambem pausa a espera e a cura.
        tempoAteRecuperar -= delta;
        while (tempoAteRecuperar <= 0f && vidaAtual < vidaMaxima)
        {
            vidaAtual++;
            EstaRegenerando = vidaAtual < vidaMaxima;
            tempoAteRecuperar += Mathf.Max(0.05f, intervaloRecuperacao);
        }
    }

    void AtualizarFlash()
    {
        if (telaVermelha != null)
            telaVermelha.color = new Color(1f, 0.08f, 0.04f, estaMorto ? 0f : 0.35f * tempoFlash / 0.15f);
    }

    void OnDisable()
    {
        EncerrarTransicaoNascimento();
        tempoFlash = 0f;
        AtualizarFlash();
        if (indicador != null) indicador.Ocultar();
    }

    System.Collections.IEnumerator RotinaMorte()
    {
        estaMorto = true;
        EncerrarTransicaoNascimento();
        vidaAtual = 0;
        EstaRegenerando = false;
        tempoFlash = 0f;
        AtualizarFlash();
        if (indicador != null) indicador.Ocultar();
        Debug.Log("GAME OVER! O Endy foi esmagado.");

        if (scriptDeMovimento != null) scriptDeMovimento.enabled = false;
        if (controleDeColisao != null) controleDeColisao.enabled = false;

        if (caranguejoChefe != null)
        {
            // Pega em todos os scripts do chefe e desliga-os para ele parar de pensar/atacar
            MonoBehaviour[] scriptsDoChefe = caranguejoChefe.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour script in scriptsDoChefe)
            {
                script.enabled = false;
            }

            // Se ele usar NavMesh (sistema de navegação da Unity), trava as pernas dele
            UnityEngine.AI.NavMeshAgent agente = caranguejoChefe.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agente != null) agente.enabled = false;
        }

        if (animEndy != null)
        {
            animEndy.SetTrigger("Morrer");
        }

        if (telaGameOverUI != null) telaGameOverUI.SetActive(true); // Liga a TV
        if (videoGameOver != null) videoGameOver.Play();            // Dá o Play no vídeo!

        // Remova o ReiniciarFase() daqui para a cena não dar refresh sozinha!
        yield return null;
    }

    void ReiniciarFase()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ForcarDerrota()
    {
        if (estaMorto) return;
        StartCoroutine(RotinaMorte());
    }
}
