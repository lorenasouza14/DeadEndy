using UnityEngine;

/// <summary>
/// Bau exclusivo da caverna: abre com um E e entrega a lanterna ao personagem.
/// Nao depende do caranguejo e nao altera o comportamento dos outros baus.
/// </summary>
public class InteracaoBauLanterna : MonoBehaviour
{
    [Header("Animacoes")]
    public Animator animEndy;
    public Animator animBau;

    [Header("Lanterna dentro do bau")]
    public GameObject lanternaNoBau;
    public Transform maoDireita;
    [Min(0.5f)] public float distanciaParaInteragir = 3f;
    [Min(0f)] public float tempoParaPegarLanterna = 0.8f;
    public Vector3 posicaoNaMao;
    public Vector3 rotacaoNaMao;

    private bool pertoDoBau;
    private bool bauAberto;
    private Transform jogador;

    private void Awake()
    {
        StarterAssets.ThirdPersonController controlador =
            FindFirstObjectByType<StarterAssets.ThirdPersonController>();

        if (controlador != null)
            jogador = controlador.transform;
    }

    private void Update()
    {
        if (!bauAberto && Input.GetKeyDown(KeyCode.E) && JogadorEstaPerto())
            AbrirBau();
    }

    private bool JogadorEstaPerto()
    {
        if (pertoDoBau)
            return true;

        if (jogador == null)
        {
            StarterAssets.ThirdPersonController controlador =
                FindFirstObjectByType<StarterAssets.ThirdPersonController>();
            if (controlador != null)
                jogador = controlador.transform;
        }

        return jogador != null &&
               Vector3.Distance(jogador.position, transform.position) <= distanciaParaInteragir;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") ||
            other.GetComponentInParent<StarterAssets.ThirdPersonController>() != null)
            pertoDoBau = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") ||
            other.GetComponentInParent<StarterAssets.ThirdPersonController>() != null)
            pertoDoBau = false;
    }

    private void AbrirBau()
    {
        bauAberto = true;

        if (animEndy != null)
            animEndy.SetTrigger("Interagir");

        if (animBau == null)
            animBau = GetComponent<Animator>() ?? GetComponentInChildren<Animator>() ?? GetComponentInParent<Animator>();

        if (animBau != null)
            animBau.SetTrigger("Abrir");
        else
            Debug.LogWarning("Arraste o Animator do bau para o campo Anim Bau.");

        Invoke(nameof(EntregarLanterna), tempoParaPegarLanterna);
    }

    private void EntregarLanterna()
    {
        if (lanternaNoBau == null || maoDireita == null)
        {
            Debug.LogWarning("Arraste a lanterna e a mao direita nos campos do bau.");
            return;
        }

        Rigidbody corpo = lanternaNoBau.GetComponent<Rigidbody>();
        if (corpo != null)
        {
            corpo.isKinematic = true;
            corpo.useGravity = false;
        }

        lanternaNoBau.transform.SetParent(maoDireita, false);
        lanternaNoBau.transform.localPosition = posicaoNaMao;
        lanternaNoBau.transform.localEulerAngles = rotacaoNaMao;
        lanternaNoBau.SetActive(true);

        if (LanternaJogador.Instancia != null)
            LanternaJogador.Instancia.ColetarLanterna();
    }
}
