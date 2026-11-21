using UnityEngine;
namespace StarterAssets
{
    public partial class ThirdPersonController
    {
        readonly RaycastHit[] contatosPouso = new RaycastHit[16];
        float puloPendenteAte = -1f;
        bool DetectarApoioDosPes()
        {
            if (_verticalVelocity > 0f || ApoiadoEmParedeNaoEscalavel()) return false;
            if (_controller.isGrounded) return true;
            Bounds b = _controller.bounds;
            float raio = Mathf.Min(b.extents.x, b.extents.z) * .85f;
            Vector3 origem = new Vector3(b.center.x, b.min.y + raio + .02f, b.center.z);
            int n = Physics.SphereCastNonAlloc(origem, raio, Vector3.down, contatosPouso,
                .10f, GroundLayers, QueryTriggerInteraction.Ignore);
            float normalMinima = Mathf.Cos(_controller.slopeLimit * Mathf.Deg2Rad);
            for (int i = 0; i < n; i++)
            {
                var hit = contatosPouso[i];
                if (hit.transform.IsChildOf(transform) || hit.normal.y < normalMinima) continue;
                if (hit.collider.GetComponentInParent<ParedeNaoEscalavel>()) continue;
                return true;
            }
            return false;
        }
    }
}
