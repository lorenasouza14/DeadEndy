using UnityEngine;

[RequireComponent(typeof(Collider))]
public class GatilhoArena : MonoBehaviour
{
    [Header("Parede que vai bloquear a saída")]
    public GameObject paredeEntrada;

    void OnTriggerEnter(Collider outro)
    {
        // Verifica se quem pisou no gatilho foi o Endy
        if (outro.CompareTag("Player"))
        {
            // Liga a parede invisível atrás dele!
            if (paredeEntrada != null)
            {
                paredeEntrada.SetActive(true);
                Debug.Log("Arena trancada! Não tem mais volta!");
            }

            // Desativa o próprio gatilho para ele não rodar mais de uma vez
            gameObject.SetActive(false);
        }
    }
}