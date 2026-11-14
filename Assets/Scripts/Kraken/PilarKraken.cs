using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class PilarKraken : MonoBehaviour
{
    public BatalhaKraken batalha;
    public CorpoLiberavelKraken pilar;
    public CorpoLiberavelKraken corrente;
    public UnityEvent aoQuebrar = new UnityEvent();
    public bool Quebrado { get; private set; }
    public bool Rachado { get; private set; }
    public void ReceberGolpe(Vector3 origem)
    {
        if (Quebrado) return;
        if (!Rachado)
        {
            Rachado = true;
            MostrarRachaduras();
            if (batalha) batalha.RegistrarImpactoPilar(this);
            return;
        }
        Quebrado = true;
        foreach (var marca in GetComponentsInChildren<LineRenderer>()) marca.enabled = false;
        Vector3 direcao = Vector3.ProjectOnPlane(transform.position - origem, Vector3.up);
        if (direcao.sqrMagnitude < .01f) direcao = Vector3.forward;
        if (pilar) pilar.Liberar(direcao, 5f);
        if (corrente) corrente.Liberar(direcao, 2f);
        aoQuebrar.Invoke();
        if (batalha)
        {
            batalha.RegistrarPilar(this);
            batalha.RegistrarImpactoPilar(this);
        }
    }

    readonly List<Mesh> malhasRachaduras = new List<Mesh>();
    Material materialRachaduras;

    void MostrarRachaduras()
    {
        var marca = GetComponentInChildren<LineRenderer>();
        if (!pilar || !marca || !marca.sharedMaterial) return;
        materialRachaduras = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Pedra rachada - superficie" };
        var cor = new Color(.075f, .035f, .02f, 1);
        if (materialRachaduras.HasProperty("_BaseColor")) materialRachaduras.SetColor("_BaseColor", cor);
        if (materialRachaduras.HasProperty("_Color")) materialRachaduras.SetColor("_Color", cor);
        if (materialRachaduras.HasProperty("_EmissionColor")) materialRachaduras.SetColor("_EmissionColor", Color.black);
        if (materialRachaduras.HasProperty("_Cull")) materialRachaduras.SetFloat("_Cull", 0);
        materialRachaduras.renderQueue = 2001;

        foreach (var filtro in pilar.GetComponentsInChildren<MeshFilter>())
        {
            if (!filtro.sharedMesh || !filtro.GetComponent<MeshRenderer>()) continue;
            // O colisor de combate pode ser convexo: nao representa as reentrancias da pedra.
            // Esta copia estatica existe somente durante a projecao e usa a malha visual completa.
            var auxiliar = new GameObject("Projecao temporaria da rachadura");
            auxiliar.hideFlags = HideFlags.HideAndDontSave;
            auxiliar.layer = 2;
            auxiliar.transform.SetPositionAndRotation(filtro.transform.position, filtro.transform.rotation);
            auxiliar.transform.localScale = filtro.transform.lossyScale;
            var superficie = auxiliar.AddComponent<MeshCollider>();
            superficie.sharedMesh = filtro.sharedMesh;
            superficie.convex = false;
            try
            {
                Bounds b = superficie.bounds;
                for (int face = 0; face < 4; face++)
                    ProjetarRachadura(superficie, b, face);
            }
            finally
            {
                superficie.enabled = false;
                if (Application.isPlaying) Destroy(auxiliar); else DestroyImmediate(auxiliar);
            }
        }
    }

    void ProjetarRachadura(MeshCollider superficie, Bounds b, int face)
    {
        const int amostras = 192;
        Vector3 normal = Quaternion.Euler(0, face * 90f + 20f, 0) * Vector3.forward;
        Vector3 lateral = Vector3.Cross(Vector3.up, normal);
        float largura = Mathf.Min(b.extents.x, b.extents.z);
        float raio = b.extents.magnitude + 1f;
        // Apenas o minimo afastamento para evitar cintilacao por sobreposicao de faces.
        float afastamento = Mathf.Clamp(largura * .0008f, .001f, .006f);
        var vertices = new List<Vector3>();
        var normais = new List<Vector3>();
        var triangulos = new List<int>();
        bool anteriorValido = false;
        Vector3 esquerdaAnterior = Vector3.zero, direitaAnterior = Vector3.zero;
        float limiteEntreAmostras = b.size.y / amostras * 3f + largura * .025f;
        for (int i = 0; i <= amostras; i++)
        {
            float t = i / (float)amostras;
            float controle = t * 6f;
            int trecho = Mathf.Min(5, Mathf.FloorToInt(controle));
            float a = (trecho % 2 == 0 ? -.14f : .16f) + trecho * .035f;
            float c = ((trecho + 1) % 2 == 0 ? -.14f : .16f) + (trecho + 1) * .035f;
            float desvio = Mathf.Lerp(a, c, controle - trecho);
            Vector3 centro = b.center + lateral * largura * desvio
                + Vector3.up * b.extents.y * Mathf.Lerp(.998f, -.998f, t);
            float meiaLargura = largura * Mathf.Lerp(.045f, .025f, t);
            bool esquerda = superficie.Raycast(new Ray(centro - lateral * meiaLargura + normal * raio, -normal), out var he, raio * 2f);
            bool direita = superficie.Raycast(new Ray(centro + lateral * meiaLargura + normal * raio, -normal), out var hd, raio * 2f);
            if (!esquerda || !direita || Vector3.Dot(he.normal, normal) < .15f || Vector3.Dot(hd.normal, normal) < .15f)
            {
                anteriorValido = false;
                continue;
            }
            Vector3 pe = he.point + he.normal * afastamento;
            Vector3 pd = hd.point + hd.normal * afastamento;
            int k = vertices.Count;
            vertices.Add(pilar.transform.InverseTransformPoint(pe));
            vertices.Add(pilar.transform.InverseTransformPoint(pd));
            normais.Add(pilar.transform.InverseTransformDirection(he.normal));
            normais.Add(pilar.transform.InverseTransformDirection(hd.normal));
            // Nao atravessa vazios ou saltos de profundidade entre partes do asset.
            if (anteriorValido && Vector3.Distance(pe, esquerdaAnterior) < limiteEntreAmostras
                && Vector3.Distance(pd, direitaAnterior) < limiteEntreAmostras)
            {
                triangulos.Add(k - 2); triangulos.Add(k); triangulos.Add(k - 1);
                triangulos.Add(k - 1); triangulos.Add(k); triangulos.Add(k + 1);
            }
            anteriorValido = true;
            esquerdaAnterior = pe; direitaAnterior = pd;
        }
        if (triangulos.Count == 0) return;
        var malha = new Mesh { name = "Rachadura projetada na pedra" };
        malha.SetVertices(vertices); malha.SetNormals(normais); malha.SetTriangles(triangulos, 0);
        malha.RecalculateBounds();
        malhasRachaduras.Add(malha);
        var rachadura = new GameObject("Rachadura aderida");
        rachadura.transform.SetParent(pilar.transform, false);
        rachadura.AddComponent<MeshFilter>().sharedMesh = malha;
        var visual = rachadura.AddComponent<MeshRenderer>();
        visual.sharedMaterial = materialRachaduras;
        visual.shadowCastingMode = ShadowCastingMode.Off;
        visual.receiveShadows = false;
        visual.lightProbeUsage = LightProbeUsage.Off;
        visual.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    void OnDestroy()
    {
        foreach (var malha in malhasRachaduras)
            if (malha) { if (Application.isPlaying) Destroy(malha); else DestroyImmediate(malha); }
        if (materialRachaduras) { if (Application.isPlaying) Destroy(materialRachaduras); else DestroyImmediate(materialRachaduras); }
    }
}
