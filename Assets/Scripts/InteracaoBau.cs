using UnityEngine;

public class InteracaoBau : MonoBehaviour
{
    [Header("Animações e Chefe")]
    public Animator animEndy;
    public Animator animBau;
    public CaranguejoChefe chefe; // Precisamos referenciar o script do chefe aqui

    private bool pertoDoBau = false;
    private bool bauAberto = false;
    private int toquesNoBau = 0; // Contador de interações
    private bool chefeAcordado = false;

    void Update()
    {
        // Se apertar E perto do baú...
        if (pertoDoBau && !bauAberto && Input.GetKeyDown(KeyCode.E))
        {
            // Só deixa interagir SE o caranguejo estiver atordoado
            if (chefe != null && chefe.estadoAtual == CaranguejoChefe.EstadoChefe.Atordoado)
            {
                toquesNoBau++; // Soma 1 ao contador
                Debug.Log($"Toque {toquesNoBau} de 4!");

                if (animEndy != null) animEndy.SetTrigger("Interagir");

                // Se chegou no quarto toque, abre o baú de vez
                if (toquesNoBau >= 4)
                {
                    bauAberto = true;
                    if (animBau != null) animBau.SetTrigger("Abrir"); // Use "Open" se manteve o asset original
                    Debug.Log("Baú aberto! O diário foi revelado.");
                }
            }
            else
            {
                Debug.Log("Você não tem tempo! O Caranguejo está atacando!");
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            pertoDoBau = true;

            if (!chefeAcordado && chefe != null)
            {
                chefe.gameObject.SetActive(true);

                chefe.AcordarChefe();

                chefeAcordado = true;
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) pertoDoBau = false;
    }
}