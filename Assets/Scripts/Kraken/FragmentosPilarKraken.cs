using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Configurado apenas nos quatro suportes da batalha, nunca no prefab compartilhado.
[DisallowMultipleComponent]
public class FragmentosPilarKraken : MonoBehaviour
{
    public MeshFilter origem;
    public Mesh[] malhas;
    public Material interior;
    [Tooltip("Tempo minimo antes de remover um fragmento que ja passou abaixo do chao.")]
    [Min(1f)] public float duracao = 3f;
    public bool Desabou { get; private set; }
    public int QuantidadeAtiva => destrocos ? destrocos.childCount : 0;
    Transform destrocos;
    float nivelDeRemocao;

    public bool Desmoronar(Vector3 direcao)
    {
        if (Desabou) return true;
        if (!origem || malhas == null || malhas.Length == 0) return false;
        foreach (var mesh in malhas) if (!mesh) return false;
        var visual = origem.GetComponent<MeshRenderer>();
        if (!visual) return false;
        Desabou = true;
        var materiais = new List<Material>(visual.sharedMaterials) { interior };
        Vector3 centro = visual.bounds.center;
        nivelDeRemocao = visual.bounds.min.y - 2f;
        destrocos = new GameObject("Fragmentos do pilar Kraken").transform;
        // Raiz independente: esconder/restaurar o suporte nao deixa fragmentos orfaos.
        destrocos.SetPositionAndRotation(origem.transform.position, origem.transform.rotation);
        destrocos.localScale = origem.transform.lossyScale;
        var pedras = new List<Rigidbody>();


        foreach (var mesh in malhas)
        {
            var go = new GameObject(mesh.name);
            go.transform.SetParent(destrocos, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materiais.ToArray();
            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;
            col.convex = true;



            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 4f;
            // Despedaca e cai sem girar os blocos do pilar.
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            // Fragmentos visuais atravessam o cenario e nao bloqueiam o jogador.
            rb.detectCollisions = false;
            pedras.Add(rb);
        }
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        for (int i = 0; i < pedras.Count; i++)
        {
            var rb = pedras[i];
            Vector3 fora = Vector3.ProjectOnPlane(rb.worldCenterOfMass - centro, Vector3.up).normalized;
            rb.AddForce(fora * 1.8f + direcao.normalized * 2f + Vector3.up * .4f, ForceMode.VelocityChange);

        }
        StartCoroutine(Recolher());
        return true;
    }

    IEnumerator Recolher()
    {
        float tempo = 0;
        while (destrocos && destrocos.childCount > 0)
        {
            // Conserva a escala. Cada pedra desaparece apenas depois de afundar por inteiro.
            if (tempo >= duracao)
                foreach (Transform parte in destrocos)
                {
                    var visual = parte.GetComponent<Renderer>();
                    if (visual && visual.bounds.max.y < nivelDeRemocao) Destroy(parte.gameObject);
                }
            tempo += Time.deltaTime;
            yield return null;
        }
        Limpar();
    }

    void Limpar()
    {
        if (destrocos) Destroy(destrocos.gameObject);
        destrocos = null;
    }
    void OnDisable() { StopAllCoroutines(); Limpar(); }
    void OnDestroy() { Limpar(); }
}

