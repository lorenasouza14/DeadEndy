using UnityEngine;

// Pose aditiva no esqueleto: o corpo físico continua parado durante a distração.
[DefaultExecutionOrder(130)]
public class ComerPirulitoCaranguejo : MonoBehaviour
{
    [Tooltip("Posição da boca a partir da mandíbula, nos eixos do chefe.")]
    public Vector3 bocaOffset = new Vector3(0, 0.12f, 0.35f);
    [Min(0.1f)] public float mordidasPorSegundo = 3.5f;
    private CaranguejoChefe chefe;
    private Transform[] ossos;
    private Quaternion[] rotacoes;
    private Transform visual;
    private Vector3 escalaVisual, centroDoce;
    private float inicio, duracao;
    private bool aplicada;
    public bool Ativa { get; private set; }

    public void Iniciar(CaranguejoChefe dono, EfeitoPirulito item, float tempo)
    {
        Encerrar();
        chefe = dono;
        string[] nomes = { "RigHead", "RigJaw", "RigRArm1", "RigRArm2", "RigRArmPalm" };
        ossos = new Transform[nomes.Length];
        rotacoes = new Quaternion[nomes.Length];
        foreach (var filho in GetComponentsInChildren<Transform>(true))
            for (int i = 0; i < nomes.Length; i++) if (filho.name == nomes[i]) ossos[i] = filho;
        inicio = Time.time;
        duracao = Mathf.Max(0.1f, tempo);
        if (item != null) CriarVisual(item);
        Ativa = true;
    }

    void CriarVisual(EfeitoPirulito item)
    {
        // Copia somente a malha: não duplica física, coleta nem a destruição do projétil.
        visual = new GameObject("Pirulito sendo devorado").transform;
        visual.SetParent(transform, false);
        Bounds limites = new Bounds();
        bool temLimites = false;
        foreach (var filtro in item.GetComponentsInChildren<MeshFilter>())
        {
            var original = filtro.GetComponent<MeshRenderer>();
            if (original == null || !original.enabled || filtro.sharedMesh == null) continue;
            var parte = new GameObject(filtro.name);
            parte.layer = gameObject.layer;
            parte.transform.SetParent(visual, false);
            parte.transform.localPosition = item.transform.InverseTransformPoint(filtro.transform.position);
            parte.transform.localRotation = Quaternion.Inverse(item.transform.rotation) * filtro.transform.rotation;
            var s = item.transform.lossyScale;
            var f = filtro.transform.lossyScale;
            parte.transform.localScale = new Vector3(f.x / s.x, f.y / s.y, f.z / s.z);
            parte.AddComponent<MeshFilter>().sharedMesh = filtro.sharedMesh;
            parte.AddComponent<MeshRenderer>().sharedMaterials = original.sharedMaterials;
            Bounds b = filtro.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 canto = b.center + Vector3.Scale(b.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = visual.InverseTransformPoint(parte.transform.TransformPoint(canto));
                if (!temLimites) { limites = new Bounds(p, Vector3.zero); temLimites = true; }
                else limites.Encapsulate(p);
            }
        }
        // O doce fica na parte superior do palito; tamanho legível junto ao chefe.
        centroDoce = limites.center + Vector3.up * limites.size.y * 0.25f;
        escalaVisual = Vector3.one * (0.9f / Mathf.Max(0.01f, limites.size.y));
        visual.localScale = escalaVisual;
        visual.gameObject.SetActive(false); // Primeira pose posiciona na garra antes de exibir.
    }

    void Update() { Restaurar(); }

    void LateUpdate()
    {
        if (!Ativa) return;
        if (chefe == null || !chefe.isActiveAndEnabled || !chefe.EstaCalmo) { Encerrar(); return; }
        float tempo = Time.time - inicio;
        float entrada = Mathf.SmoothStep(0, 1, tempo / 0.45f);
        float saida = Mathf.Clamp01((duracao - tempo) / 0.25f);
        float peso = entrada * saida;
        float mordida = 0.5f + 0.5f * Mathf.Sin(tempo * Mathf.PI * 2 * mordidasPorSegundo);
        for (int i = 0; i < ossos.Length; i++)
            if (ossos[i] != null) rotacoes[i] = ossos[i].localRotation;
        aplicada = true;
        Girar(ossos[0], transform.right, (-5 + mordida * 7) * peso);
        Girar(ossos[1], transform.right, (8 + mordida * 22) * peso);
        Vector3 boca = (ossos[1] != null ? ossos[1].position : transform.TransformPoint(new Vector3(0, 2, 1)))
            + transform.TransformVector(bocaOffset);
        Vector3 doce = boca + transform.forward * ((0.1f + mordida * 0.1f) * transform.lossyScale.y);
        Vector3 pega = doce - transform.up * (0.4f * transform.lossyScale.y);
        // Aproxima a garra da boca mantendo o comprimento dos segmentos.
        for (int passo = 0; passo < 4; passo++)
            for (int i = 3; i >= 2; i--)
            {
                if (ossos[i] == null || ossos[4] == null) continue;
                Vector3 de = ossos[4].position - ossos[i].position;
                Vector3 para = pega - ossos[i].position;
                if (de.sqrMagnitude < 0.000001f || para.sqrMagnitude < 0.000001f) continue;
                ossos[i].rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(de, para), peso) * ossos[i].rotation;
            }
        if (visual != null)
        {
            visual.gameObject.SetActive(true);
            float consumido = Mathf.SmoothStep(0, 1, Mathf.Clamp01((tempo / duracao - 0.55f) / 0.45f));
            visual.localScale = escalaVisual * Mathf.Lerp(1, 0.03f, consumido);
            Quaternion rotacao = Quaternion.LookRotation(transform.forward, transform.up);
            Vector3 inicioGarra = ossos[4] != null ? ossos[4].position + transform.up * (0.4f * transform.lossyScale.y) : doce;
            visual.rotation = rotacao;
            visual.position = Vector3.Lerp(inicioGarra, doce, entrada) - rotacao * Vector3.Scale(centroDoce, visual.lossyScale);
        }
    }

    static void Girar(Transform osso, Vector3 eixo, float angulo)
    {
        if (osso != null) osso.rotation = Quaternion.AngleAxis(angulo, eixo) * osso.rotation;
    }

    void Restaurar()
    {
        if (!aplicada) return;
        for (int i = 0; i < ossos.Length; i++) if (ossos[i] != null) ossos[i].localRotation = rotacoes[i];
        aplicada = false;
    }

    public void Encerrar()
    {
        Restaurar();
        Ativa = false;
        if (visual != null) { visual.gameObject.SetActive(false); Destroy(visual.gameObject); }
        visual = null;
    }

    void OnDisable() { Encerrar(); }
}
