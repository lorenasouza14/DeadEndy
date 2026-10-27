using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class PocaoMagica : MonoBehaviour
{
    public enum TipoPocao { Encolher, Velocidade }
    public TipoPocao tipo;
    [Min(0.1f)] public float tempoDeEfeito = 10f;
    [Range(0.1f, 1f)] public float fatorEncolhimento = 0.5f;
    [Min(1f)] public float multiplicadorVelocidade = 2f;
    [Tooltip("Raio físico do frasco, menor que a área usada para coletar.")]
    public float raioArremesso = 0.12f;
    public GameObject prefabEfeitoSplash;
    [Tooltip("Regra antiga: perder ao errar a poção de encolher.")]
    public bool perderAoErrar = true;

    private VidaEndy vidaEndy;
    private Transform dono;
    private bool naMao;
    private bool arremessada;
    private bool jaBateu;
    public bool DisponivelParaColeta => !naMao && !arremessada && !jaBateu;

    public void PrepararNaMao()
    {
        naMao = true;
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        foreach (Collider colisor in GetComponentsInChildren<Collider>()) colisor.enabled = false;
        ItemColetavel item = GetComponent<ItemColetavel>();
        if (item != null) item.enabled = false;
    }

    public void ConfigurarArremesso(VidaEndy vidaDoJogador, Collider colisorDoJogador, Vector3 velocidadeInicial)
    {
        if (arremessada || jaBateu) return;
        vidaEndy = vidaDoJogador;
        dono = vidaDoJogador != null ? vidaDoJogador.transform :
            (colisorDoJogador != null ? colisorDoJogador.transform : null);
        naMao = false;
        arremessada = true;
        transform.SetParent(null, true);
        ItemColetavel item = GetComponent<ItemColetavel>();
        if (item != null) item.enabled = false;
        foreach (Collider colisor in GetComponentsInChildren<Collider>())
        {
            colisor.enabled = true;
            colisor.isTrigger = false;
            if (colisor is SphereCollider esfera) esfera.radius = raioArremesso;
            if (dono != null)
                foreach (Collider colisorDono in dono.GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(colisor, colisorDono);
        }
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.linearVelocity = velocidadeInicial;
        Invoke(nameof(ErrouArremesso), 5f);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!arremessada || jaBateu) return;
        if (dono != null && collision.transform.IsChildOf(dono)) return;
        jaBateu = true;
        CancelInvoke(nameof(ErrouArremesso));
        if (prefabEfeitoSplash != null)
        {
            Vector3 impacto = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            Destroy(Instantiate(prefabEfeitoSplash, impacto, Quaternion.identity), 3f);
        }
        CaranguejoChefe chefe = collision.collider.GetComponentInParent<CaranguejoChefe>();
        if (chefe == null) { ErrouArremesso(); return; }
        if (tipo == TipoPocao.Encolher) chefe.TomarPocaoEncolhedora();
        else chefe.FicarRapido(tempoDeEfeito);
        Destroy(gameObject);
    }

    void ErrouArremesso()
    {
        jaBateu = true;
        CancelInvoke();
        if (perderAoErrar && tipo == TipoPocao.Encolher && vidaEndy != null) vidaEndy.ForcarDerrota();
        Destroy(gameObject);
    }
}
