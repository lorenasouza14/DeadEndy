using UnityEngine;

[DisallowMultipleComponent]
public class BalancoCorrenteKraken : MonoBehaviour
{
    public MeshFilter visual;
    public CorpoLiberavelKraken corpo;
    [Min(0f)] public float amplitude = .55f;
    [Min(0f)] public float flecha = .45f;
    [Min(.1f)] public float frequencia = .55f;
    public float fase;
    Mesh original, animada;
    Vector3[] repouso, vertices;
    float[] pesos;
    Vector3 lateral, vertical;
    float tempo, impacto;

    void Awake()
    {
        if (!visual || !visual.sharedMesh || !visual.sharedMesh.isReadable) { enabled = false; return; }
        original = visual.sharedMesh;
        animada = Instantiate(original);
        animada.name = original.name + " (balanco local)";
        animada.MarkDynamic();
        visual.sharedMesh = animada;
        repouso = original.vertices;
        vertices = new Vector3[repouso.Length];
        pesos = new float[repouso.Length];
        var b = original.bounds;
        int eixo = b.size.x > b.size.z ? 0 : 2;
        if (b.size.y > b.size[eixo]) eixo = 1;
        var longitudinal = Vector3.zero; longitudinal[eixo] = 1f;
        var mundo = visual.transform.TransformDirection(longitudinal);
        var lado = Vector3.Cross(Vector3.up, mundo).normalized;
        if (lado.sqrMagnitude < .01f) lado = visual.transform.right;
        lateral = visual.transform.InverseTransformVector(lado);
        vertical = visual.transform.InverseTransformVector(Vector3.up);
        for (int i = 0; i < pesos.Length; i++)
        {
            float t = Mathf.InverseLerp(b.min[eixo], b.max[eixo], repouso[i][eixo]);
            // Pontas e seus primeiros elos ficam imoveis junto aos pontos de fixacao.
            float envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - .06f) / .88f));
            pesos[i] = envelope * envelope;
        }
    }

    public void ReceberImpacto() { impacto = 1f; }

    void LateUpdate()
    {
        if (!animada || Time.deltaTime <= 0f || (corpo && corpo.Liberado)) return;
        tempo += Time.deltaTime;
        impacto = Mathf.MoveTowards(impacto, 0f, Time.deltaTime * .6f);
        float onda = Mathf.Sin(tempo * frequencia * Mathf.PI * 2f + fase);
        float tremor = impacto * Mathf.Sin(tempo * 17f) * .25f;
        Vector3 deslocamento = lateral * (onda * amplitude + tremor)
            - vertical * (flecha + Mathf.Sin(tempo * 2.1f + fase) * amplitude * .15f);
        for (int i = 0; i < vertices.Length; i++) vertices[i] = repouso[i] + deslocamento * pesos[i];
        animada.vertices = vertices;
        animada.RecalculateNormals();
        animada.RecalculateBounds();
    }

    void OnDestroy()
    {
        if (visual && original) visual.sharedMesh = original;
        if (animada) Destroy(animada);
    }
}
