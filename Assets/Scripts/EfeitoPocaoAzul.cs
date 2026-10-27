using UnityEngine;
using System.Collections;

public class EfeitoPocaoAzul : MonoBehaviour
{
    [Header("Configurações da Poção")]
    public float tempoDeEfeito = 5f; // Quanto tempo dura a velocidade ou lentidão

    private bool jogadorPerto = false;
    private GameObject endy;
    private CaranguejoChefe chefe;
    private bool jaFoiUsada = false;

    void Start()
    {
        chefe = FindObjectOfType<CaranguejoChefe>();
    }

    void Update()
    {
        if (jogadorPerto && !jaFoiUsada)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                StartCoroutine(BeberPocao());
            }
            else if (Input.GetKeyDown(KeyCode.F))
            {
                TacarNoChefe();
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !jaFoiUsada)
        {
            jogadorPerto = true;
            endy = other.gameObject;
            Debug.Log("Achou a Poção Azul! [E] para BEBER (Ficar rápido) ou [F] para TACAR (Deixar o Chefe lento).");
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            jogadorPerto = false;
            endy = null;
        }
    }

    // === OPÇÃO 1: BEBER ===
    IEnumerator BeberPocao()
    {
        jaFoiUsada = true;
        Debug.Log("GLUB GLUB! Endy bebeu a poção. Velocidade máxima!");

        // 1. Esconde a poção (desliga a malha 3D e a área de colisão)
        GetComponentInChildren<MeshRenderer>().enabled = false;
        GetComponent<Collider>().enabled = false;

        // Puxa o script de movimento oficial do Endy
        StarterAssets.ThirdPersonController movimento = endy.GetComponent<StarterAssets.ThirdPersonController>();

        float velNormalOriginal = 2f;
        float velSprintOriginal = 5.335f;

        if (movimento != null)
        {
            // 2. Memoriza as velocidades que você configurou na Unity
            velNormalOriginal = movimento.MoveSpeed;
            velSprintOriginal = movimento.SprintSpeed;

            // Aplica o super turbo (ajuste esses números se quiser mais rápido ou mais devagar)
            movimento.MoveSpeed = 12f;
            movimento.SprintSpeed = 18f;
        }

        // 3. Espera o tempo acabar
        yield return new WaitForSeconds(tempoDeEfeito);

        Debug.Log("O efeito da poção azul acabou. Endy voltou ao normal.");

        // 4. Devolve a velocidade original ao Endy
        if (movimento != null)
        {
            movimento.MoveSpeed = velNormalOriginal;
            movimento.SprintSpeed = velSprintOriginal;
        }

        // 5. Destrói o frasco vazio
        Destroy(gameObject);
    }

    // === OPÇÃO 2: TACAR ===
    void TacarNoChefe()
    {
        jaFoiUsada = true;
        Debug.Log("Endy arremessou a Poção Azul!");

        // Liga a física e faz o arco parabólico igual ao da Poção Verde
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;

        // Transforma o Trigger num colisor sólido para bater no chefe
        GetComponent<Collider>().isTrigger = false;

        Vector3 direcaoProChefe = (chefe.transform.position - transform.position).normalized;
        rb.linearVelocity = (direcaoProChefe * 12f) + (Vector3.up * 5f);
    }

    // Quando a poção física bate em alguma coisa após ser arremessada
    void OnCollisionEnter(Collision collision)
    {
        if (!jaFoiUsada) return; // Só funciona se tiver sido arremessada

        // Verifica se acertou o Caranguejo
        CaranguejoChefe chefeAtingido = collision.collider.GetComponentInParent<CaranguejoChefe>();

        if (chefeAtingido != null)
        {
            Debug.Log("SPLAT! Você jogou a poção de velocidade no Chefe! VOCÊ DEU UM BUFF NELE!");

            // Chama a nova função para acelerar o Caranguejo
            chefeAtingido.FicarRapido(tempoDeEfeito);

            Destroy(gameObject); // Some com a poção
        }
        else if (!collision.collider.CompareTag("Player"))
        {
            Debug.Log("Errou o alvo! A poção quebrou no chão.");
            Destroy(gameObject);
        }
    }
}