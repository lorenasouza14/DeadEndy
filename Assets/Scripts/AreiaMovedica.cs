using UnityEngine;
using StarterAssets;

public class AreiaMovedica : MonoBehaviour
{
    public float velocidadeLenta = 1.5f;

    // Agora guardamos as DUAS velocidades do Endy
    private float velocidadeOriginal;
    private float sprintOriginal;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            var controle = other.GetComponent<ThirdPersonController>();
            if (controle != null)
            {
                velocidadeOriginal = controle.MoveSpeed;
                sprintOriginal = controle.SprintSpeed;

                // Força a mesma lentidão quer ele ande ou corra
                controle.MoveSpeed = velocidadeLenta;
                controle.SprintSpeed = velocidadeLenta;
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            var controle = other.GetComponent<ThirdPersonController>();
            if (controle != null)
            {
                // Devolve as capacidades normais quando sai da poça
                controle.MoveSpeed = velocidadeOriginal;
                controle.SprintSpeed = sprintOriginal;
            }
        }
    }
}