using UnityEngine;
using System.Collections;

public class InteracaoBau : MonoBehaviour
{
    [Header("Configurações do Combate")]
    public CaranguejoChefe chefe;
    [Tooltip("Quantas vezes precisa tocar no baú (com o chefe tonto) para abrir")]
    public int toquesParaAbrir = 3;

    [Header("Animação do Baú")]
    public Animator animBau;
    [Tooltip("Tempo em segundos até a tampa chegar no topo antes de congelar aberta")]
    public float tempoAteTampaAbrir = 0.5f;

    private int toquesConseguidos = 0;
    private int ultimoAtordoamentoUsado = -1;
    private bool combateIniciado = false;
    private bool bauAberto = false;
    private float tempoUltimoToque = 0f;

    // Guarda a posição exata no chão para o baú nunca flutuar
    private Vector3 posicaoFixaPai;
    private Vector3 posicaoFixaAnim;

    void Awake()
    {
        if (animBau == null) animBau = GetComponentInChildren<Animator>();

        // Salva a altura original na pedra ANTES de o Animator rodar o primeiro frame
        posicaoFixaPai = transform.position;
        if (animBau != null)
        {
            posicaoFixaAnim = animBau.transform.position;
            animBau.applyRootMotion = false;
            animBau.speed = 0f;
        }
    }

    void LateUpdate()
    {
        // O LateUpdate roda logo DEPOIS da animação e força o baú a ficar colado no chão,
        // deixando apenas a tampa girar!
        transform.position = posicaoFixaPai;
        if (animBau != null)
        {
            animBau.transform.position = posicaoFixaAnim;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || bauAberto) return;

        if (Time.time - tempoUltimoToque < 0.8f) return;
        tempoUltimoToque = Time.time;

        if (!combateIniciado)
        {
            combateIniciado = true;
            StartCoroutine(TremerBauRapido());

            if (chefe != null)
            {
                chefe.gameObject.SetActive(true);
                chefe.AcordarChefe();
            }
            Debug.Log("Você tocou no baú e acordou o Chefe!");
            return;
        }

        if (chefe == null) return;

        if (chefe.estadoAtual == CaranguejoChefe.EstadoChefe.Atordoado &&
            chefe.NumeroAtordoamento != ultimoAtordoamentoUsado)
        {
            ultimoAtordoamentoUsado = chefe.NumeroAtordoamento;
            toquesConseguidos++;

            if (toquesConseguidos >= toquesParaAbrir)
            {
                bauAberto = true;
                StartCoroutine(AbrirETravarTampa());

                InventarioEndy inventario = other.GetComponentInParent<InventarioEndy>();
                if (inventario != null)
                {
                    inventario.ReceberItensDoBau();
                }

                chefe.InvestidaPunitiva(false);
            }
            else
            {
                StartCoroutine(TremerBauRapido());
                Debug.Log("Destravou parte do baú (" + toquesConseguidos + "/" + toquesParaAbrir + ")! CORRA, ele acordou furioso!");
                chefe.InvestidaPunitiva(false);
            }
        }
        else
        {
            Debug.Log("Tentou forçar o baú! O Chefe veio direto te dar dano!");
            chefe.InvestidaPunitiva(true);
        }
    }

    IEnumerator TremerBauRapido()
    {
        if (animBau != null)
        {
            animBau.speed = 1f;
            animBau.Play("Chest_Shake", 0, 0f);
            yield return new WaitForSeconds(0.6f);
            if (!bauAberto) animBau.speed = 0f;
        }
    }

    IEnumerator AbrirETravarTampa()
    {
        if (animBau != null)
        {
            animBau.speed = 1f;
            animBau.Play("Chest_Open_Close", 0, 0f);
            yield return new WaitForSeconds(tempoAteTampaAbrir);
            animBau.speed = 0f;
        }
    }
}