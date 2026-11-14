using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Alterna a luz da lanterna com o botao esquerdo do mouse.
/// Pode ficar no modelo da lanterna antes ou depois de ele ser colocado na mao.
/// </summary>
[DefaultExecutionOrder(350)]
public class LanternaJogador : MonoBehaviour
{
    public static LanternaJogador Instancia { get; private set; }
    [Header("Luz da lanterna")]
    [SerializeField] private Light luzDaLanterna;
    [SerializeField] private Color corDaLuz = new Color(1f, 0.9f, 0.65f);
    [SerializeField, Min(0f)] private float intensidade = 5f;
    [SerializeField, Min(0.1f)] private float alcance = 18f;
    [SerializeField, Range(1f, 179f)] private float angulo = 55f;
    [SerializeField] private Vector3 posicaoDaLuz = new Vector3(0f, 0f, 0.15f);
    [SerializeField] private Vector3 rotacaoDaLuz;
    [SerializeField] private bool iniciarLigada;

    [Header("Estado Lanterna")]
    public bool foiColetada = false;

    [Header("Feixe visivel no ar")]
    [SerializeField] private bool mostrarFeixe = true;
    [SerializeField, Min(0.5f)] private float comprimentoDoFeixe = 12f;
    [SerializeField, Min(0.05f)] private float larguraFinalDoFeixe = 1.5f;
    [SerializeField, Range(0.01f, 0.35f)] private float transparenciaDoFeixe = 0.06f;
    [SerializeField] private Color corDoFeixe = new Color(1f, 0.92f, 0.65f, 1f);

    private GameObject feixeVisual;
    private Transform personagem;
    public void AtualizarDirecao() { AlinharFeixeComPersonagem(); }
    public bool Ligada => luzDaLanterna != null && luzDaLanterna.enabled;
    public Transform AlvoDeMiraTemporario { get; set; }
    public event System.Action<bool> EstadoAlterado;
    public void DefinirLigada(bool ligada) { AtualizarLuz(ligada); }

    private void Awake()
    {
        // Uma lanterna ja coletada pode acompanhar o jogador persistente de outra fase.
        if (Instancia == null || !Instancia.foiColetada)
            Instancia = this;
        if (luzDaLanterna == null)
            luzDaLanterna = GetComponentInChildren<Light>(true);

        if (luzDaLanterna == null)
        {
            GameObject objetoDaLuz = new GameObject("Luz da Lanterna");
            objetoDaLuz.transform.SetParent(transform, false);
            luzDaLanterna = objetoDaLuz.AddComponent<Light>();
            luzDaLanterna.type = LightType.Spot;
            luzDaLanterna.shadows = LightShadows.None;
        }

        luzDaLanterna.transform.localPosition = posicaoDaLuz;
        luzDaLanterna.transform.localEulerAngles = rotacaoDaLuz;
        CriarFeixeVisual();
        AtualizarLuz(iniciarLigada);
        if (foiColetada) ColetarLanterna();
    }

    private void OnDestroy()
    {
        if (Instancia == this)
            Instancia = null;
    }

    private void Update()
    {
        // S� permite ligar/desligar se a lanterna j� foi coletada
        if (foiColetada && Time.timeScale > 0f && Input.GetMouseButtonDown(0))
            AtualizarLuz(!luzDaLanterna.enabled);
    }

    private void LateUpdate()
    {
        AlinharFeixeComPersonagem();
    }

    private void AlinharFeixeComPersonagem()
    {
        if (!foiColetada || luzDaLanterna == null) return;
        if (personagem == null || !transform.IsChildOf(personagem))
        {
            var controlador = GetComponentInParent<StarterAssets.ThirdPersonController>();
            personagem = controlador != null ? controlador.transform : null;
        }
        if (personagem == null && AlvoDeMiraTemporario == null) return;

        // A animacao do pulso nao deve desviar o feixe para os lados.
        // O cone visivel e filho da luz e acompanha a mesma orientacao.
        Vector3 frente = AlvoDeMiraTemporario != null
            ? AlvoDeMiraTemporario.position-luzDaLanterna.transform.position
            : Vector3.ProjectOnPlane(personagem.forward, Vector3.up);
        if (frente.sqrMagnitude > .0001f)
            luzDaLanterna.transform.rotation = Quaternion.LookRotation(frente, Vector3.up);
    }

    private void AtualizarLuz(bool ligar)
    {
        bool mudou = luzDaLanterna.enabled != ligar;
        luzDaLanterna.color = corDaLuz;
        luzDaLanterna.intensity = intensidade;
        luzDaLanterna.range = alcance;
        luzDaLanterna.spotAngle = angulo;
        luzDaLanterna.enabled = ligar;

        if (feixeVisual != null)
            feixeVisual.SetActive(ligar && mostrarFeixe);
        if (mudou) EstadoAlterado?.Invoke(ligar);
    }

