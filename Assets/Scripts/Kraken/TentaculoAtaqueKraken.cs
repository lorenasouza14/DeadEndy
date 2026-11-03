using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TentaculoAtaqueKraken : MonoBehaviour
{
    [Range(6, 32)] public int segmentos = 20;
    [Range(5, 12)] public int lados = 8;
    public float raioBase = 2.8f;
    public float raioPonta = .7f;
    Mesh malha;
    Vector3[] vertices;
    void Awake()
    {
        malha = new Mesh { name = "Tentaculo de ataque procedural" };
        malha.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = malha;
        vertices = new Vector3[(segmentos + 1) * lados];
        int[] indices = new int[segmentos * lados * 6];
        int k = 0;
        for (int i = 0; i < segmentos; i++) for (int j = 0; j < lados; j++)
        {
            int a = i * lados + j, b = i * lados + (j + 1) % lados;
            indices[k++] = a; indices[k++] = b; indices[k++] = a + lados;
            indices[k++] = b; indices[k++] = b + lados; indices[k++] = a + lados;
        }
        malha.vertices = vertices; malha.triangles = indices;
    }
    public void Desenhar(Vector3 origem, Vector3 ponta, float arco)
    {
        if (!malha) return;
        Vector3 a = origem, b = origem + Vector3.up * arco;
        Vector3 c = ponta + Vector3.up * arco * .45f, d = ponta;
        for (int i = 0; i <= segmentos; i++)
        {
            float t = i / (float)segmentos, u = 1 - t;
            Vector3 p = u*u*u*a + 3*u*u*t*b + 3*u*t*t*c + t*t*t*d;
            Vector3 tangente = (3*u*u*(b-a) + 6*u*t*(c-b) + 3*t*t*(d-c)).normalized;
            if (tangente.sqrMagnitude < .01f) tangente = Vector3.up;
            Vector3 lateral = Vector3.Cross(tangente, Vector3.forward).normalized;
            if (lateral.sqrMagnitude < .01f) lateral = Vector3.right;
            Vector3 vertical = Vector3.Cross(tangente, lateral).normalized;
            float r = Mathf.Lerp(raioBase, raioPonta, t);
            for (int j = 0; j < lados; j++)
            {
                float angulo = j * Mathf.PI * 2 / lados;
                vertices[i*lados+j] = transform.InverseTransformPoint(p + (lateral*Mathf.Cos(angulo)+vertical*Mathf.Sin(angulo))*r);
            }
        }
        malha.vertices = vertices; malha.RecalculateNormals(); malha.RecalculateBounds();
    }
    void OnDestroy() { if (malha) Destroy(malha); }
}
