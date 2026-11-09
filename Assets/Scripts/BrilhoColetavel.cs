using UnityEngine;
using UnityEngine.Rendering;

public sealed class BrilhoColetavel
{
    private static Material material;
    private static Mesh quad;
    private readonly Renderer[] visuais;

    public BrilhoColetavel(GameObject objeto)
    {
        visuais = objeto.GetComponentsInChildren<Renderer>(true);
        if (material == null) material = Resources.Load<Material>("BrilhoColetavel");
        if (quad == null) quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
    }

    public void Desenhar(Camera camera)
    {
        if (camera == null || material == null || quad == null) return;
        Bounds limites = default;
        int camada = -1;
        foreach (var visual in visuais)
        {
            if (visual == null || !visual.enabled || !visual.gameObject.activeInHierarchy ||
                !(visual is MeshRenderer || visual is SpriteRenderer || visual is SkinnedMeshRenderer)) continue;
            if (camada == -1) limites = visual.bounds;
            else limites.Encapsulate(visual.bounds);
            camada = visual.gameObject.layer;
        }
        if (camada == -1) return;

        // Encosta o reflexo na face voltada para o jogador e deixa o cenário ocultá-lo normalmente.
        var frente = camera.transform.forward;
        var extensao = limites.extents;
        float profundidade = Mathf.Abs(frente.x) * extensao.x + Mathf.Abs(frente.y) * extensao.y +
            Mathf.Abs(frente.z) * extensao.z;
        float tamanho = Mathf.Clamp(extensao.magnitude * 2.2f, 0.45f, 1.8f);
        var posicao = limites.center - frente * (profundidade + 0.012f);
        var desenho = new RenderParams(material)
        {
            camera = camera,
            layer = camada,
            receiveShadows = false,
            shadowCastingMode = ShadowCastingMode.Off
        };
        Graphics.RenderMesh(desenho, quad, 0, Matrix4x4.TRS(posicao, camera.transform.rotation, Vector3.one * tamanho));
    }
}
