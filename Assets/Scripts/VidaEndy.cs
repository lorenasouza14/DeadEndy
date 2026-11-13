using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public class VidaEndy : MonoBehaviour
{
    [Header("Configurações de Vida")]
    public int vidaMaxima = 5;
    private float vidaAtual;
    private bool estaMorto = false;
    public bool EstaMorto => estaMorto;
    public float VidaAtual => vidaAtual;
    public float FracaoVida => vidaAtual / Mathf.Max(1, vidaMaxima);
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
    private GerenciadorPause menuPause;
    private StarterAssets.ThirdPersonController movimentoQueda;

    [Header("Componentes")]
    public Animator animEndy;
    public Image telaVermelha;

    [Header("Tempos de Animação")]
    public float tempoAnimacaoMorte = 2.5f;

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
    }

    void OnEnable()
    {
        movimentoQueda = GetComponent<StarterAssets.ThirdPersonController>();
        if (movimentoQueda != null) movimentoQueda.AoSofrerDanoDeQueda += TomarDano;
    }

    void Start()
    {
        menuPause = GerenciadorPause.Instalar(this);
        if (mostrarIndicadorVida)
        {
            indicador = GetComponent<IndicadorVidaEndy>();
            if (indicador == null) indicador = gameObject.AddComponent<IndicadorVidaEndy>();
            indicador.Preparar(this);
        }
        if (telaVermelha != null) telaVermelha.raycastTarget = false;

        if (animEndy != null)
        {
            animEndy.SetTrigger("Nascer");
        }
    }

    public void TomarDano()
    {
        TomarDano(1f);
    }

    public void TomarDano(float quantidade)
    {
        // Se já está morto, ignora golpes extras
        if (estaMorto || Time.timeScale <= 0f || quantidade <= 0f ||
            float.IsNaN(quantidade) || float.IsInfinity(quantidade)) return;

        vidaAtual = Mathf.Max(0f, vidaAtual - quantidade);
        // Tres parcelas de 1/3 podem deixar um residuo de ponto flutuante.
        if (vidaAtual <= vidaMaxima * 0.000001f) vidaAtual = 0f;
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
            vidaAtual = Mathf.Min(vidaMaxima, vidaAtual + 1f);
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
        if (movimentoQueda != null) movimentoQueda.AoSofrerDanoDeQueda -= TomarDano;
        if (menuPause != null) menuPause.Retornar();
        tempoFlash = 0f;
        AtualizarFlash();
        if (indicador != null) indicador.Ocultar();
    }

    System.Collections.IEnumerator RotinaMorte()
    {
        if (menuPause != null) menuPause.Retornar();
        estaMorto = true;
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
