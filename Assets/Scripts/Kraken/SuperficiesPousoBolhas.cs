using System.Collections.Generic;
using UnityEngine;

// Consulta apenas as faces visiveis do piso, sem alterar os colisores do jogador.
public sealed class SuperficiesPousoBolhas
{
    struct Face
    {
        public Vector3 a, b, c, normal;
        public Bounds limites;
    }
    readonly List<Face> faces = new List<Face>();
    readonly List<Vector3> centros = new List<Vector3>();
    public SuperficiesPousoBolhas(Transform[] pisos)
    {
        var vistos = new HashSet<MeshFilter>();
        if (pisos == null) return;
        foreach (var piso in pisos)
        {
            if (!piso) continue;
            foreach (var filtro in piso.GetComponentsInChildren<MeshFilter>())
            {
                if (!vistos.Add(filtro) || !filtro.sharedMesh || !filtro.sharedMesh.isReadable) continue;
                var vertices = filtro.sharedMesh.vertices;
                var indices = filtro.sharedMesh.triangles;
                var renderer = filtro.GetComponent<Renderer>();
                if (renderer) centros.Add(renderer.bounds.center);
                for (int i = 0; i < indices.Length; i += 3)
                {
                    Vector3 a = filtro.transform.TransformPoint(vertices[indices[i]]);
                    Vector3 b = filtro.transform.TransformPoint(vertices[indices[i + 1]]);
                    Vector3 c = filtro.transform.TransformPoint(vertices[indices[i + 2]]);
                    Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                    if (normal.y < .5f) continue;
                    var limites = new Bounds(a, Vector3.zero); limites.Encapsulate(b); limites.Encapsulate(c);
                    faces.Add(new Face { a = a, b = b, c = c, normal = normal, limites = limites });
                }
            }
        }
    }
    public bool PisoAbaixo(Vector3 origem, float distancia, out Vector3 ponto)
    {
        ponto = default; bool achou = false; float altura = float.NegativeInfinity;
        foreach (var f in faces)
        {
            if (origem.x < f.limites.min.x || origem.x > f.limites.max.x
                || origem.z < f.limites.min.z || origem.z > f.limites.max.z) continue;
            float y = f.a.y - (f.normal.x * (origem.x - f.a.x) + f.normal.z * (origem.z - f.a.z)) / f.normal.y;
            if (y > origem.y || y < origem.y - distancia || y <= altura) continue;
            Vector3 p = new Vector3(origem.x, y, origem.z);
            if (Dentro(p, f)) { ponto = p; altura = y; achou = true; }
        }
        return achou;
    }
    public bool DestinoProximo(Vector3 preferido, out Vector3 ponto)
    {
        if (PisoAbaixo(preferido + Vector3.up * 200, 1000, out ponto)) return true;
        float menor = float.PositiveInfinity; bool achou = false;
        foreach (var centro in centros)
        {
            if (!PisoAbaixo(centro + Vector3.up * 200, 1000, out var candidato)) continue;
            float distancia = (Vector3.ProjectOnPlane(candidato - preferido, Vector3.up)).sqrMagnitude;
            if (distancia < menor) { menor = distancia; ponto = candidato; achou = true; }
        }
        return achou;
    }
    public bool Tocou(Vector3 centro, float raio)
    {
        foreach (var f in faces)
        {
            if (f.limites.SqrDistance(centro) > raio * raio) continue;
            float altura = Vector3.Dot(centro - f.a, f.normal);
            if (altura < 0 || altura > raio) continue;
            Vector3 projecao = centro - f.normal * altura;
            if (Dentro(projecao, f)) return true;
            if ((PontoNoSegmento(centro, f.a, f.b) - centro).sqrMagnitude <= raio * raio
                || (PontoNoSegmento(centro, f.b, f.c) - centro).sqrMagnitude <= raio * raio
                || (PontoNoSegmento(centro, f.c, f.a) - centro).sqrMagnitude <= raio * raio) return true;
        }
        return false;
    }
    static bool Dentro(Vector3 p, Face f) => Vector3.Dot(Vector3.Cross(f.b - f.a, p - f.a), f.normal) >= -.0001f
        && Vector3.Dot(Vector3.Cross(f.c - f.b, p - f.b), f.normal) >= -.0001f
        && Vector3.Dot(Vector3.Cross(f.a - f.c, p - f.c), f.normal) >= -.0001f;
    static Vector3 PontoNoSegmento(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        return a + ab * Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(.000001f, ab.sqrMagnitude));
    }
}
