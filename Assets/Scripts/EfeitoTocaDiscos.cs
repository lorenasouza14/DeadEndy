using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(BoxCollider), typeof(AudioSource))]
public class EfeitoTocaDiscos : MonoBehaviour
{
    [Header("Música e distração")]
    public AudioClip musicaRelaxante;
    [Min(0.1f)] public float tempoDePaz = 15f;
    [Min(1f)] public float alcanceMusica = 80f;
    public Vector3 rotacaoNaMao = new Vector3(0, 90, 0);
    public bool NaMao { get; private set; }
    public bool Colocado { get; private set; }
    public bool Ligado { get; private set; }
    public bool DisponivelParaColeta => !NaMao && !Ligado;
    private AudioSource fonte;
    private CaranguejoChefe chefeOuvindo;
    private float proximaBusca;
    private int frameColocacao = -1;
    public string Aviso { get; private set; }

    void Awake()
    {
        fonte = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        fonte.playOnAwake = false;
        fonte.loop = true;
        fonte.spatialBlend = 1f;
        // A distância muda o volume, mas o movimento não altera o tom da música.
        fonte.dopplerLevel = 0f;
        fonte.pitch = 1f;
        fonte.rolloffMode = AudioRolloffMode.Linear;
        fonte.minDistance = 4f;
        fonte.maxDistance = alcanceMusica;
        fonte.clip = musicaRelaxante;
        fonte.Stop();
        GetComponent<Rigidbody>().isKinematic = true;
        GetComponent<Rigidbody>().useGravity = false;
    }

    public void PrepararNaMao(Transform mao)
    {
        NaMao = true;
        Colocado = false;
        Aviso = null;
        foreach (Collider colisor in GetComponentsInChildren<Collider>()) colisor.enabled = false;
        GetComponent<ItemColetavel>().enabled = false;
        transform.SetParent(mao, false);
        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.Euler(rotacaoNaMao);
        Bounds limites = GetComponent<AjusteDeItem>().CalcularLimites(transform);
        // O topo do modelo é a alça: pendura o rádio pela mão.
        transform.localPosition = -(transform.localRotation * new Vector3(limites.center.x, limites.max.y, limites.center.z));
    }

    public bool TentarColocar(Transform jogador)
    {
        if (!NaMao) return false;
        Vector3 frente = Vector3.ProjectOnPlane(jogador.forward, Vector3.up).normalized;
        Vector3 origem = jogador.position + frente * 1.4f + Vector3.up * 2f;
        RaycastHit? chao = null;
        foreach (var hit in Physics.RaycastAll(origem, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(jogador) || hit.transform.IsChildOf(transform)) continue;
            if (!chao.HasValue || hit.distance < chao.Value.distance) chao = hit;
        }
        if (!chao.HasValue || Vector3.Angle(chao.Value.normal, Vector3.up) > 35f)
        {
            Aviso = "Aproxime-se de um chão plano para colocar o rádio.";
            return false;
        }
        Bounds limites = GetComponent<AjusteDeItem>().CalcularLimites(transform);
        Quaternion giro = Quaternion.LookRotation(-frente, Vector3.up);
        Vector3 baseRadio = chao.Value.point + Vector3.up * 0.025f;
        Vector3 centro = baseRadio + Vector3.up * (limites.size.y * 0.5f);
        Vector3 inicio = jogador.position + Vector3.up * 0.5f;
        Vector3 caminho = centro - inicio;
        foreach (var hit in Physics.RaycastAll(inicio, caminho.normalized, caminho.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(jogador) || hit.transform.IsChildOf(transform)) continue;
            Aviso = "Há um obstáculo na frente do rádio.";
            return false;
        }
        foreach (Collider obstaculo in Physics.OverlapBox(centro, limites.extents * 0.95f, giro, ~0, QueryTriggerInteraction.Ignore))
        {
            if (obstaculo.transform.IsChildOf(jogador) || obstaculo.transform.IsChildOf(transform)) continue;
            Aviso = "Escolha um espaço livre para o rádio.";
            return false;
        }
        transform.SetParent(null, false);
        transform.localScale = Vector3.one;
        transform.SetPositionAndRotation(baseRadio - giro * new Vector3(limites.center.x, limites.min.y, limites.center.z), giro);
        NaMao = false;
        Colocado = true;
        Aviso = null;
        frameColocacao = Time.frameCount;
        GetComponent<BoxCollider>().enabled = true;
        GetComponent<ItemColetavel>().enabled = true;
        return true;
    }

    public bool AlternarSom()
    {
        if (!Colocado || NaMao || frameColocacao == Time.frameCount) return false;
        if (Ligado)
        {
            Ligado = false;
            fonte.Stop();
            if (chefeOuvindo != null) chefeOuvindo.RadioDesligado(this);
        }
        else
        {
            if (musicaRelaxante == null) { Aviso = "O rádio está sem música configurada."; return false; }
            Aviso = null;
            Ligado = true;
            fonte.clip = musicaRelaxante;
            fonte.Play();
            proximaBusca = 0;
            NotificarChefe();
        }
        return true;
    }

    void Update()
    {
        if (Ligado && Time.time >= proximaBusca) NotificarChefe();
    }

    void NotificarChefe()
    {
        proximaBusca = Time.time + 0.5f;
        var chefe = chefeOuvindo != null && chefeOuvindo.isActiveAndEnabled ? chefeOuvindo : FindAnyObjectByType<CaranguejoChefe>();
        if (chefe == null || Vector3.Distance(chefe.transform.position, transform.position) > alcanceMusica) return;
        if (chefe.OuvirRadio(this, tempoDePaz)) chefeOuvindo = chefe;
    }

    void OnDisable()
    {
        if (Ligado && chefeOuvindo != null) chefeOuvindo.RadioDesligado(this);
        Ligado = false;
        if (fonte != null) fonte.Stop();
    }
}

