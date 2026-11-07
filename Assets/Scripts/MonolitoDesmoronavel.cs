using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public class MonolitoDesmoronavel : MonoBehaviour
{
    public Collider corpoInteiro;
    public GameObject fragmentos;
    public Rigidbody banana;
    public ParticleSystem poeira;
    public CinemachineImpulseSource tremor;
    [Min(0.1f)] public float antecipacao = 0.7f;
    [Min(0.5f)] public float tempoDosFragmentos = 3f;
    [Min(0f)] public float intensidadeTremor = 0.35f;
    public bool Iniciado { get; private set; }
    public bool Desabou { get; private set; }
    public bool Concluido { get; private set; }
    private float tempo;
    private Vector3 repousoPosicao;
    private Quaternion repousoRotacao;
    private Rigidbody[] pedras;
    private Vector3[] escalas;
    private bool preparado;

    private void Awake()
    {
        if (corpoInteiro == null || fragmentos == null || banana == null)
        {
            Debug.LogError("MonolitoDesmoronavel: configure o corpo, os fragmentos e a banana.", this);
            enabled = false;
            return;
        }
        repousoPosicao = corpoInteiro.transform.localPosition;
        repousoRotacao = corpoInteiro.transform.localRotation;
        pedras = fragmentos.GetComponentsInChildren<Rigidbody>(true);
        escalas = new Vector3[pedras.Length];
        for (int i = 0; i < pedras.Length; i++) escalas[i] = pedras[i].transform.localScale;
        preparado = true;
    }

    public bool Desmoronar()
    {
        if (!preparado || Iniciado || !isActiveAndEnabled) return false;
        Iniciado = true;
        tempo = 0f;
        return true;
    }

    private void Update()
    {
        if (!Iniciado || Concluido || Time.deltaTime <= 0f) return;
        tempo += Time.deltaTime;
        if (tempo < antecipacao)
        {
            float forca = tempo / antecipacao;
            corpoInteiro.transform.localPosition = repousoPosicao + new Vector3(Mathf.Sin(tempo * 61f), 0f, Mathf.Sin(tempo * 49f)) * (0.025f * forca);
            corpoInteiro.transform.localRotation = repousoRotacao * Quaternion.Euler(Mathf.Sin(tempo * 47f) * forca, 0f, Mathf.Sin(tempo * 57f) * forca);
            return;
        }
        if (!Desabou) Quebrar();
        float restante = tempo - antecipacao;
        float escala = 1f - Mathf.Clamp01((restante - tempoDosFragmentos + 0.8f) / 0.8f);
        for (int i = 0; i < pedras.Length; i++) pedras[i].transform.localScale = escalas[i] * Mathf.Max(0.01f, escala);
        if (restante >= tempoDosFragmentos)
        {
            fragmentos.SetActive(false);
            Concluido = true;
        }
    }

    private void Quebrar()
    {
        Desabou = true;
        corpoInteiro.transform.localPosition = repousoPosicao;
        corpoInteiro.transform.localRotation = repousoRotacao;
        Vector3 centro = corpoInteiro.bounds.center;
        corpoInteiro.gameObject.SetActive(false);
        fragmentos.SetActive(true);
        var jogador = FindAnyObjectByType<StarterAssets.ThirdPersonController>();
        var capsula = jogador != null ? jogador.GetComponent<CharacterController>() : null;
        var colisaoBanana = banana.GetComponent<Collider>();
        for (int i = 0; i < pedras.Length; i++)
        {
            var col = pedras[i].GetComponent<Collider>();
            Physics.IgnoreCollision(col, colisaoBanana);
            if (capsula != null) Physics.IgnoreCollision(col, capsula);
            pedras[i].isKinematic = false;
            Vector3 fora = Vector3.ProjectOnPlane(pedras[i].worldCenterOfMass - centro, Vector3.up).normalized;
            pedras[i].AddForce(fora * 1.8f + Vector3.up * 0.4f, ForceMode.VelocityChange);
            pedras[i].AddTorque(new Vector3(i % 3 - 1f, 0.5f, (i + 1) % 3 - 1f) * 2f, ForceMode.VelocityChange);
        }
        banana.isKinematic = false;
        banana.useGravity = true;
        banana.WakeUp();
        if (poeira != null) poeira.Play();
        if (tremor != null) tremor.GenerateImpulseWithVelocity(new Vector3(0.5f, 1f, 0.2f) * intensidadeTremor);
    }

    private void OnDisable()
    {
        if (preparado && corpoInteiro != null && !Desabou)
        {
            corpoInteiro.transform.localPosition = repousoPosicao;
            corpoInteiro.transform.localRotation = repousoRotacao;
        }
    }
}
