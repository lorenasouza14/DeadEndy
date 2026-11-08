using UnityEngine;

[DisallowMultipleComponent]
public class BananaSopa : MonoBehaviour
{
    [Header("Desafio de origem (preencha somente o correspondente)")]
    public GorilaGuardiao gorilaQueProtege;
    public MonolitoDesmoronavel monolito;
    [Min(0.1f)] public float alcanceColeta = 2f;
    [Header("Aparencia na mao")]
    [Min(0.05f)] public float tamanhoNaMao = 0.35f;
    public Vector3 posicaoNaMao;
    public Vector3 rotacaoNaMao;
    public bool NaMao { get; private set; }
    public bool Depositada { get; private set; }
    private Transform paiOriginal;
    private Vector3 posicaoOriginal, escalaOriginal;
    private Quaternion rotacaoOriginal;
    private Collider[] colisoes;
    private bool[] colisoesAtivas;
    private Rigidbody corpo;
    private bool eraCinematico;

    public Vector3 Centro
    {
        get
        {
            var visual = GetComponentInChildren<MeshRenderer>();
            return visual != null ? visual.bounds.center : transform.position;
        }
    }

    public bool DesafioLiberado => (gorilaQueProtege == null || gorilaQueProtege.EstaDormindo) &&
        (monolito == null || (monolito.Concluido && monolito.banana != null &&
            monolito.banana.linearVelocity.sqrMagnitude < 0.09f));

    public bool PodeColetar(InventarioEndy jogador)
    {
        if (!isActiveAndEnabled || NaMao || Depositada || jogador == null ||
            !jogador.PodeInteragirComSopa || !jogador.PodeColetarPocao || !DesafioLiberado) return false;
        return InteracaoSopa.AoAlcance(jogador, Centro, alcanceColeta, transform);
    }

    public string ObterDica(InventarioEndy jogador)
    {
        if (jogador == null || !jogador.mostrarControles || !PodeColetar(jogador)) return null;
        return "[E] Pegar banana";
    }

    internal void ColocarNaMao(Transform mao)
    {
        paiOriginal = transform.parent;
        posicaoOriginal = transform.localPosition;
        rotacaoOriginal = transform.localRotation;
        escalaOriginal = transform.localScale;
        colisoes = GetComponentsInChildren<Collider>(true);
        colisoesAtivas = new bool[colisoes.Length];
        for (int i = 0; i < colisoes.Length; i++) { colisoesAtivas[i] = colisoes[i].enabled; colisoes[i].enabled = false; }
        corpo = GetComponent<Rigidbody>();
        if (corpo != null)
        {
            eraCinematico = corpo.isKinematic;
            if (!corpo.isKinematic) { corpo.linearVelocity = Vector3.zero; corpo.angularVelocity = Vector3.zero; }
            corpo.isKinematic = true;
        }
        var visual = GetComponentInChildren<MeshRenderer>();
        Vector3 tamanho = visual != null ? visual.bounds.size : Vector3.one;
        float fator = tamanhoNaMao / Mathf.Max(0.001f, Mathf.Max(tamanho.x, tamanho.y, tamanho.z));
        Vector3 escalaMundo = transform.lossyScale * fator;
        NaMao = true;
        transform.SetParent(mao, false);
        transform.localPosition = posicaoNaMao;
        transform.localRotation = Quaternion.Euler(rotacaoNaMao);
        Vector3 escalaPai = mao.lossyScale;
        transform.localScale = new Vector3(escalaMundo.x / Mathf.Max(0.001f, Mathf.Abs(escalaPai.x)),
            escalaMundo.y / Mathf.Max(0.001f, Mathf.Abs(escalaPai.y)), escalaMundo.z / Mathf.Max(0.001f, Mathf.Abs(escalaPai.z)));
    }

    public void DevolverAoLocalDaColeta()
    {
        if (!NaMao || Depositada) return;
        NaMao = false;
        RestaurarTransform();
        for (int i = 0; i < colisoes.Length; i++) if (colisoes[i] != null) colisoes[i].enabled = colisoesAtivas[i];
        if (corpo != null) corpo.isKinematic = eraCinematico;
    }

    internal void Consumir()
    {
        Depositada = true;
        NaMao = false;
        RestaurarTransform();
        gameObject.SetActive(false);
    }

    private void RestaurarTransform()
    {
        transform.SetParent(paiOriginal, false);
        transform.localPosition = posicaoOriginal;
        transform.localRotation = rotacaoOriginal;
        transform.localScale = escalaOriginal;
    }
}
