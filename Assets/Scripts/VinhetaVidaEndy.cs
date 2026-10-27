using UnityEngine;
using UnityEngine.UI;

// Gradiente somente nas bordas. O centro permanece transparente em qualquer proporcao de tela.
[RequireComponent(typeof(CanvasRenderer))]
public class VinhetaVidaEndy : Graphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = rectTransform.rect;
        const int segments = 64;
        float[] rings = { 0.18f, 0.48f, 1f };
        float[] alpha = { 0f, 0.55f, 1f };
        for (int ring = 0; ring < rings.Length; ring++)
        {
            Color tint = color;
            tint.a *= alpha[ring];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                direction /= Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y));
                Vector2 point = rect.center + Vector2.Scale(direction * rings[ring], rect.size * 0.5f);
                mesh.AddVert(point, tint, Vector2.zero);
            }
        }
        for (int ring = 0; ring < rings.Length - 1; ring++)
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int a = ring * segments + i, b = ring * segments + next;
                mesh.AddTriangle(a, b, b + segments);
                mesh.AddTriangle(a, b + segments, a + segments);
            }
    }
}
