using UnityEngine;

/// <summary>As duas estatuas mostram a proxima pedra esperada pelo puzzle.</summary>
[DisallowMultipleComponent]
public class SequenciaLuzesEstatuas : MonoBehaviour
{
    [Header("Luzes das duas maos")]
    public Light[] luzesDasEstatuas = new Light[0];
    [Header("Cores da dica: vermelho, laranja, amarelo, verde, azul e violeta")]
    public Color[] cores = {
        Color.red, new Color(1f,.4f,0), Color.yellow,
        Color.green, new Color(0,.3f,1), new Color(.55f,0,1)
    };

    void OnEnable()
    {
        PedraBrilhante.ProgressoAlterado += AtualizarDica;
        AtualizarDica();
    }

    void OnDisable()
    {
        PedraBrilhante.ProgressoAlterado -= AtualizarDica;
    }

    void AtualizarDica()
    {
        if (cores == null || cores.Length == 0 || luzesDasEstatuas == null) return;
        var proxima = PedraBrilhante.ProximaCorEsperada;
        // Ao concluir, conserva a ultima cor sem reiniciar a dica.
        int indice = proxima.HasValue ? (int)proxima.Value : cores.Length - 1;
        Color cor = cores[Mathf.Clamp(indice, 0, cores.Length - 1)];
        foreach (Light luz in luzesDasEstatuas)
            if (luz != null) luz.color = cor;
    }
}
