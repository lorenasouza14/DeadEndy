using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class EfeitoTrompete : MonoBehaviour
{
    [Header("Configuração do Caos")]
    public float tempoDeFuria = 12f;
    [Header("Gesto de tocar")]
    [Min(0.1f)] public float tempoLevantar = 0.45f;
    [Min(0.1f)] public float tempoTocando = 1.1f;
    [Min(0.1f)] public float tempoBaixar = 0.35f;
    public AudioClip somTrompete;
    [Tooltip("Bocal no espaço local do prefab, já com a escala da malha aplicada.")]
    public Vector3 bocalLocal = new Vector3(-0.0464f, 0.0005f, 0.244f);
    public Vector3 posicaoNaMao = new Vector3(0, 0.04f, 0);
    public Vector3 rotacaoNaMao = new Vector3(0, 90, 0);
    private bool coletado;
    private bool jaTocou;
    public bool DisponivelParaColeta => !coletado && !jaTocou;
    public bool JaTocou => jaTocou;

    public void PrepararNaMao()
    {
        coletado = true;
        var corpo = GetComponent<Rigidbody>();
        corpo.isKinematic = true;
        corpo.useGravity = false;
        foreach (Collider colisor in GetComponentsInChildren<Collider>()) colisor.enabled = false;
        var item = GetComponent<ItemColetavel>();
        if (item != null) item.enabled = false;
    }

    public void AplicarEfeito()
    {
        if (!coletado || jaTocou) return;
        jaTocou = true;
        if (somTrompete != null) AudioSource.PlayClipAtPoint(somTrompete, transform.position);
        // Procura no momento do uso: o chefe pode ter sido ativado depois da coleta.
        var chefe = FindAnyObjectByType<CaranguejoChefe>();
        if (chefe == null) return;
        chefe.ReagirComRaiva();
        chefe.FicarRapido(tempoDeFuria);
    }
}
