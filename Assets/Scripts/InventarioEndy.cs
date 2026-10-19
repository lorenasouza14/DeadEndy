using UnityEngine;
using System.Collections;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class InventarioEndy : MonoBehaviour
{
    [Header("Itens do Inventário")]
    public bool temDiario = false;
    public bool temPocao = false;

    [Header("Animações do Endy")]
    public Animator animEndy;
    [Tooltip("Tempo em segundos entre apertar F e a poção sair da mão (para sincronizar com o braço)")]
    public float tempoSincroniaArremesso = 0.35f;

    [Header("Configuração do Arremesso (Tecla F)")]
    public GameObject prefabPocao;       // O Prefab da poção com Rigidbody e PocaoMagica
    public Transform pontoDeSaida;       // Opcional: ponto na frente do Endy
    public float forcaFrente = 14f;      // Distância do arremesso
    public float forcaParaCima = 4.5f;   // Arco do arremesso

    [Header("Ícones na Tela (UI Canvas)")]
    public GameObject iconeDiarioUI;     // Imagem do Diário no Canvas (fica na tela sempre após pegar)
    public GameObject iconePocaoUI;      // Imagem da Poção no Canvas (some quando aperta F)
    public GameObject pocaoNaMao3D;      // Opcional: poção presa na mão do Endy

    private VidaEndy vidaEndy;
    private Collider colisorEndy;
    private bool estaJogando = false;

    void Start()
    {
        vidaEndy = GetComponent<VidaEndy>();
        colisorEndy = GetComponent<Collider>();
        if (animEndy == null) animEndy = GetComponentInChildren<Animator>();

        // Garante que os ícones começam escondidos no início da fase
        AtualizarVisuais();
    }

    void Update()
    {
        // Só joga se tiver a poção, não estiver no meio do arremesso e apertar F
        if (temPocao && !estaJogando && ApertouTeclaF())
        {
            StartCoroutine(RotinaJogarPocao());
        }
    }

    bool ApertouTeclaF()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            return true;
#else
        if (Input.GetKeyDown(KeyCode.F))
            return true;
#endif
        return false;
    }

    IEnumerator RotinaJogarPocao()
    {
        estaJogando = true;
        temPocao = false; // Gasta a única chance!

        // 1. Dispara a animação de arremessar
        if (animEndy != null)
        {
            animEndy.SetTrigger("Jogar");
        }

        // 2. Espera o braço do Endy ir para a frente na animação antes de soltar o objeto
        yield return new WaitForSeconds(tempoSincroniaArremesso);

        // 3. Some com o ícone da poção da tela (mas o Diário continua lá!)
        AtualizarVisuais();

        if (prefabPocao != null)
        {
            Vector3 posicaoSaida = pontoDeSaida != null
                ? pontoDeSaida.position
                : transform.position + Vector3.up * 1.2f + transform.forward * 0.8f;

            GameObject pocaoCriada = Instantiate(prefabPocao, posicaoSaida, Quaternion.identity);

            PocaoMagica scriptPocao = pocaoCriada.GetComponent<PocaoMagica>();
            if (scriptPocao != null)
            {
                Vector3 velocidadeArremesso = (transform.forward * forcaFrente) + (Vector3.up * forcaParaCima);
                scriptPocao.ConfigurarArremesso(vidaEndy, colisorEndy, velocidadeArremesso);
            }
        }
        else
        {
            Debug.LogWarning("Faltou arrastar o Prefab da Poção no InventarioEndy!");
        }

        estaJogando = false;
    }

    void AtualizarVisuais()
    {
        if (iconeDiarioUI != null) iconeDiarioUI.SetActive(temDiario);
        if (iconePocaoUI != null) iconePocaoUI.SetActive(temPocao);
        if (pocaoNaMao3D != null) pocaoNaMao3D.SetActive(temPocao);
    }

    public void ColetarItem(bool pegouPocao, bool pegouDiario)
    {
        if (pegouPocao)
        {
            // O nome das variáveis (TemPocao, iconePocaoUI, etc) deve ser exatamente igual ao que está lá no começo do seu script!
            temPocao = true;
            if (iconePocaoUI != null) iconePocaoUI.SetActive(true);
            if (pocaoNaMao3D != null) pocaoNaMao3D.SetActive(true);
        }

        if (pegouDiario)
        {
            temDiario = true;
            if (iconeDiarioUI != null) iconeDiarioUI.SetActive(true);
        }
    }
}