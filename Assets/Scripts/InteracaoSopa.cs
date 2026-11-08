using UnityEngine;

public static class InteracaoSopa
{
    public static bool AoAlcance(InventarioEndy jogador, Vector3 ponto, float alcance, Transform ignorar)
    {
        var capsula = jogador.GetComponent<CharacterController>();
        Vector3 origem = capsula != null ? capsula.bounds.center : jogador.transform.position + Vector3.up;
        Vector3 delta = ponto - origem;
        if (delta.sqrMagnitude > alcance * alcance) return false;
        foreach (var hit in Physics.RaycastAll(origem, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(jogador.transform) || hit.transform.IsChildOf(ignorar)) continue;
            return false;
        }
        return true;
    }
}
