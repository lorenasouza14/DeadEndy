using UnityEngine;

public class EfeitoPocaoVerde : MonoBehaviour
{
    private bool jogadorPerto = false;
    private GameObject endy;
    private CaranguejoChefe chefe;

    void Start()
    {
        // Encontra o chefe na cena automaticamente
        chefe = FindObjectOfType<CaranguejoChefe>();
    }

    void Update()
    {
        // Se o jogador apertar F na área da poção
        if (jogadorPerto && Input.GetKeyDown(KeyCode.F))
        {
            TacarNoChefe();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            jogadorPerto = true;
            endy = other.gameObject;
            Debug.Log("A Poção de Encolher! Aperte [F] para TACAR no Chefe e vencer!");
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

    void TacarNoChefe()
    {
        Debug.Log("Você arremessou a Poção Verde!");

        // 1. Desliga o Kinematic para a física voltar a funcionar (a gravidade puxar)
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;

        // 2. Calcula um arco parabólico do baú até à cabeça do chefe
        Vector3 direcaoProChefe = (chefe.transform.position - transform.position).normalized;

        // Empurra para a frente (12f) e para cima (5f) para fazer uma curva bonita
        Vector3 forcaDoArremesso = (direcaoProChefe * 12f) + (Vector3.up * 5f);

        // 3. Puxa o SEU script original e manda ele fazer a mágica!
        PocaoMagica pocaoMagica = GetComponent<PocaoMagica>();
        VidaEndy vida = endy.GetComponent<VidaEndy>();
        Collider colisorEndy = endy.GetComponent<Collider>();

        pocaoMagica.ConfigurarArremesso(vida, colisorEndy, forcaDoArremesso);

        // 4. Desliga a área de deteção para o jogador não apertar F de novo com ela no ar
        Collider[] colisores = GetComponents<Collider>();
        foreach (Collider col in colisores)
        {
            if (col.isTrigger) col.enabled = false;
        }

        this.enabled = false; // Desliga este script
    }
}