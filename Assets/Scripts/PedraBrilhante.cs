using UnityEngine;

/// <summary>
/// Faz esta pedra acender quando o personagem encosta nela na ordem correta.
/// O material e instanciado em tempo de jogo, por isso outras pedras que usam
/// o mesmo material nao recebem o brilho.
/// </summary>
public class PedraBrilhante : MonoBehaviour
{
    public enum CorDaSequencia
    {
        Vermelho,
        Laranja,
        Amarelo,
        Verde,
        Azul,
        Violeta
    }

    [Header("Brilho quando o jogador encostar")]
    [ColorUsage(true, true)] public Color corDoBrilho = new Color(0.15f, 1f, 0.8f, 1f);
    [Min(0f)] public float intensidadeDoBrilho = 6f;

    [Header("Puzzle da sequencia")]
    [Tooltip("Normalmente fica desligado: o puzzle le a cor usada no brilho da pedra.")]
    [SerializeField] private bool definirCorDaSequenciaManualmente;
    [Tooltip("Use somente se quiser que a ordem seja diferente da cor do brilho visual.")]
    public CorDaSequencia corDaPedra = CorDaSequencia.Vermelho;
    [Tooltip("Coloque este som em uma das pedras. Ele toca ao errar qualquer pedra.")]
    [SerializeField] private AudioClip somDaSequenciaIncorreta;

    [Header("Luz espalhada no ambiente")]
    [SerializeField] private bool criarLuzNoAmbiente = true;
    [SerializeField, Min(0f)] private float intensidadeDaLuz = 4f;
    [SerializeField, Min(0.1f)] private float alcanceDaLuz = 5f;
    [SerializeField] private Light luzDaPedra;

    private Renderer[] renderizadores;
    private Material[][] materiais;
    private bool acesa;
    private Bounds limitesDaPedra;

    private static readonly CorDaSequencia[] OrdemCorreta =
    {
        CorDaSequencia.Vermelho,
        CorDaSequencia.Laranja,
        CorDaSequencia.Amarelo,
        CorDaSequencia.Verde,
        CorDaSequencia.Azul,
        CorDaSequencia.Violeta
    };

    private static readonly System.Collections.Generic.List<PedraBrilhante> PedrasDoPuzzle =
        new System.Collections.Generic.List<PedraBrilhante>();
    private static int proximaCor;

    /// <summary>Quantidade de pedras acertadas na tentativa atual.</summary>
    public static int ProgressoAtual => proximaCor;

    private void Awake()
    {
        if (!PedrasDoPuzzle.Contains(this))
            PedrasDoPuzzle.Add(this);

        PrepararRenderizadores();
        CriarAreaDeToque();

        PrepararLuz();
    }

    private void OnDestroy()
    {
        PedrasDoPuzzle.Remove(this);
    }

    private void PrepararRenderizadores()
    {
        renderizadores = GetComponentsInChildren<Renderer>(true);

        if (renderizadores.Length == 0)
        {
            Renderer renderizadorPai = GetComponentInParent<Renderer>();
            if (renderizadorPai != null)
                renderizadores = new[] { renderizadorPai };
        }

        materiais = new Material[renderizadores.Length][];
        for (int i = 0; i < renderizadores.Length; i++)
        {
            // .materials cria materiais locais para esta pedra, sem editar o asset compartilhado.
            materiais[i] = renderizadores[i].materials;
        }
    }

    private void CriarAreaDeToque()
    {
        if (renderizadores.Length == 0)
            return;

        limitesDaPedra = renderizadores[0].bounds;
        for (int i = 1; i < renderizadores.Length; i++)
            limitesDaPedra.Encapsulate(renderizadores[i].bounds);

        // O trigger e adicionado apenas nesta pedra e nao altera o collider fisico existente.
        SphereCollider areaDeToque = GetComponent<SphereCollider>();
        if (areaDeToque == null || !areaDeToque.isTrigger)
            areaDeToque = gameObject.AddComponent<SphereCollider>();

        areaDeToque.isTrigger = true;
        areaDeToque.center = transform.InverseTransformPoint(limitesDaPedra.center);

        float maiorEscala = Mathf.Max(
            Mathf.Abs(transform.lossyScale.x),
            Mathf.Abs(transform.lossyScale.y),
            Mathf.Abs(transform.lossyScale.z));
        areaDeToque.radius = limitesDaPedra.extents.magnitude / Mathf.Max(maiorEscala, 0.0001f) * 1.2f;

        Rigidbody corpo = GetComponent<Rigidbody>();
        if (corpo == null)
            corpo = gameObject.AddComponent<Rigidbody>();

        corpo.isKinematic = true;
        corpo.useGravity = false;
    }

