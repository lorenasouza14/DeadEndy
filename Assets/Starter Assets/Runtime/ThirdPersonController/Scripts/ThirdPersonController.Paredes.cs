using UnityEngine;
namespace StarterAssets {
 public partial class ThirdPersonController {
    int ultimoContatoParede = -100;
    float degrauOriginal = -1f;
    void AtualizarBloqueioEscalada() {
        if (!_controller) return;
        if (degrauOriginal < 0f) degrauOriginal = _controller.stepOffset;
        _controller.stepOffset = Time.frameCount - ultimoContatoParede <= 2 ? 0f : degrauOriginal;
    }
    void OnControllerColliderHit(ControllerColliderHit hit) {
        if (!hit.collider.GetComponentInParent<ParedeNaoEscalavel>()) return;
        ultimoContatoParede = Time.frameCount;
        if (_verticalVelocity > 0f) _verticalVelocity = 0f;
        if (_input != null) _input.jump = false;
    }
    bool ApoiadoEmParedeNaoEscalavel() {
        if (!_controller) return false;
        Vector3 pes = transform.TransformPoint(_controller.center - Vector3.up * _controller.height * .5f);
        float distancia = float.PositiveInfinity;
        Collider suporte = null;
        foreach (var hit in Physics.RaycastAll(pes + Vector3.up * .2f, Vector3.down, .45f, ~0, QueryTriggerInteraction.Ignore)) {
            if (hit.transform.IsChildOf(transform) || hit.distance >= distancia) continue;
            distancia = hit.distance; suporte = hit.collider;
        }
        return suporte && suporte.GetComponentInParent<ParedeNaoEscalavel>();
    }
 }
}