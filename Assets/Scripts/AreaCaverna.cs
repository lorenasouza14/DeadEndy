using UnityEngine;

// Consulta a sobreposicao diretamente para funcionar tambem ao carregar um save
// dentro da area ou teleportar para fora, sem depender de OnTriggerExit.
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class AreaCaverna : MonoBehaviour
{
    public Collider colliderMontanha;
    public CharacterController jogador;
    private BoxCollider area;
    private Collider colliderDesativado;
    private bool estadoAnterior;

    private void OnEnable() => AtualizarColisao();
    private void Update() => AtualizarColisao();
    private void LateUpdate() => AtualizarColisao();
    private void OnDisable() => RestaurarColisao();

    private void AtualizarColisao()
    {
        if (area == null) area = GetComponent<BoxCollider>();
        bool dentro = area != null && area.enabled && jogador != null &&
            jogador.enabled && jogador.gameObject.activeInHierarchy &&
            Physics.ComputePenetration(area, area.transform.position, area.transform.rotation,
                jogador, jogador.transform.position, jogador.transform.rotation, out _, out _);

        if (!dentro || colliderMontanha != colliderDesativado) RestaurarColisao();
        if (!dentro || colliderMontanha == null || colliderMontanha == area) return;

        if (colliderDesativado == null)
        {
            colliderDesativado = colliderMontanha;
            estadoAnterior = colliderMontanha.enabled;
        }
        colliderDesativado.enabled = false;
    }

    private void RestaurarColisao()
    {
        if (colliderDesativado != null) colliderDesativado.enabled = estadoAnterior;
        colliderDesativado = null;
    }

    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }
}
