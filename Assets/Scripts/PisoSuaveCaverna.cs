using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Suaviza apenas a colisao do piso existente. Nao cria superficies entre pecas ou sobre vaos.
[DisallowMultipleComponent]
public sealed class PisoSuaveCaverna : MonoBehaviour
{
    const float AjusteMaximo = .18f;
    sealed class Alteracao
    {
        public MeshCollider colisor;
        public Mesh original, suave;
        public bool convexo;
    }
    readonly List<Alteracao> malhas = new List<Alteracao>();
    readonly List<Collider> detalhes = new List<Collider>();
    CharacterController jogador;
    float degrauOriginal, peleOriginal, inclinacaoOriginal;
    public int QuantidadeMalhasSuavizadas => malhas.Count;
    public int QuantidadeDetalhesIgnorados => detalhes.Count;
    public int VerticesSuavizados { get; private set; }

    public static PisoSuaveCaverna Garantir(Scene cena, CharacterController capsula)
    {
        foreach (var raiz in cena.GetRootGameObjects())
            if (raiz.TryGetComponent<PisoSuaveCaverna>(out var existente)) return existente;
        var go = new GameObject("Colisao suave do piso da caverna");
        SceneManager.MoveGameObjectToScene(go, cena);
        var piso = go.AddComponent<PisoSuaveCaverna>();
        piso.Aplicar(cena, capsula);
        return piso;
    }

    static bool EhPiso(string nome)
    {
        nome = nome.ToLowerInvariant();
        return nome.Contains("stone_ground") || nome.Contains("rock_plateau") ||
            nome.Contains("cave_tube") || nome.Contains("cave_entrance") || nome.Contains("mountain_cave");
    }

    void Aplicar(Scene cena, CharacterController capsula)
    {
        jogador = capsula;
        degrauOriginal = jogador.stepOffset;
        peleOriginal = jogador.skinWidth;
        inclinacaoOriginal = jogador.slopeLimit;
        jogador.stepOffset = Mathf.Max(jogador.stepOffset, .45f);
        jogador.skinWidth = Mathf.Max(jogador.skinWidth, .06f);
        jogador.slopeLimit = Mathf.Max(jogador.slopeLimit, 50f);

        var colisores = new List<Collider>();
        foreach (var raiz in cena.GetRootGameObjects())
            colisores.AddRange(raiz.GetComponentsInChildren<Collider>());
        foreach (var colisor in colisores)
        {
            if (!colisor.enabled || colisor.isTrigger || !EhPiso(colisor.name) ||
                !(colisor is MeshCollider mesh) || !mesh.sharedMesh || !mesh.sharedMesh.isReadable) continue;
            Mesh suave = Suavizar(mesh);
            if (!suave && !mesh.convex) continue;
            malhas.Add(new Alteracao { colisor = mesh, original = mesh.sharedMesh, suave = suave, convexo = mesh.convex });
            // Um casco convexo nao representa os tuneis e as concavidades do piso.
            mesh.convex = false;
            if (suave) mesh.sharedMesh = suave;
        }
        Physics.SyncTransforms();
        foreach (var colisor in colisores)
        {
            if (!colisor.enabled || colisor.isTrigger || !colisor.name.ToLowerInvariant().Contains("pebbles")) continue;
            var b = colisor.bounds;
            if (b.size.y > .6f || Mathf.Max(b.size.x, b.size.z) > 4.5f) continue;
            foreach (var hit in Physics.RaycastAll(b.center + Vector3.up * (b.extents.y + .2f),
                Vector3.down, b.size.y + .8f, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform == colisor.transform || !EhPiso(hit.collider.name) || hit.normal.y < .65f) continue;
                if (b.min.y - hit.point.y < -.3f || b.min.y - hit.point.y > .6f) continue;
                detalhes.Add(colisor);
                colisor.enabled = false;
                break;
            }
        }
        Physics.SyncTransforms();
    }