    private void PrepararLuz()
    {
        if (luzDaPedra == null && criarLuzNoAmbiente && renderizadores.Length > 0)
        {
            GameObject objetoDaLuz = new GameObject("Luz da Pedra");
            objetoDaLuz.transform.SetParent(transform, true);
            objetoDaLuz.transform.position = limitesDaPedra.center;
            luzDaPedra = objetoDaLuz.AddComponent<Light>();
            luzDaPedra.type = LightType.Point;
            luzDaPedra.shadows = LightShadows.None;
        }

        if (luzDaPedra == null)
            return;

        luzDaPedra.color = corDoBrilho;
        luzDaPedra.intensity = intensidadeDaLuz;
        luzDaPedra.range = alcanceDaLuz;
        luzDaPedra.enabled = false;
    }

    private void OnTriggerEnter(Collider outro)
    {
        if (outro.CompareTag("Player") ||
            outro.GetComponentInParent<StarterAssets.ThirdPersonController>() != null)
        {
            SelecionarPedra();
        }
    }

    private void OnCollisionEnter(Collision colisao)
    {
        if (colisao.collider.CompareTag("Player") ||
            colisao.collider.GetComponentInParent<StarterAssets.ThirdPersonController>() != null)
        {
            SelecionarPedra();
        }
    }

    private void SelecionarPedra()
    {
        // Depois de concluir as seis cores, as pedras permanecem acesas.
        if (acesa || proximaCor >= OrdemCorreta.Length)
            return;

        if (ObterCorDaPedra() != OrdemCorreta[proximaCor])
        {
            TocarSomDeErro();
            ApagarTodasAsPedras();
            proximaCor = 0;
            Debug.Log("Sequencia incorreta. Tente novamente.");
            return;
        }

        Acender();
        proximaCor++;

        if (proximaCor == OrdemCorreta.Length)
        {
            Debug.Log("Sequencia das pedras concluida!");
            // Aqui depois podemos ligar a animacao da porta.
        }
    }

    private CorDaSequencia ObterCorDaPedra()
    {
        if (definirCorDaSequenciaManualmente)
            return corDaPedra;

        // Tags deixam a configuracao da fase bem mais clara no Inspector.
        // Se uma pedra ainda nao tiver tag, usamos sua cor visual como reserva.
        switch (gameObject.tag)
        {
            case "Vermelho": return CorDaSequencia.Vermelho;
            case "Laranja": return CorDaSequencia.Laranja;
            case "Amarelo": return CorDaSequencia.Amarelo;
            case "Verde": return CorDaSequencia.Verde;
            case "Azul": return CorDaSequencia.Azul;
            case "Violeta": return CorDaSequencia.Violeta;
        }

        Color.RGBToHSV(corDoBrilho, out float matiz, out _, out _);
        float graus = matiz * 360f;

        if (graus < 8f || graus >= 330f) return CorDaSequencia.Vermelho;
        if (graus < 45f) return CorDaSequencia.Laranja;
        if (graus < 75f) return CorDaSequencia.Amarelo;
        if (graus < 170f) return CorDaSequencia.Verde;
        if (graus < 260f) return CorDaSequencia.Azul;
        return CorDaSequencia.Violeta;
    }

    private void Acender()
    {
        acesa = true;

        foreach (Material[] listaDeMateriais in materiais)
        {
            foreach (Material material in listaDeMateriais)
            {
                if (material == null || !material.HasProperty("_EmissionColor"))
                    continue;

                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", corDoBrilho * intensidadeDoBrilho);
            }
        }

        if (luzDaPedra != null)
        {
            luzDaPedra.color = corDoBrilho;
            luzDaPedra.intensity = intensidadeDaLuz;
            luzDaPedra.range = alcanceDaLuz;
            luzDaPedra.enabled = true;
        }
    }

    private void Apagar()
    {
        acesa = false;

        foreach (Material[] listaDeMateriais in materiais)
        {
            foreach (Material material in listaDeMateriais)
            {
                if (material == null || !material.HasProperty("_EmissionColor"))
                    continue;

                material.SetColor("_EmissionColor", Color.black);
            }
        }

        if (luzDaPedra != null)
            luzDaPedra.enabled = false;
    }

    private static void ApagarTodasAsPedras()
    {
        foreach (PedraBrilhante pedra in PedrasDoPuzzle)
        {
            if (pedra != null)
                pedra.Apagar();
        }
    }

    private void TocarSomDeErro()
    {
        AudioClip som = somDaSequenciaIncorreta;
        if (som == null)
        {
            foreach (PedraBrilhante pedra in PedrasDoPuzzle)
            {
                if (pedra != null && pedra.somDaSequenciaIncorreta != null)
                {
                    som = pedra.somDaSequenciaIncorreta;
                    break;
                }
            }
        }

        if (som != null && Camera.main != null)
            AudioSource.PlayClipAtPoint(som, Camera.main.transform.position);
    }
}
