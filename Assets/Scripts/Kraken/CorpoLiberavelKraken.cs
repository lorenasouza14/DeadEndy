using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
public class CorpoLiberavelKraken : MonoBehaviour
{
    public Collider[] colisoresFixos;
    public Collider[] colisoresDaQueda;
    public bool Liberado { get; private set; }
    Rigidbody corpo;
    void Awake()
    {
        corpo = GetComponent<Rigidbody>();
        corpo.isKinematic = true;
        corpo.useGravity = false;
        foreach (var c in colisoresDaQueda) if (c) c.enabled = false;
    }
    public void Liberar(Vector3 direcao, float impulso = 4f)
    {
        if (Liberado) return;
        Liberado = true;
        foreach (var c in colisoresFixos) if (c) c.enabled = false;
        foreach (var c in colisoresDaQueda) if (c) c.enabled = true;
        gameObject.isStatic = false;
        transform.SetParent(null, true);
        corpo.isKinematic = false;
        corpo.useGravity = true;
        corpo.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        corpo.AddForce(direcao.normalized * impulso, ForceMode.VelocityChange);
        // Aplica torque para tombar, em vez de somente afundar verticalmente.
        corpo.AddTorque(Vector3.Cross(Vector3.up, direcao.normalized) * impulso * .4f, ForceMode.VelocityChange);
    }
}
