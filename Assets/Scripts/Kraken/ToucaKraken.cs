using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
public class ToucaKraken : MonoBehaviour
{
    public Transform encaixeNaCabeca;
    [Min(.1f)] public float gravidade = 30f;
    public bool Caindo { get; private set; }
    public bool Encaixou { get; private set; }
    Rigidbody corpo;
    float velocidade;
    void Awake() { corpo = GetComponent<Rigidbody>(); corpo.isKinematic = true; corpo.useGravity = false; }
    public void Soltar() { if (!Encaixou && encaixeNaCabeca) Caindo = true; }
    void FixedUpdate()
    {
        if (!Caindo || !encaixeNaCabeca) return;
        velocidade += gravidade * Time.fixedDeltaTime;
        Vector3 destino = encaixeNaCabeca.position;
        Vector3 p = corpo.position;
        p.x = Mathf.MoveTowards(p.x, destino.x, 35f * Time.fixedDeltaTime);
        p.z = Mathf.MoveTowards(p.z, destino.z, 35f * Time.fixedDeltaTime);
        p.y = Mathf.MoveTowards(p.y, destino.y, velocidade * Time.fixedDeltaTime);
        corpo.MovePosition(p);
        corpo.MoveRotation(Quaternion.RotateTowards(corpo.rotation, encaixeNaCabeca.rotation, 90f * Time.fixedDeltaTime));
        if ((p - destino).sqrMagnitude < .01f)
        {
            Caindo = false; Encaixou = true;
            transform.SetPositionAndRotation(destino, encaixeNaCabeca.rotation);
            transform.SetParent(encaixeNaCabeca, true);
        }
    }
}
