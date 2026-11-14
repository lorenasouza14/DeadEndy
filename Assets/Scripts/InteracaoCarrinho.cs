using UnityEngine;
using StarterAssets;

public class InteracaoCarrinho : MonoBehaviour
{
    public Transform interiorDoCarrinho;
    public CarrinhoTrilho scriptCarrinho;
    ThirdPersonController jogador;
    bool embarcado;
    public bool MostrarDica => !embarcado && jogador != null && jogador.isActiveAndEnabled &&
        interiorDoCarrinho != null && scriptCarrinho != null && Time.timeScale > 0;

    void OnTriggerStay(Collider outro)
    {
        if (!embarcado && outro.TryGetComponent<ThirdPersonController>(out var controle))
            jogador = controle;
    }

    void OnTriggerExit(Collider outro)
    {
        if (!embarcado && jogador != null && outro.gameObject == jogador.gameObject) jogador = null;
    }

    void Update()
    {
        if (!MostrarDica || !Input.GetKeyDown(KeyCode.E)) return;
        embarcado = true;
        jogador.enabled = false;
        scriptCarrinho.IniciarViagem();
    }

    void LateUpdate()
    {
        if (!embarcado || jogador == null) return;
        // O jogador continua na cena persistente ao acompanhar o carrinho.
        jogador.transform.SetPositionAndRotation(interiorDoCarrinho.position, Quaternion.Euler(0, interiorDoCarrinho.eulerAngles.y, 0));
        if (!scriptCarrinho.EmViagem) Desembarcar();
    }

    void OnDisable() { Desembarcar(); }

    void Desembarcar()
    {
        if (embarcado && jogador != null) jogador.enabled = true;
        embarcado = false;
        jogador = null;
    }
}
