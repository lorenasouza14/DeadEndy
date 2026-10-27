using UnityEngine;

public class AjusteDeItem : MonoBehaviour
{
    [Header("Ajuste Visual no Baú")]
    [Tooltip("Posição (X, Y, Z) que este item deve ficar quando o baú abrir")]
    public Vector3 posicaoCustomizada = new Vector3(0f, 0.2f, 0f);

    [Tooltip("Rotação/Giro (X, Y, Z) para deitar ou virar o item")]
    public Vector3 rotacaoCustomizada = Vector3.zero;

    [Tooltip("Centraliza a malha no baú; a altura indica a base do objeto, não o pivô do modelo.")]
    public bool centralizarNoBau;

    public void Aplicar()
    {
        transform.localPosition = posicaoCustomizada;
        transform.localEulerAngles = rotacaoCustomizada;
        if (!centralizarNoBau || transform.parent == null) return;
        Bounds limites = CalcularLimites(transform.parent);
        transform.localPosition += new Vector3(posicaoCustomizada.x - limites.center.x,
            posicaoCustomizada.y - limites.min.y, posicaoCustomizada.z - limites.center.z);
    }

    public Bounds CalcularLimites(Transform referencia)
    {
        Bounds limites = new Bounds();
        bool iniciou = false;
        foreach (MeshFilter filtro in GetComponentsInChildren<MeshFilter>(true))
        {
            if (filtro.sharedMesh == null) continue;
            Bounds malha = filtro.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 canto = malha.center + Vector3.Scale(malha.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 ponto = referencia.InverseTransformPoint(filtro.transform.TransformPoint(canto));
                if (!iniciou) { limites = new Bounds(ponto, Vector3.zero); iniciou = true; }
                else limites.Encapsulate(ponto);
            }
        }
        return limites;
    }
}