    Mesh Suavizar(MeshCollider colisor)
    {
        var origem = colisor.sharedMesh;
        var vertices = origem.vertices;
        var tris = origem.triangles;
        var grupos = new Dictionary<Vector3Int, int>();
        var posicoes = new List<Vector3>();
        var indices = new int[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 p = colisor.transform.TransformPoint(vertices[i]);
            var chave = new Vector3Int(Mathf.RoundToInt(p.x * 1000f), Mathf.RoundToInt(p.y * 1000f), Mathf.RoundToInt(p.z * 1000f));
            if (!grupos.TryGetValue(chave, out int id)) { id = posicoes.Count; grupos.Add(chave, id); posicoes.Add(p); }
            indices[i] = id;
        }
        var vizinhos = new HashSet<int>[posicoes.Count];
        var piso = new bool[posicoes.Count];
        var fixo = new bool[posicoes.Count];
        var arestas = new Dictionary<ulong, int>();
        for (int i = 0; i < vizinhos.Length; i++) vizinhos[i] = new HashSet<int>();
        for (int i = 0; i < tris.Length; i += 3)
        {
            int a = indices[tris[i]], b = indices[tris[i + 1]], c = indices[tris[i + 2]];
            if (a == b || a == c || b == c) continue;
            bool caminhavel = Vector3.Cross(posicoes[b] - posicoes[a], posicoes[c] - posicoes[a]).normalized.y >= .65f;
            foreach (int v in new[] { a, b, c }) { piso[v] |= caminhavel; fixo[v] |= !caminhavel; }
            AdicionarAresta(a, b, vizinhos, arestas);
            AdicionarAresta(b, c, vizinhos, arestas);
            AdicionarAresta(c, a, vizinhos, arestas);
        }
        // Mantem bordas abertas, buracos e encontros com paredes exatamente no lugar.
        foreach (var aresta in arestas)
            if (aresta.Value == 1) { fixo[(int)(aresta.Key >> 32)] = true; fixo[(int)(aresta.Key & uint.MaxValue)] = true; }
        var alturas = new float[posicoes.Count];
        for (int i = 0; i < alturas.Length; i++) alturas[i] = posicoes[i].y;
        for (int passo = 0; passo < 3; passo++)
        {
            var novas = (float[])alturas.Clone();
            for (int i = 0; i < alturas.Length; i++)
            {
                if (!piso[i] || fixo[i] || vizinhos[i].Count < 3) continue;
                float soma = 0f;
                foreach (int v in vizinhos[i]) soma += alturas[v];
                novas[i] = Mathf.Clamp(Mathf.Lerp(alturas[i], soma / vizinhos[i].Count, .5f),
                    posicoes[i].y - AjusteMaximo, posicoes[i].y + AjusteMaximo);
            }
            alturas = novas;
        }
        int alterados = 0;
        for (int i = 0; i < vertices.Length; i++)
        {
            int id = indices[i];
            float diferenca = alturas[id] - posicoes[id].y;
            if (Mathf.Abs(diferenca) < .001f) continue;
            Vector3 p = colisor.transform.TransformPoint(vertices[i]);
            p.y += diferenca;
            vertices[i] = colisor.transform.InverseTransformPoint(p);
            alterados++;
        }
        if (alterados == 0) return null;
        VerticesSuavizados += alterados;
        var copia = Instantiate(origem);
        copia.name = origem.name + " - colisao suavizada";
        copia.vertices = vertices;
        copia.RecalculateBounds();
        return copia;
    }

    static void AdicionarAresta(int a, int b, HashSet<int>[] vizinhos, Dictionary<ulong, int> arestas)
    {
        vizinhos[a].Add(b); vizinhos[b].Add(a);
        uint menor = (uint)Mathf.Min(a, b), maior = (uint)Mathf.Max(a, b);
        ulong chave = ((ulong)menor << 32) | maior;
        arestas.TryGetValue(chave, out int quantidade);
        arestas[chave] = quantidade + 1;
    }

    void OnDestroy()
    {
        foreach (var alteracao in malhas)
        {
            if (alteracao.colisor) { alteracao.colisor.sharedMesh = alteracao.original; alteracao.colisor.convex = alteracao.convexo; }
            if (alteracao.suave) Destroy(alteracao.suave);
        }
        foreach (var detalhe in detalhes) if (detalhe) detalhe.enabled = true;
        if (jogador) { jogador.stepOffset = degrauOriginal; jogador.skinWidth = peleOriginal; jogador.slopeLimit = inclinacaoOriginal; }
    }
}
