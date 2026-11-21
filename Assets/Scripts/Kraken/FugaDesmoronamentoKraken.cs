using UnityEngine;
using StarterAssets;

[DisallowMultipleComponent]
public class FugaDesmoronamentoKraken : MonoBehaviour
{
    public BatalhaKraken batalha;
    public InundacaoKraken inundacao;
    public Transform pedraGatilho;
    public Collider patamarDestino;
    [Min(1)] public float duracaoSubida = 90f;
    [Min(0)] public float margemAbaixoDoPatamar = .6f;
    public bool Acionada { get; private set; }
    public float NivelDestino { get; private set; }
    ThirdPersonController jogador;
    CharacterController capsula;
    VidaJogadorKraken vida;
    readonly RaycastHit[] contatos = new RaycastHit[32];

    public void ReiniciarParkour()
    {
        if (inundacao) inundacao.CancelarERestaurar();
        Acionada = false;
        NivelDestino = 0f;
    }

    void Update()
    {
        if (Acionada || !batalha || !batalha.isActiveAndEnabled || batalha.Estado != BatalhaKraken.Etapa.Derrotado
            || !inundacao || !pedraGatilho || !patamarDestino) return;
        if (!jogador)
        {
            jogador = FindAnyObjectByType<ThirdPersonController>();
            if (!jogador) return;
            capsula = jogador.GetComponent<CharacterController>();
            vida = jogador.GetComponent<VidaJogadorKraken>();
        }
        if (!capsula || !capsula.enabled || !capsula.isGrounded || !vida || vida.Morto) return;
        // Usa o chao sob os pes, sem disparar por encostar na lateral ou passar no ar.
        Bounds corpo = capsula.bounds;
        int quantidade = Physics.RaycastNonAlloc(corpo.center, Vector3.down, contatos,
            corpo.extents.y + .3f, ~0, QueryTriggerInteraction.Ignore);
        float distancia = float.PositiveInfinity;
        RaycastHit chao = default;
        for (int i = 0; i < quantidade; i++)
        {
            var contato = contatos[i];
            if (contato.transform.IsChildOf(jogador.transform) || contato.distance >= distancia) continue;
            chao = contato; distancia = contato.distance;
        }
        if (!chao.collider || chao.normal.y < .5f
            || (chao.transform != pedraGatilho && !chao.transform.IsChildOf(pedraGatilho))) return;
        NivelDestino = patamarDestino.bounds.max.y - margemAbaixoDoPatamar;
        if (inundacao.IniciarSubidaDeFuga(NivelDestino, duracaoSubida)) Acionada = true;
    }
}

