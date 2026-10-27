using UnityEngine;

// Pose sobre a animação de locomoção, sem substituir o Animator Controller.
[DefaultExecutionOrder(100)]
public class AnimacaoTrompete : MonoBehaviour
{
    [Tooltip("Deslocamento da boca a partir do osso da cabeça, nos eixos do personagem.")]
    public Vector3 bocaOffset = new Vector3(0, 0.44f, 0.24f);
    [Range(0, 1)] public float peso;
    private EfeitoTrompete trompete;
    private Transform cabeca;
    private Transform[] bracoDireito;
    private Transform[] bracoEsquerdo;
    private Quaternion[] rotacoesDireitas = new Quaternion[3];
    private Quaternion[] rotacoesEsquerdas = new Quaternion[3];
    private bool poseAplicada;
    private Vector3 posicaoLocal;
    private Quaternion rotacaoLocal;
    public Vector3 PosicaoBoca => cabeca != null ? cabeca.position + transform.TransformVector(bocaOffset) :
        transform.TransformPoint(new Vector3(0, 1.4f, 0.2f));
    public bool TemBracos => bracoDireito != null && bracoEsquerdo != null;

    public void Preparar(Animator animator, EfeitoTrompete item)
    {
        Encerrar();
        trompete = item;
        cabeca = Osso(animator, HumanBodyBones.Head, "head");
        bracoDireito = Braco(animator, true);
        bracoEsquerdo = Braco(animator, false);
        posicaoLocal = item.transform.localPosition;
        rotacaoLocal = item.transform.localRotation;
    }

    Transform Osso(Animator animator, HumanBodyBones humanoid, string nome)
    {
        if (animator != null && animator.isHuman)
        {
            Transform osso = animator.GetBoneTransform(humanoid);
            if (osso != null) return osso;
        }
        foreach (Transform filho in GetComponentsInChildren<Transform>(true))
            if (filho.name == nome) return filho;
        return null;
    }

    Transform[] Braco(Animator animator, bool direita)
    {
        var cima = Osso(animator, direita ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm, direita ? "upperarm.r" : "upperarm.l");
        var baixo = Osso(animator, direita ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm, direita ? "lowerarm.r" : "lowerarm.l");
        var mao = Osso(animator, direita ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand, direita ? "wrist.r" : "wrist.l");
        return cima != null && baixo != null && mao != null ? new[] { cima, baixo, mao } : null;
    }

    void Update() { RestaurarBracos(); }

    void LateUpdate()
    {
        if (trompete == null || peso <= 0) return;
        Salvar(bracoDireito, rotacoesDireitas);
        Salvar(bracoEsquerdo, rotacoesEsquerdas);
        poseAplicada = true;
        Transform item = trompete.transform;
        // Calcula a posição de repouso antes de mover os braços.
        Vector3 repouso = item.parent.TransformPoint(posicaoLocal);
        Quaternion rotRepouso = item.parent.rotation * rotacaoLocal;
        Quaternion rotBoca = Quaternion.LookRotation(-transform.forward, transform.up);
        Vector3 posBoca = PosicaoBoca - rotBoca * Vector3.Scale(trompete.bocalLocal, item.lossyScale);
        Vector3 posFinal = Vector3.Lerp(repouso, posBoca, peso);
        Quaternion rotFinal = Quaternion.Slerp(rotRepouso, rotBoca, peso);
        float escala = Mathf.Abs(transform.lossyScale.y);
        Vector3 direita = posBoca + transform.right * (0.08f * escala) - transform.up * (0.13f * escala);
        Vector3 esquerda = posBoca - transform.right * (0.08f * escala) - transform.up * (0.13f * escala);
        MoverBraco(bracoDireito, direita, transform.right, peso);
        MoverBraco(bracoEsquerdo, esquerda, -transform.right, peso);
        item.SetPositionAndRotation(posFinal, rotFinal);
    }

    void MoverBraco(Transform[] ossos, Vector3 alvo, Vector3 lado, float mistura)
    {
        if (ossos == null) return;
        Transform cima = ossos[0], baixo = ossos[1], mao = ossos[2];
        alvo = Vector3.Lerp(mao.position, alvo, mistura);
        float a = Vector3.Distance(cima.position, baixo.position);
        float b = Vector3.Distance(baixo.position, mao.position);
        Vector3 direcao = alvo - cima.position;
        if (a < 0.0001f || b < 0.0001f || direcao.sqrMagnitude < 0.000001f) return;
        float distancia = Mathf.Clamp(direcao.magnitude, Mathf.Abs(a - b) + 0.0001f, a + b - 0.0001f);
        direcao.Normalize();
        Vector3 dobra = Vector3.ProjectOnPlane(lado - transform.up, direcao).normalized;
        if (dobra.sqrMagnitude < 0.001f) dobra = Vector3.Cross(direcao, transform.forward).normalized;
        float x = (a * a - b * b + distancia * distancia) / (2 * distancia);
        Vector3 cotovelo = cima.position + direcao * x + dobra * Mathf.Sqrt(Mathf.Max(0, a * a - x * x));
        cima.rotation = Quaternion.FromToRotation(baixo.position - cima.position, cotovelo - cima.position) * cima.rotation;
        baixo.rotation = Quaternion.FromToRotation(mao.position - baixo.position,
            cima.position + direcao * distancia - baixo.position) * baixo.rotation;
        mao.rotation = Quaternion.Slerp(mao.rotation, Quaternion.LookRotation(transform.forward, transform.up), mistura);
    }

    static void Salvar(Transform[] ossos, Quaternion[] destino)
    {
        if (ossos == null) return;
        for (int i = 0; i < ossos.Length; i++) destino[i] = ossos[i].localRotation;
    }

    static void Restaurar(Transform[] ossos, Quaternion[] origem)
    {
        if (ossos == null) return;
        for (int i = 0; i < ossos.Length; i++) if (ossos[i] != null) ossos[i].localRotation = origem[i];
    }

    void RestaurarBracos()
    {
        if (!poseAplicada) return;
        Restaurar(bracoDireito, rotacoesDireitas);
        Restaurar(bracoEsquerdo, rotacoesEsquerdas);
        poseAplicada = false;
        if (trompete != null)
        {
            trompete.transform.localPosition = posicaoLocal;
            trompete.transform.localRotation = rotacaoLocal;
        }
    }

    public void Encerrar()
    {
        RestaurarBracos();
        peso = 0;
        trompete = null;
    }

    void OnDisable() { Encerrar(); }
}
