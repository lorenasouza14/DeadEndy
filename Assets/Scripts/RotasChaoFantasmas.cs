using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

// O trajeto usa a projecao horizontal do piso. A altura vem dos triangulos originais.
// Assim o fantasma acompanha as curvas e elevacoes, mas pode atravessar paredes.
public sealed class RotasChaoFantasmas : MonoBehaviour
{
    static readonly Dictionary<Scene, RotasChaoFantasmas> instancias = new Dictionary<Scene, RotasChaoFantasmas>();
    struct Face { public Vector3 a, b, c, normal; public Bounds limites; }
    readonly Dictionary<Vector2Int, List<Face>> piso = new Dictionary<Vector2Int, List<Face>>();
    readonly List<NavMeshLinkInstance> ligacoes = new List<NavMeshLinkInstance>();
    readonly List<Vector3[]> emendas = new List<Vector3[]>();
    NavMeshData dados;
    Mesh projecao;
    NavMeshDataInstance instancia;
    NavMeshQueryFilter filtro;
    Scene cena;
    const float Celula = 10f;
    public bool Pronto => instancia.valid;
    public int QuantidadeDeLigacoes => ligacoes.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() { instancias.Clear(); }
    public static RotasChaoFantasmas Obter(Scene scene)
    {
        if (instancias.TryGetValue(scene, out var atual) && atual && atual.Pronto) return atual;
        var go = new GameObject("Rotas do piso - somente fantasmas");
        go.hideFlags = HideFlags.HideInHierarchy;
        SceneManager.MoveGameObjectToScene(go, scene);
        var rotas = go.AddComponent<RotasChaoFantasmas>();
        rotas.cena = scene;
        instancias[scene] = rotas;
        rotas.Construir(scene);
        return rotas;
    }
    static Vector3 Plano(Vector3 p) => new Vector3(p.x, 0, p.z);
    static Vector2Int Chave(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x / Celula), Mathf.FloorToInt(p.z / Celula));
    public bool Amostrar(Vector3 posicao, float raio, out Vector3 ponto)
    {
        ponto = posicao;
        if (!Pronto || !NavMesh.SamplePosition(Plano(posicao), out var hit, raio, filtro)) return false;
        ponto = new Vector3(hit.position.x, posicao.y, hit.position.z);
        if (Altura(ponto, out float y)) ponto.y = y;
        return true;
    }
    public bool Altura(Vector3 referencia, out float altura)
    {
        altura = referencia.y; float menor = float.PositiveInfinity;
        if (!piso.TryGetValue(Chave(referencia), out var faces)) return false;
        foreach (var f in faces)
        {
            if (referencia.x < f.limites.min.x || referencia.x > f.limites.max.x
                || referencia.z < f.limites.min.z || referencia.z > f.limites.max.z) continue;
            float y = f.a.y - (f.normal.x * (referencia.x - f.a.x) + f.normal.z * (referencia.z - f.a.z)) / f.normal.y;
            var p = new Vector3(referencia.x, y, referencia.z);
            if (Vector3.Dot(Vector3.Cross(f.b - f.a, p - f.a), f.normal) < -.0001f
                || Vector3.Dot(Vector3.Cross(f.c - f.b, p - f.b), f.normal) < -.0001f
                || Vector3.Dot(Vector3.Cross(f.a - f.c, p - f.c), f.normal) < -.0001f) continue;
            float distancia = Mathf.Abs(y - referencia.y);
            if (distancia < menor && distancia <= 8f) { menor = distancia; altura = y; }
        }
        return !float.IsPositiveInfinity(menor);
    }
    public bool Calcular(Vector3 origem, Vector3 destino, NavMeshPath caminho)
    {
        if (!Pronto || !NavMesh.SamplePosition(Plano(origem), out var inicio, 20, filtro)
            || !NavMesh.SamplePosition(Plano(destino), out var fim, 20, filtro)) return false;
        for (int tentativa = 0; tentativa < 8; tentativa++)
        {
            if (!NavMesh.CalculatePath(inicio.position, fim.position, filtro, caminho)) return false;
            if (caminho.status == NavMeshPathStatus.PathComplete) return true;
            var volta = new NavMeshPath();
            if (!NavMesh.CalculatePath(fim.position, inicio.position, filtro, volta)
                || caminho.corners.Length == 0 || volta.corners.Length == 0) break;
            var ida = caminho.corners; var retorno = volta.corners;
            Vector3 a = ida[ida.Length - 1], b = retorno[retorno.Length - 1];
            if (ida.Length > 1) a = Vector3.MoveTowards(a, ida[ida.Length - 2], .2f);
            if (retorno.Length > 1) b = Vector3.MoveTowards(b, retorno[retorno.Length - 2], .2f);
            if ((a - b).sqrMagnitude > 16f * 16f || (a - b).sqrMagnitude < .0001f) break;
            bool existe = false;
            foreach (var e in emendas)
                if (((e[0] - a).sqrMagnitude < .1f && (e[1] - b).sqrMagnitude < .1f)
                    || ((e[1] - a).sqrMagnitude < .1f && (e[0] - b).sqrMagnitude < .1f)) { existe = true; break; }
            if (existe) break;
            var link = NavMesh.AddLink(new NavMeshLinkData { startPosition = a, endPosition = b,
                width = 0, bidirectional = true, costModifier = 1.2f, area = 0, agentTypeID = filtro.agentTypeID });
            if (!NavMesh.IsLinkValid(link)) break;
            NavMesh.SetLinkOwner(link, this); NavMesh.SetLinkActive(link, true);
            ligacoes.Add(link); emendas.Add(new[] { a, b });
        }
        return caminho.corners.Length > 0;
    }
    void Construir(Scene scene)
    {
        var vertices = new List<Vector3>(); var indices = new List<int>();
        var vistos = new HashSet<MeshFilter>();
        foreach (var raiz in scene.GetRootGameObjects()) foreach (var mf in raiz.GetComponentsInChildren<MeshFilter>())
        {
            if (!vistos.Add(mf) || !mf.sharedMesh || !mf.sharedMesh.isReadable) continue;
            string nome = (mf.name + " " + mf.sharedMesh.name).ToLowerInvariant();
            if (nome.Contains("water") || nome.Contains("agua")) continue;
            if (!(nome.Contains("ground") || nome.Contains("floor") || nome.Contains("stairs") || nome.Contains("ramp")
                || nome.Contains("cave_tube") || nome.Contains("mountain_cave") || nome.Contains("rock_plateau")
                || nome.Contains("cave_entrance") || nome.Contains("cave_wall") || nome.Contains("bridge") || nome.Contains("plank"))) continue;
            var colisor = mf.GetComponent<Collider>();
            if (!colisor || !colisor.enabled || colisor.isTrigger) continue;
            var v = mf.sharedMesh.vertices; var tris = mf.sharedMesh.triangles;
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = mf.transform.TransformPoint(v[tris[i]]), b = mf.transform.TransformPoint(v[tris[i + 1]]), c = mf.transform.TransformPoint(v[tris[i + 2]]);
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                if (normal.y < .35f) continue;
                var limites = new Bounds(a, Vector3.zero); limites.Encapsulate(b); limites.Encapsulate(c);
                var face = new Face { a = a, b = b, c = c, normal = normal, limites = limites };
                var min = Chave(limites.min); var max = Chave(limites.max);
                for (int x = min.x; x <= max.x; x++) for (int z = min.y; z <= max.y; z++)
                {
                    var key = new Vector2Int(x, z);
                    if (!piso.TryGetValue(key, out var lista)) { lista = new List<Face>(); piso[key] = lista; }
                    lista.Add(face);
                }
                int start = vertices.Count;
                vertices.Add(Plano(a)); vertices.Add(Plano(b)); vertices.Add(Plano(c));
                indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
            }
        }
        if (vertices.Count == 0) return;
        projecao = new Mesh { name = "Piso horizontal - rotas dos fantasmas", indexFormat = IndexFormat.UInt32 };
        projecao.SetVertices(vertices); projecao.SetTriangles(indices, 0); projecao.RecalculateBounds();
        var config = NavMesh.GetSettingsByIndex(0);
        config.agentRadius = .05f; config.agentHeight = .5f; config.agentClimb = .2f;
        config.overrideVoxelSize = true; config.voxelSize = .12f;
        config.overrideTileSize = true; config.tileSize = 128; config.minRegionArea = .01f;
        filtro = new NavMeshQueryFilter { agentTypeID = config.agentTypeID, areaMask = NavMesh.AllAreas };
        var bounds = projecao.bounds; bounds.Expand(5);
        dados = NavMeshBuilder.BuildNavMeshData(config, new List<NavMeshBuildSource> {
            new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Mesh, sourceObject = projecao, transform = Matrix4x4.identity, area = 0 }
        }, bounds, Vector3.zero, Quaternion.identity);
        if (dados) instancia = NavMesh.AddNavMeshData(dados);
    }
    void OnDisable() { Liberar(); }
    void OnDestroy() { Liberar(); }
    public void Liberar()
    {
        foreach (var link in ligacoes) NavMesh.RemoveLink(link);
        ligacoes.Clear();
        if (instancia.valid) instancia.Remove();
        if (Application.isPlaying) { if (dados) Destroy(dados); if (projecao) Destroy(projecao); }
        else { if (dados) DestroyImmediate(dados); if (projecao) DestroyImmediate(projecao); }
        dados = null; projecao = null;
        if (instancias.TryGetValue(cena, out var atual) && atual == this) instancias.Remove(cena);
    }
}
