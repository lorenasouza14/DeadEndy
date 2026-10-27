using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class EfeitoPirulito : MonoBehaviour
{
    [Header("Configuração")]
    [Min(0.1f)] public float tempoDePaz = 8f;
    private bool naMao;
    private bool jaFoiArremessado;
    private bool acertou;
    private Transform dono;
    public bool DisponivelParaColeta => !naMao && !jaFoiArremessado && !acertou;

    public void PrepararNaMao()
    {
        naMao = true;
        var corpo = GetComponent<Rigidbody>();
        corpo.isKinematic = true;
        corpo.useGravity = false;
        foreach (Collider colisor in GetComponentsInChildren<Collider>()) colisor.enabled = false;
        var item = GetComponent<ItemColetavel>();
        if (item != null) item.enabled = false;
    }

    public void ConfigurarArremesso(Transform jogador, Vector3 velocidade)
    {
        if (jaFoiArremessado || acertou) return;
        dono = jogador;
        naMao = false;
        jaFoiArremessado = true;
        transform.SetParent(null, true);
        var item = GetComponent<ItemColetavel>();
        if (item != null) item.enabled = false;
        var esfera = GetComponent<SphereCollider>();
        esfera.center = new Vector3(0, 0.36f, 0);
        esfera.radius = 0.15f;
        esfera.isTrigger = false;
        esfera.enabled = true;
        if (dono != null)
            foreach (Collider colisor in dono.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(esfera, colisor);
        var corpo = GetComponent<Rigidbody>();
        corpo.isKinematic = false;
        corpo.useGravity = true;
        corpo.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        corpo.linearVelocity = velocidade;
        Destroy(gameObject, 5f);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!jaFoiArremessado || acertou) return;
        if (dono != null && collision.transform.IsChildOf(dono)) return;
        acertou = true;
        var chefe = collision.collider.GetComponentInParent<CaranguejoChefe>();
        if (chefe != null) chefe.FicarCalmo(tempoDePaz);
        Destroy(gameObject);
    }
}
