using UnityEngine;

/// <summary>Libera a passagem depois de acertar todas as pedras da caverna.</summary>
[DisallowMultipleComponent]
public class PortaPuzzleCaverna : MonoBehaviour
{
    [Tooltip("Objeto da porta que bloqueia a passagem, incluindo seus colisores.")]
    public GameObject porta;

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
        if (porta != null && !PedraBrilhante.ProximaCorEsperada.HasValue)
            porta.SetActive(false);
    }
}
