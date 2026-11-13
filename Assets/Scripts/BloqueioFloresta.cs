using UnityEngine;

// O próprio cubo é o guia editável. Só seu visual desaparece ao iniciar a partida.
[DisallowMultipleComponent, RequireComponent(typeof(BoxCollider)), DefaultExecutionOrder(-75)]
public class BloqueioFloresta : MonoBehaviour
{
    [Tooltip("Extensão acima e abaixo do cubo, em metros, para impedir passar por baixo ou pular o bloqueio.")]
    [Min(0)] public float margemVertical = 10f;
    BoxCollider parede;
    bool preparado;

    void Awake() { Preparar(); }

    public BoxCollider Preparar()
    {
        if (parede == null) parede = GetComponent<BoxCollider>();
        if (preparado) return parede;
        preparado = true;
        foreach (var visual in GetComponentsInChildren<Renderer>(true)) visual.enabled = false;
        var tamanho = parede.size;
        tamanho.y += 2f * margemVertical / Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.y));
        parede.size = tamanho;
        parede.isTrigger = false;
        parede.enabled = true;
        return parede;
    }
}
