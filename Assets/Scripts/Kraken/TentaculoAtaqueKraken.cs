using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TentaculoAtaqueKraken : MonoBehaviour
{
    [Header("Braco copiado do Kraken")]
    [Tooltip("Malha do braco preparada com o comprimento normalizado no eixo Z.")]
    public Mesh malhaDoBraco;
    [Min(.01f)] public float escalaEspessura = 1f;
    [Min(0)] public float amplitudeOndulacao = .6f;
    [Min(0)] public float velocidadeOndulacao = 1.35f;
    [Header("Geometria de reserva")]
    [Range(6, 32)] public int segmentos = 20;
    [Range(5, 12)] public int lados = 8;
    public float raioBase = 2.8f;
    public float raioPonta = .7f;
    Mesh malha, fonteAtual;
    Vector3[] coordenadas, vertices;
    Vector3 ultimaFrente = Vector3.forward;
    float arcoAtual;
    bool primeiroDesenho = true;

    void Awake() { PrepararMalha(); }

    void PrepararMalha()
    {
        if (malha && fonteAtual == malhaDoBraco) return;
        LiberarMalha();
        fonteAtual = malhaDoBraco;
        if (malhaDoBraco)
        {
            malha = Instantiate(malhaDoBraco);
            malha.name = "Braco do Kraken - ataque animado";
            coordenadas = malhaDoBraco.vertices;
        }
        else
        {
            malha = new Mesh { name = "Tentaculo de ataque - reserva" };
            coordenadas = new Vector3[(segmentos + 1) * lados];
            int[] indices = new int[segmentos * lados * 6];
            int k = 0;
            for (int i = 0; i <= segmentos; i++)
            {
                float t = i / (float)segmentos;
                float r = Mathf.Lerp(raioBase, raioPonta, t);
                for (int j = 0; j < lados; j++)
                {
                    float angulo = j * Mathf.PI * 2 / lados;
                    coordenadas[i*lados+j] = new Vector3(Mathf.Cos(angulo)*r, Mathf.Sin(angulo)*r, t);
                }
            }
            for (int i = 0; i < segmentos; i++) for (int j = 0; j < lados; j++)
            {
                int a = i*lados+j, b = i*lados+(j+1)%lados;
                indices[k++] = a; indices[k++] = b; indices[k++] = a+lados;
                indices[k++] = b; indices[k++] = b+lados; indices[k++] = a+lados;
            }
            malha.vertices = coordenadas;
            malha.triangles = indices;
        }
        vertices = new Vector3[coordenadas.Length];
        malha.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = malha;
    }

    public void Desenhar(Vector3 origem, Vector3 ponta, float arco)
    {
        PrepararMalha();
        Vector3 horizontal = Vector3.ProjectOnPlane(ponta-origem, Vector3.up);
        float distancia = horizontal.magnitude;
        if (distancia > .01f) ultimaFrente = horizontal / distancia;
        Vector3 lateral = Vector3.Cross(Vector3.up, ultimaFrente).normalized;
        // Uma pequena curva lateral permite recolher o braco sem dobrar tudo no mesmo ponto.
        Vector3 folga = ultimaFrente * Mathf.Max(0, 8f-distancia) * .5f;
        Vector3 a=origem, b=origem+Vector3.up*arco+folga;
        Vector3 c=ponta+Vector3.up*arco*.45f+folga, d=ponta;
        float tempo = Application.isPlaying ? Time.time * velocidadeOndulacao : 0;
        Matrix4x4 paraLocal = transform.worldToLocalMatrix;
        for (int i=0; i<coordenadas.Length; i++)
        {
            Vector3 fonte = coordenadas[i];
            float t=Mathf.Clamp01(fonte.z), u=1-t;
            float envelope=Mathf.Sin(Mathf.PI*t), fase=tempo-t*7f;
            Vector3 centro=u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d;
            centro += lateral * (Mathf.Sin(fase)*amplitudeOndulacao*envelope);
            Vector3 tangente=3*u*u*(b-a)+6*u*t*(c-b)+3*t*t*(d-c);
            tangente += lateral * amplitudeOndulacao * (-7f*Mathf.Cos(fase)*envelope+Mathf.PI*Mathf.Sin(fase)*Mathf.Cos(Mathf.PI*t));
            if(tangente.sqrMagnitude<.0001f) tangente=ultimaFrente;
            tangente.Normalize();
            Vector3 vertical=Vector3.Cross(tangente,lateral).normalized;
            Vector3 direita=Vector3.Cross(vertical,tangente).normalized;
            vertices[i]=paraLocal.MultiplyPoint3x4(centro+(direita*fonte.x+vertical*fonte.y)*escalaEspessura);
        }
        malha.vertices=vertices;
        malha.RecalculateNormals();
        malha.RecalculateBounds();
    }

    void LiberarMalha()
    {
        if (!malha) return;
        if (Application.isPlaying) Destroy(malha); else DestroyImmediate(malha);
        malha=null;
    }
    void OnDestroy() { LiberarMalha(); }
}

