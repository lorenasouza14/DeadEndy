using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Gera colisão vertical na margem em Play Mode, sem alterar o terreno ou salvar a cena.
public class BarreiraDaAgua : MonoBehaviour
{
    public const float MargemSeca = 0.2f;
    public struct Trecho
    {
        public Vector3 inicio, fim, direcaoTerra;
    }
    struct Superficie
    {
        public Matrix4x4 inversa;
        public Bounds limites;
        public Vector3 centro, normal;
        public float escala;
        public float Distancia(Vector3 ponto)
        {
            float nivel = centro.y - (normal.x * (ponto.x - centro.x) + normal.z * (ponto.z - centro.z)) / normal.y;
            Vector3 local = inversa.MultiplyPoint3x4(new Vector3(ponto.x, nivel, ponto.z));
            float fora = Mathf.Max(Mathf.Abs(local.x - limites.center.x) - limites.extents.x,
                Mathf.Abs(local.z - limites.center.z) - limites.extents.z) * escala;
            return Mathf.Max(ponto.y - nivel - MargemSeca, fora);
        }
    }
    readonly List<Superficie> superficies = new List<Superficie>();
    readonly List<Terrain> terrenos = new List<Terrain>();
    readonly List<Trecho> trechos = new List<Trecho>();
    readonly List<Mesh> malhas = new List<Mesh>();
    public IReadOnlyList<Trecho> Trechos => trechos;
    public float AlturaTopo { get; private set; }
    float alturaBase;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Registrar()
    {
        SceneManager.sceneLoaded -= AoCarregar;
        SceneManager.sceneLoaded += AoCarregar;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InstalarNasCenasAbertas()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++) Garantir(SceneManager.GetSceneAt(i));
    }
    static void AoCarregar(Scene cena, LoadSceneMode modo) { Garantir(cena); }

    public static BarreiraDaAgua Garantir(Scene cena)
    {
        if (!cena.IsValid() || !cena.isLoaded) return null;
        var aguas = new List<MeshCollider>();
        var terrenos = new List<Terrain>();
        foreach (var raiz in cena.GetRootGameObjects())
        {
            var existente = raiz.GetComponent<BarreiraDaAgua>();
            if (existente != null) return existente;
            terrenos.AddRange(raiz.GetComponentsInChildren<Terrain>());
            foreach (var colisor in raiz.GetComponentsInChildren<MeshCollider>())
                if (colisor.sharedMesh != null && (colisor.name == "Ocean_Collider" || colisor.name == "Water_Plane_01") &&
                    Mathf.Abs(colisor.transform.up.y) > 0.8f) aguas.Add(colisor);
        }
        // Somente cenas com os planos de água do cenário recebem a barreira.
        if (aguas.Count == 0 || terrenos.Count == 0) return null;
        var objeto = new GameObject("Proteção permanente da margem");
        SceneManager.MoveGameObjectToScene(objeto, cena);
        var barreira = objeto.AddComponent<BarreiraDaAgua>();
        barreira.terrenos.AddRange(terrenos);
        barreira.AlturaTopo = float.NegativeInfinity;
        barreira.alturaBase = float.PositiveInfinity;
        foreach (var agua in aguas)
        {
            var limites = agua.sharedMesh.bounds;
            Vector3 centro = agua.transform.TransformPoint(limites.center);
            barreira.superficies.Add(new Superficie {
                inversa = agua.transform.worldToLocalMatrix, limites = limites,
                centro = centro, normal = agua.transform.up,
                escala = Mathf.Min(Mathf.Abs(agua.transform.lossyScale.x), Mathf.Abs(agua.transform.lossyScale.z))
            });
            barreira.AlturaTopo = Mathf.Max(barreira.AlturaTopo, agua.bounds.max.y + 20f);
            barreira.alturaBase = Mathf.Min(barreira.alturaBase, agua.bounds.min.y - 50f);
        }
        foreach (var terreno in terrenos) barreira.Construir(terreno);
        Physics.SyncTransforms();
        return barreira;
    }

    float Distancia(Vector3 ponto)
    {
        float distancia = float.PositiveInfinity;
        foreach (var superficie in superficies) distancia = Mathf.Min(distancia, superficie.Distancia(ponto));
        return distancia;
    }

    public bool EstaSobreAgua(Vector3 ponto)
    {
        foreach (var terreno in terrenos)
        {
            Vector3 local = ponto - terreno.transform.position;
            Vector3 tamanho = terreno.terrainData.size;
            if (local.x < 0 || local.z < 0 || local.x > tamanho.x || local.z > tamanho.z) continue;
            ponto.y = terreno.SampleHeight(ponto) + terreno.transform.position.y;
            return Distancia(ponto) < 0;
        }
        return false;
    }

    void Construir(Terrain terreno)
    {
        TerrainData dados = terreno.terrainData;
        int n = dados.heightmapResolution;
        float[,] alturas = dados.GetHeights(0, 0, n, n);
        var pontos = new Vector3[n, n];
        var distancias = new float[n, n];
        Vector3 origem = terreno.transform.position;
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                Vector3 p = origem + new Vector3(x * dados.size.x / (n - 1), alturas[z, x] * dados.size.y, z * dados.size.z / (n - 1));
                pontos[z, x] = p;
                distancias[z, x] = Distancia(p);
            }
        var vertices = new List<Vector3>();
        var indices = new List<int>();
        for (int z = 0; z < n - 1; z++)
            for (int x = 0; x < n - 1; x++)
            {
                CortarTriangulo(pontos[z, x], pontos[z + 1, x], pontos[z, x + 1],
                    distancias[z, x], distancias[z + 1, x], distancias[z, x + 1], vertices, indices);
                CortarTriangulo(pontos[z + 1, x + 1], pontos[z, x + 1], pontos[z + 1, x],
                    distancias[z + 1, x + 1], distancias[z, x + 1], distancias[z + 1, x], vertices, indices);
            }
        if (vertices.Count == 0) return;
        var malha = new Mesh { name = "Colisão da margem", indexFormat = IndexFormat.UInt32 };
        malha.SetVertices(vertices);
        malha.SetTriangles(indices, 0);
        malha.RecalculateBounds();
        malhas.Add(malha);
        var parte = new GameObject("Margem - " + terreno.name);
        parte.transform.SetParent(transform, false);
        parte.AddComponent<MeshCollider>().sharedMesh = malha;
    }

    void CortarTriangulo(Vector3 a, Vector3 b, Vector3 c, float da, float db, float dc, List<Vector3> vertices, List<int> indices)
    {
        bool sa = da > 0, sb = db > 0, sc = dc > 0;
        if (sa == sb && sb == sc) return;
        Vector3 primeiro = Vector3.zero, segundo = Vector3.zero;
        int contagem = 0;
        if (sa != sb) { primeiro = Vector3.Lerp(a, b, da / (da - db)); contagem++; }
        if (sb != sc)
        {
            Vector3 p = Vector3.Lerp(b, c, db / (db - dc));
            if (contagem++ == 0) primeiro = p; else segundo = p;
        }
        if (sc != sa)
        {
            Vector3 p = Vector3.Lerp(c, a, dc / (dc - da));
            if (contagem++ == 0) primeiro = p; else segundo = p;
        }
        Vector3 tangente = Vector3.ProjectOnPlane(segundo - primeiro, Vector3.up);
        if (tangente.sqrMagnitude < 0.000001f) return;
        tangente.Normalize();
        Vector3 normal = Vector3.Cross(Vector3.up, tangente);
        if (Vector3.Dot(normal, (sa ? a : sb ? b : c) - primeiro) < 0) normal = -normal;
        trechos.Add(new Trecho { inicio = primeiro, fim = segundo, direcaoTerra = normal });
        // Trechos espessos e sobrepostos vedam as emendas, inclusive durante investidas rápidas.
        primeiro -= tangente * 0.08f;
        segundo += tangente * 0.08f;
        Vector3 espessura = normal * 0.2f;
        int inicio = vertices.Count;
        foreach (var p in new[] { primeiro + espessura, segundo + espessura, segundo - espessura, primeiro - espessura })
        {
            vertices.Add(new Vector3(p.x, alturaBase, p.z));
            vertices.Add(new Vector3(p.x, AlturaTopo, p.z));
        }
        for (int lado = 0; lado < 4; lado++)
        {
            int i = inicio + lado * 2, j = inicio + ((lado + 1) % 4) * 2;
            // Dupla face: bloqueia pelas duas direções, independentemente da orientação do contorno.
            indices.AddRange(new[] { i, i + 1, j, j, i + 1, j + 1, j, i + 1, i, j + 1, i + 1, j });
        }
    }

    void OnDestroy()
    {
        foreach (var malha in malhas) if (malha != null) Destroy(malha);
    }
}
