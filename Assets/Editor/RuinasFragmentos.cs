using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Recorta a malha original e fecha as faces internas para o desmoronamento.
public static class RuinasFragmentos
{
    private struct Vertice
    {
        public Vector3 p;
        public Vector3 n;
        public Vector2 uv;
        public static Vertice Entre(Vertice a, Vertice b, float t)
            => new Vertice { p = Vector3.Lerp(a.p, b.p, t), n = Vector3.Lerp(a.n, b.n, t).normalized, uv = Vector2.Lerp(a.uv, b.uv, t) };
    }
    private class Face
    {
        public List<Vertice> vertices;
        public int material;
    }

    public static Mesh[] Criar(Mesh origem, string caminho)
    {
        var existentes = AssetDatabase.LoadAllAssetsAtPath(caminho).OfType<Mesh>().OrderBy(m => m.name).ToArray();
        if (existentes.Length > 0) return existentes;
        var vertices = origem.vertices;
        var normais = origem.normals;
        var uvs = origem.uv;
        var faces = new List<Face>();
        for (int s = 0; s < origem.subMeshCount; s++)
        {
            var indices = origem.GetTriangles(s);
            for (int i = 0; i < indices.Length; i += 3)
            {
                var face = new Face { material = s, vertices = new List<Vertice>() };
                for (int v = 0; v < 3; v++)
                {
                    int indice = indices[i + v];
                    face.vertices.Add(new Vertice { p = vertices[indice], n = normais[indice], uv = uvs.Length > indice ? uvs[indice] : Vector2.zero });
                }
                faces.Add(face);
            }
        }
        var bounds = origem.bounds;
        var meshes = new List<Mesh>();
        for (int y = 0; y < 4; y++)
        for (int lado = 0; lado < 2; lado++)
        {
            // Cortes alternados evitam uma pilha de fatias regulares.
            Vector3 eixo = Quaternion.Euler(0f, y * 37f, 0f) * Vector3.right;
            if (lado == 1) eixo = -eixo;
            var pedaco = Cortar(faces, Vector3.up, bounds.min.y + bounds.size.y * y / 4f, origem.subMeshCount);
            pedaco = Cortar(pedaco, Vector3.down, -(bounds.min.y + bounds.size.y * (y + 1) / 4f), origem.subMeshCount);
            pedaco = Cortar(pedaco, eixo, Vector3.Dot(eixo, bounds.center), origem.subMeshCount);
            var mesh = Montar(pedaco, origem.subMeshCount + 1);
            mesh.name = "Monolito_fragmento_" + meshes.Count.ToString("00");
            if (meshes.Count == 0) AssetDatabase.CreateAsset(mesh, caminho);
            else AssetDatabase.AddObjectToAsset(mesh, caminho);
            meshes.Add(mesh);
        }
        AssetDatabase.SaveAssets();
        return meshes.ToArray();
    }

    private static List<Face> Cortar(List<Face> faces, Vector3 normal, float distancia, int materialInterior)
    {
        var resultado = new List<Face>();
        var borda = new List<Vector3>();
        foreach (var face in faces)
        {
            var recorte = new List<Vertice>();
            for (int i = 0; i < face.vertices.Count; i++)
            {
                var a = face.vertices[i];
                var b = face.vertices[(i + 1) % face.vertices.Count];
                float da = Vector3.Dot(normal, a.p) - distancia;
                float db = Vector3.Dot(normal, b.p) - distancia;
                bool dentroA = da >= -0.00001f;
                bool dentroB = db >= -0.00001f;
                if (dentroA) recorte.Add(a);
                if (dentroA == dentroB) continue;
                var ponto = Vertice.Entre(a, b, Mathf.Clamp01(da / (da - db)));
                recorte.Add(ponto);
                if (!borda.Any(v => (v - ponto.p).sqrMagnitude < 0.0000001f)) borda.Add(ponto.p);
            }
            if (recorte.Count >= 3) resultado.Add(new Face { vertices = recorte, material = face.material });
        }
        if (borda.Count >= 3)
        {
            Vector3 centro = borda.Aggregate(Vector3.zero, (total, p) => total + p) / borda.Count;
            Vector3 u = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 v = Vector3.Cross(-normal, u);
            borda.Sort((a, b) => Mathf.Atan2(Vector3.Dot(a - centro, v), Vector3.Dot(a - centro, u))
                .CompareTo(Mathf.Atan2(Vector3.Dot(b - centro, v), Vector3.Dot(b - centro, u))));
            resultado.Add(new Face { material = materialInterior, vertices = borda.Select(p => new Vertice
            {
                p = p, n = -normal, uv = new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, v))
            }).ToList() });
        }
        return resultado;
    }

    private static Mesh Montar(List<Face> faces, int materiais)
    {
        var vertices = new List<Vector3>();
        var normais = new List<Vector3>();
        var uvs = new List<Vector2>();
        var indices = new List<int>[materiais];
        for (int i = 0; i < materiais; i++) indices[i] = new List<int>();
        foreach (var face in faces)
        {
            int inicio = vertices.Count;
            foreach (var v in face.vertices) { vertices.Add(v.p); normais.Add(v.n); uvs.Add(v.uv); }
            for (int i = 1; i < face.vertices.Count - 1; i++)
                indices[face.material].AddRange(new[] { inicio, inicio + i, inicio + i + 1 });
        }
        var mesh = new Mesh { subMeshCount = materiais };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normais);
        mesh.SetUVs(0, uvs);
        for (int i = 0; i < materiais; i++) mesh.SetTriangles(indices[i], i);
        mesh.RecalculateBounds();
        return mesh;
    }
}
