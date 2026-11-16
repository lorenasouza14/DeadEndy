using UnityEngine;
using UnityEngine.Video;

/// <summary>Libera a passagem depois de acertar todas as pedras da caverna.</summary>
[DisallowMultipleComponent]
public class PortaPuzzleCaverna : MonoBehaviour
{
    [Tooltip("Objeto da porta que bloqueia a passagem, incluindo seus colisores.")]
    public GameObject porta;
    [SerializeField] VideoClip videoCarregamento;
    BoxCollider passagem;
    TelaCarregamento tela;

    void Awake()
    {
        passagem = GetComponent<BoxCollider>();
        tela = TelaCarregamento.Preparar(transform, videoCarregamento);
    }

    void OnTriggerEnter(Collider outro)
    {
        if (porta == null || porta.activeSelf || PedraBrilhante.ProximaCorEsperada.HasValue) return;
        var jogador = outro.GetComponentInParent<TrocarMundo>();
        if (jogador != null) IniciarTravessia(jogador);
    }

    public void IniciarTravessia(TrocarMundo jogador) => jogador.PassarPelaPorta(tela);

    void OnEnable()
    {
        PedraBrilhante.ProgressoAlterado += AtualizarPorta;
        AtualizarPorta();
    }

    void OnDisable()
    {
        PedraBrilhante.ProgressoAlterado -= AtualizarPorta;
    }

    void AtualizarPorta()
    {
        bool bloqueada = PedraBrilhante.ProximaCorEsperada.HasValue;
        if (porta != null)
            porta.SetActive(bloqueada);
        if (passagem != null && passagem.isTrigger)
            passagem.enabled = !bloqueada;
    }
}
