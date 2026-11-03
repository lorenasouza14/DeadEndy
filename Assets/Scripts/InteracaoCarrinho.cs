using UnityEngine;

public class InteracaoCarrinho : MonoBehaviour
{
    public Transform interiorDoCarrinho; // Um objeto vazio posicionado dentro do carrinho
    public CarrinhoTrilho scriptCarrinho;
    private GameObject player;
    private bool dentroDoCarrinho = false;

    void OnTriggerStay(Collider outro)
    {
        if (!dentroDoCarrinho && (outro.CompareTag("Player") || outro.GetComponentInParent<StarterAssets.ThirdPersonController>() != null))
        {
            player = outro.gameObject;

            // Se o jogador pressionar E, entra no carrinho
            if (Input.GetKeyDown(KeyCode.E))
            {
                EntrarNoCarrinho();
            }
        }
    }

    void EntrarNoCarrinho()
    {
        dentroDoCarrinho = true;

        // Desativa o controlador do personagem para ele não andar lá dentro
        var controller = player.GetComponentInParent<StarterAssets.ThirdPersonController>();
        if (controller != null) controller.enabled = false;

        // Cola o personagem dentro do carrinho
        player.transform.SetParent(interiorDoCarrinho);
        player.transform.localPosition = Vector3.zero;
        player.transform.localRotation = Quaternion.identity;

        // Inicia o movimento do trilho
        if (scriptCarrinho != null)
        {
            scriptCarrinho.IniciarViagem();
        }
    }
}