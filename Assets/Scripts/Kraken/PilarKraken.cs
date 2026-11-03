using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class PilarKraken : MonoBehaviour
{
    public BatalhaKraken batalha;
    public CorpoLiberavelKraken pilar;
    public CorpoLiberavelKraken corrente;
    public UnityEvent aoQuebrar = new UnityEvent();
    public bool Quebrado { get; private set; }
    public void ReceberGolpe(Vector3 origem)
    {
        if (Quebrado) return;
        Quebrado = true;
        foreach (var marca in GetComponentsInChildren<LineRenderer>()) marca.enabled = false;
        Vector3 direcao = Vector3.ProjectOnPlane(transform.position - origem, Vector3.up);
        if (direcao.sqrMagnitude < .01f) direcao = Vector3.forward;
        if (pilar) pilar.Liberar(direcao, 5f);
        if (corrente) corrente.Liberar(direcao, 2f);
        aoQuebrar.Invoke();
        if (batalha) batalha.RegistrarPilar(this);
    }
}
