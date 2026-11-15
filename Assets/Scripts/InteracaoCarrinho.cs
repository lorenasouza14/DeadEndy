using UnityEngine;
using StarterAssets;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(100)]
public class InteracaoCarrinho : MonoBehaviour
{
    public Transform interiorDoCarrinho;
    public CarrinhoTrilho scriptCarrinho;
    public Transform saidaInicio;
    public Transform saidaFinal;
    ThirdPersonController jogador;
    CharacterController capsula;
    bool embarcado, movimentoEstavaAtivo, capsulaEstavaAtiva;
    public bool MostrarDica => !embarcado && jogador != null && jogador.isActiveAndEnabled &&
        interiorDoCarrinho != null && scriptCarrinho != null && Time.timeScale > 0 && !JogadorMorto();

    void OnTriggerStay(Collider outro)
    {
        if (!embarcado && outro.TryGetComponent<ThirdPersonController>(out var controle)) jogador = controle;
    }
    void OnTriggerExit(Collider outro)
    {
        if (!embarcado && jogador != null && outro.gameObject == jogador.gameObject) jogador = null;
    }
    void Update()
    {
        if (!MostrarDica) return;
#if ENABLE_INPUT_SYSTEM
        bool pressionou = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        bool pressionou = Input.GetKeyDown(KeyCode.E);
#endif
        if (pressionou) TentarEmbarcar(jogador);
    }

    public bool TentarEmbarcar(ThirdPersonController controle)
    {
        if (embarcado || controle == null || !controle.isActiveAndEnabled || Time.timeScale <= 0 ||
            interiorDoCarrinho == null || scriptCarrinho == null) return false;
        jogador = controle;
        if (JogadorMorto() || !scriptCarrinho.IniciarViagem()) return false;
        movimentoEstavaAtivo = jogador.enabled;
        capsula = jogador.GetComponent<CharacterController>();
        capsulaEstavaAtiva = capsula != null && capsula.enabled;
        if (capsula != null) capsula.enabled = false;
        jogador.enabled = false;
        jogador.ReiniciarMedicaoQueda();
        embarcado = true;
        AcompanharAssento();
        return true;
    }
    void LateUpdate()
    {
        if (!embarcado) return;
        if (jogador == null || JogadorMorto()) { Desembarcar(false); return; }
        AcompanharAssento();
        if (!scriptCarrinho.EmViagem) Desembarcar(true);
    }
    void AcompanharAssento()
    {
        jogador.transform.SetPositionAndRotation(interiorDoCarrinho.position,
            Quaternion.Euler(0, interiorDoCarrinho.eulerAngles.y, 0));
    }
    bool JogadorMorto()
    {
        if (jogador == null) return false;
        var vida = jogador.GetComponent<VidaJogadorKraken>();
        var vidaEndy = jogador.GetComponent<VidaEndy>();
        return (vida != null && vida.Morto) || (vidaEndy != null && vidaEndy.EstaMorto);
    }
    void OnDisable() { Desembarcar(false); }
    void Desembarcar(bool chegou)
    {
        if (!embarcado) return;
        if (scriptCarrinho != null && scriptCarrinho.EmViagem) scriptCarrinho.PararViagem();
        if (jogador != null && !JogadorMorto())
        {
            Transform saida = chegou && scriptCarrinho != null ? (scriptCarrinho.NoFinal ? saidaFinal : saidaInicio) : null;
            if (saida != null) jogador.transform.SetPositionAndRotation(saida.position, saida.rotation);
            Physics.SyncTransforms();
            if (capsula != null) capsula.enabled = capsulaEstavaAtiva;
            jogador.PrepararTrocaDeMundo(Camera.main);
            jogador.enabled = movimentoEstavaAtivo;
        }
        embarcado = false;
        jogador = null;
        capsula = null;
    }
}
