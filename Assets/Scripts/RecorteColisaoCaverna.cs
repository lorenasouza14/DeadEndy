using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// A malha visual continua inteira. Somente a colisao perde o volume da entrada.
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshCollider))]
public class RecorteColisaoCaverna : MonoBehaviour
{
    [Tooltip("Caixa editavel que define o espaco livre da entrada. Seu collider fica desativado: serve apenas de guia para o recorte.")]
    public BoxCollider volumeLivre;
    private Mesh malhaGerada;
    private Mesh colisaoAnterior;

    private void Awake()
    {
        if (volumeLivre == null) return;
        var origem = GetComponent<MeshFilter>();
        var colisao = GetComponent<MeshCollider>();
        if (origem.sharedMesh == null || !origem.sharedMesh.isReadable)
        {
            Debug.LogError("RecorteColisaoCaverna: a malha visual precisa permitir leitura (Read/Write) para atualizar a entrada.", this);
            return;
        }
        colisaoAnterior = colisao.sharedMesh;
        malhaGerada = Gerar(origem, volumeLivre);
        colisao.convex = false;
        colisao.sharedMesh = malhaGerada;
    }

    public static Mesh Gerar(MeshFilter origem, BoxCollider abertura)
    {
        var verticesOriginais = origem.sharedMesh.vertices;
        var indicesOriginais = origem.sharedMesh.triangles;
        var vertices = new List<Vector3>();
        var indices = new List<int>();
        Vector3 metade = abertura.size * 0.5f;
        for (int i = 0; i < indicesOriginais.Length; i += 3)
        {
            var poligono = new List<Vector3>(3);
            for (int j = 0; j < 3; j++)
            {
                Vector3 mundo = origem.transform.TransformPoint(verticesOriginais[indicesOriginais[i + j]]);
                poligono.Add(abertura.transform.InverseTransformPoint(mundo) - abertura.center);
            }
            // Guarda cada parte externa e descarta somente o interior da caixa.
            for (int plano = 0; plano < 6 && poligono.Count >= 3; plano++)
            {
                int eixo = plano / 2;
                float sinal = plano % 2 == 0 ? 1f : -1f;
                var fora = Recortar(poligono, eixo, sinal, metade[eixo], false);
                if (fora.Count >= 3)
                {
                    int inicio = vertices.Count;
                    foreach (var p in fora)
                        vertices.Add(origem.transform.InverseTransformPoint(abertura.transform.TransformPoint(p + abertura.center)));
                    for (int j = 1; j < fora.Count - 1; j++)
                    {
                        if (Vector3.Cross(vertices[inicio + j] - vertices[inicio], vertices[inicio + j + 1] - vertices[inicio]).sqrMagnitude < 1e-12f) continue;
                        indices.Add(inicio); indices.Add(inicio + j); indices.Add(inicio + j + 1);
                    }
                }
                poligono = Recortar(poligono, eixo, sinal, metade[eixo], true);
            }
        }
        var malha = new Mesh { name = "Vulcao - colisao com abertura editavel", indexFormat = IndexFormat.UInt32 };
        malha.SetVertices(vertices);
        malha.SetTriangles(indices, 0);
        malha.RecalculateBounds();
        return malha;
    }

    private static List<Vector3> Recortar(List<Vector3> pontos, int eixo, float sinal, float limite, bool dentro)
    {
        var resultado = new List<Vector3>();
        if (pontos.Count == 0) return resultado;
        Vector3 anterior = pontos[pontos.Count - 1];
        float da = sinal * anterior[eixo] - limite;
        bool manterAnterior = dentro ? da <= 0f : da > 0f;
        foreach (var atual in pontos)
        {
            float db = sinal * atual[eixo] - limite;
            bool manterAtual = dentro ? db <= 0f : db > 0f;
            if (manterAnterior != manterAtual) resultado.Add(Vector3.LerpUnclamped(anterior, atual, da / (da - db)));
            if (manterAtual) resultado.Add(atual);
            anterior = atual; da = db; manterAnterior = manterAtual;
        }
        return resultado;
    }

    private void OnDestroy()
    {
        if (malhaGerada == null) return;
        var colisao = GetComponent<MeshCollider>();
        if (colisao != null && colisao.sharedMesh == malhaGerada) colisao.sharedMesh = colisaoAnterior;
        Destroy(malhaGerada);
    }

    private void OnDrawGizmosSelected()
    {
        if (volumeLivre == null) return;
        var anterior = Gizmos.matrix;
        Gizmos.matrix = volumeLivre.transform.localToWorldMatrix;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(volumeLivre.center, volumeLivre.size);
        Gizmos.matrix = anterior;
    }
}
