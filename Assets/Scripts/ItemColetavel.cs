using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ItemColetavel : MonoBehaviour
{
    [Header("Qual item é este?")]
    public bool ehPocao;
    public bool ehDiario;

    private bool jogadorPerto = false;
    private InventarioEndy inventario;

    void Update()
    {
        // Se o Endy estiver na área e o jogador apertar E
        if (jogadorPerto && Input.GetKeyDown(KeyCode.E))
        {
            if (inventario != null)
            {
                // Chama a função no Endy para acender a UI e a mão
                inventario.ColetarItem(ehPocao, ehDiario);

                // Faz o objeto 3D físico sumir do cenário!
                Destroy(gameObject);
            }
        }
    }

    void OnTriggerEnter(Collider outro)
    {
        if (outro.CompareTag("Player"))
        {
            jogadorPerto = true;
            inventario = outro.GetComponent<InventarioEndy>();
        }
    }

    void OnTriggerExit(Collider outro)
    {
        if (outro.CompareTag("Player"))
        {
            jogadorPerto = false;
            inventario = null;
        }
    }
}