    /// <summary>Verifica se um alvo esta dentro do foco e sem uma parede entre ele e a lanterna.</summary>
    public bool EstaIluminando(Vector3 pontoDoAlvo, Transform raizDoAlvo)
    {
        AlinharFeixeComPersonagem();
        if (luzDaLanterna == null || !luzDaLanterna.enabled)
            return false;

        Vector3 origem = luzDaLanterna.transform.position;
        Vector3 ateAlvo = pontoDoAlvo - origem;
        float distancia = ateAlvo.magnitude;
        if (distancia > luzDaLanterna.range || distancia <= 0.01f)
            return false;

        if (Vector3.Angle(luzDaLanterna.transform.forward, ateAlvo) > luzDaLanterna.spotAngle * 0.5f)
            return false;

        int layerMask = ~LayerMask.GetMask("Fumaca", "PonteFantasma");

        RaycastHit[] impactos = Physics.RaycastAll(
            origem,
            ateAlvo / distancia,
            distancia,
            layerMask,
            QueryTriggerInteraction.Collide);
        System.Array.Sort(impactos, (a, b) => a.distance.CompareTo(b.distance));

        bool alvoFantasma = raizDoAlvo && raizDoAlvo.GetComponentInParent<FantasmaCaverna>();
        foreach (RaycastHit impacto in impactos)
        {
            if (impacto.transform.IsChildOf(transform) || (personagem && impacto.transform.IsChildOf(personagem)))
                continue;

            if (impacto.transform == raizDoAlvo || impacto.transform.IsChildOf(raizDoAlvo)) return true;
            // Fantasmas agrupados nao fazem sombra uns nos outros no foco da lanterna.
            if (alvoFantasma && impacto.collider.GetComponentInParent<FantasmaCaverna>()) continue;
            // So os fantasmas permitem que o feixe atravesse as irregularidades do piso.
            if (alvoFantasma && PodeAtravessarPiso(impacto.collider, origem, pontoDoAlvo, impacto.normal)) continue;
            return false;
        }

        // Sem collider no caminho: o alvo esta no foco da luz.
        return true;
    }

    private static bool PodeAtravessarPiso(Collider colisor, Vector3 origem, Vector3 alvo, Vector3 normal)
    {
        // Paredes, portas e pecas do puzzle continuam sendo obstaculos.
        if (colisor.GetComponentInParent<PedraBrilhante>()) return false;
        bool piso = false, rocha = false;
        var filtro = colisor.GetComponent<MeshFilter>();
        string malha = filtro && filtro.sharedMesh ? filtro.sharedMesh.name : "";
        for (var t = colisor.transform; t; t = t.parent)
        {
            string nome = (t.name + " " + (t == colisor.transform ? malha : "")).ToLowerInvariant();
            if (nome.Contains("wall") || nome.Contains("parede") || nome.Contains("door")
                || nome.Contains("porta") || nome.Contains("ceiling") || nome.Contains("teto")) return false;
            piso |= nome.Contains("ground") || nome.Contains("floor") || nome.Contains("piso") || nome.Contains("chao");
            rocha |= nome.Contains("rock") || nome.Contains("stone") || nome.Contains("rocha")
                || nome.Contains("pedra") || nome.Contains("stalagmite") || nome.Contains("mountain");
        }
        if (piso) return true;
        // A face caminhavel do terreno tambem conta como piso, mesmo em uma malha grande.
        if (normal.y >= .45f && colisor.bounds.min.y <= Mathf.Max(origem.y, alvo.y)) return true;
        // Inclui as laterais dos colisores convexos das rochas baixas, mesmo com normal vertical.
        return rocha && colisor.bounds.max.y <= Mathf.Max(origem.y, alvo.y) + .35f;
    }

    private void CriarFeixeVisual()
    {
        if (!mostrarFeixe || feixeVisual != null)
            return;

        feixeVisual = new GameObject("Feixe Visivel da Lanterna");
        feixeVisual.transform.SetParent(luzDaLanterna.transform, false);

        MeshFilter filtro = feixeVisual.AddComponent<MeshFilter>();
        filtro.sharedMesh = CriarMalhaDeCone();

        MeshRenderer renderizador = feixeVisual.AddComponent<MeshRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            Debug.LogWarning("Nao encontrei o shader URP Unlit para o feixe da lanterna.");
            feixeVisual.SetActive(false);
            return;
        }

        Material material = new Material(shader)
        {
            name = "Material do Feixe da Lanterna",
            renderQueue = 3000
        };
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", 0f);
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetColor("_BaseColor", new Color(
            corDoFeixe.r, corDoFeixe.g, corDoFeixe.b, transparenciaDoFeixe));
        renderizador.sharedMaterial = material;
    }

    private Mesh CriarMalhaDeCone()
    {
        const int lados = 20;
        Mesh malha = new Mesh { name = "Cone do Feixe da Lanterna" };
        Vector3[] vertices = new Vector3[lados + 2];
        int[] triangulos = new int[lados * 3];

        vertices[0] = Vector3.zero;
        for (int i = 0; i < lados; i++)
        {
            float anguloAtual = i * Mathf.PI * 2f / lados;
            vertices[i + 1] = new Vector3(
                Mathf.Cos(anguloAtual) * larguraFinalDoFeixe,
                Mathf.Sin(anguloAtual) * larguraFinalDoFeixe,
                comprimentoDoFeixe);
        }

        vertices[lados + 1] = new Vector3(0f, 0f, comprimentoDoFeixe);
        for (int i = 0; i < lados; i++)
        {
            int proximo = (i + 1) % lados;
            int indice = i * 3;
            triangulos[indice] = 0;
            triangulos[indice + 1] = i + 1;
            triangulos[indice + 2] = proximo + 1;
        }

        malha.vertices = vertices;
        malha.triangles = triangulos;
        malha.RecalculateBounds();
        return malha;
    }

    public void ColetarLanterna()
    {
        foiColetada = true;
        AlinharFeixeComPersonagem();
        Instancia = this;
        GetComponentInParent<InventarioEndy>()?.RegistrarLanterna(this);
    }
}
