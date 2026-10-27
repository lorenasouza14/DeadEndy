using UnityEngine;

public class InteracaoBilhete : MonoBehaviour
{
    [Header("Interface e Animação")]
    public GameObject painelUI;
    public Animator animEndy; // Arraste o personagem Endy para cá no Inspector

    private bool pertoDoObjeto = false;
    public bool MostrarDica => enabled && pertoDoObjeto && painelUI != null && !painelUI.activeSelf;

    void Update()
    {
        // Usando o sistema clássico com a tecla E que já está funcionando no seu projeto
        if (pertoDoObjeto && Input.GetKeyDown(KeyCode.E))
        {
            // Dispara a animação no Endy
            if (animEndy != null)
            {
                animEndy.SetTrigger("Interagir");
            }

            MostrarInterface();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            pertoDoObjeto = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            pertoDoObjeto = false;
            painelUI.SetActive(false); // Esconde a interface se ele for embora
        }
    }

    void MostrarInterface()
    {
        // Inverte o estado do painel da UI
        bool estadoAtual = painelUI.activeSelf;
        painelUI.SetActive(!estadoAtual);
    }
}
