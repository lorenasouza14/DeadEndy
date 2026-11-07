using StarterAssets;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public class ArmadilhaMola : MonoBehaviour
{
    public Animator animador;
    public Transform pontoDeQueda;
    [Min(1f)] public float alturaDoArco = 7f;
    [Min(0f)] public float intervaloParaRearmar = 0.4f;
    public string estadoAnimacao = "Base Layer.FBX_Temp";

    public bool Disparada { get; private set; }
    private BoxCollider area;
    private int hashAnimacao;
    private float rearmarEm;

    private void Awake()
    {
        area = GetComponent<BoxCollider>();
        area.isTrigger = true;
        var corpo = GetComponent<Rigidbody>();
        corpo.isKinematic = true;
        corpo.useGravity = false;
        if (animador == null) animador = GetComponentInChildren<Animator>();
        hashAnimacao = Animator.StringToHash(estadoAnimacao);
    }

    private void Start()
    {
        if (pontoDeQueda == null || animador == null || animador.runtimeAnimatorController == null ||
            !animador.HasState(0, hashAnimacao))
        {
            Debug.LogError("ArmadilhaMola: configure o ponto de queda e o Animator da Spring Trap.", this);
            enabled = false;
            return;
        }
        animador.applyRootMotion = false;
        animador.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        Repousar();
    }

    private void OnTriggerEnter(Collider other) { TentarDisparar(other); }
    private void OnTriggerStay(Collider other) { TentarDisparar(other); }

    private void TentarDisparar(Collider other)
    {
        if (!isActiveAndEnabled || Disparada || Time.time < rearmarEm || Time.deltaTime <= 0f) return;
        var movimento = other.GetComponentInParent<ThirdPersonController>();
        if (movimento == null || !movimento.isActiveAndEnabled || movimento.EstaArremessado) return;
        var capsula = movimento.GetComponent<CharacterController>();
        if (capsula == null || !capsula.enabled || (!movimento.Grounded && !capsula.isGrounded)) return;
        var vida = movimento.GetComponentInChildren<VidaEndy>();
        if (vida != null && vida.EstaMorto) return;

        // A capsula pode encostar de lado ou passar por cima. O pe precisa estar sobre a placa.
        Vector3 pe = new Vector3(capsula.bounds.center.x, capsula.bounds.min.y + capsula.skinWidth, capsula.bounds.center.z);
        Vector3 local = transform.InverseTransformPoint(pe) - area.center;
        Vector3 limite = area.size * 0.5f;
        if (Mathf.Abs(local.x) > limite.x || Mathf.Abs(local.z) > limite.z ||
            local.y < -limite.y - 0.15f || local.y > limite.y + 0.1f) return;

        // O marcador indica o chao. Compensa a distancia entre a origem do Endy e seus pes.
        float alturaDaOrigem = movimento.transform.position.y - capsula.bounds.min.y;
        Vector3 destino = pontoDeQueda.position + Vector3.up * (alturaDaOrigem + capsula.skinWidth + 0.05f);
        if (!movimento.ReceberCatapulta(destino, alturaDoArco)) return;
        Disparada = true;
        animador.speed = 1f;
        animador.Play(hashAnimacao, 0, 0f);
    }

    private void Update()
    {
        if (!Disparada || Time.deltaTime <= 0f) return;
        var estado = animador.GetCurrentAnimatorStateInfo(0);
        if (estado.fullPathHash != hashAnimacao || estado.normalizedTime < 0.98f) return;
        Repousar();
        rearmarEm = Time.time + intervaloParaRearmar;
    }

    private void Repousar()
    {
        Disparada = false;
        animador.Play(hashAnimacao, 0, 0f);
        animador.Update(0f);
        animador.speed = 0f;
    }

    private void OnDisable()
    {
        Disparada = false;
        if (animador != null && animador.isActiveAndEnabled && animador.runtimeAnimatorController != null)
            Repousar();
    }
}
