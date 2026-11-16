using UnityEngine;
using UnityEngine.Video;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed class EntradaCaverna : MonoBehaviour
{
    [SerializeField] string cenaDestino = "Level_Cave_3";
    [SerializeField] VideoClip videoCarregamento;
    bool carregando;
    TelaCarregamento tela;

    void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        var corpo = GetComponent<Rigidbody>();
        corpo.isKinematic = true;
        corpo.useGravity = false;
        tela = TelaCarregamento.Preparar(transform, videoCarregamento);
    }

    void OnTriggerEnter(Collider outro)
    {
        if (carregando) return;
        var jogador = outro.GetComponentInParent<StarterAssets.ThirdPersonController>();
        if (jogador == null || !jogador.CompareTag("Player") || !jogador.isActiveAndEnabled) return;
        var vida = jogador.GetComponent<VidaEndy>();
        if (vida != null && vida.EstaMorto) return;
        if (!tela.Iniciar(cenaDestino)) return;

        carregando = true;
        jogador.GetComponent<AutosaveEndy>()?.PrepararTravessia();
        jogador.enabled = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
