using UnityEngine;
using System.Collections;

public class CaranguejoChefe : MonoBehaviour
{
    public enum EstadoChefe { Escondido, Perseguindo, PreparandoGiro, Investindo, Atordoado, Derrotado }

    [Header("Configurações do Chefe")]
    public EstadoChefe estadoAtual = EstadoChefe.Escondido;
    public Transform alvoEndy;
    public Animator animChefe;

    [Header("Atributos de Combate")]
    public float velPerseguicao = 6f;
    public float velInvestida = 25f;
    public float tempoEntreAtaques = 3.5f;

    [Header("Magia da Areia")]
    public GameObject prefabAreia;
    public float tempoEntrePocas = 0.25f;
    private float timerAreia;

    public int NumeroAtordoamento { get; private set; }

    private Vector3 direcaoInvestida;
    private Rigidbody rb;
    private float timerAtaque;
    private bool ataqueTeleguiado = false;
    private bool ignorarPedrasNoArranque = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null) rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    void FixedUpdate()
    {
        if (estadoAtual == EstadoChefe.Derrotado || estadoAtual == EstadoChefe.Escondido ||
            estadoAtual == EstadoChefe.Atordoado || estadoAtual == EstadoChefe.PreparandoGiro)
        {
            if (rb != null) rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            return;
        }

        if (estadoAtual == EstadoChefe.Investindo)
        {
            // Se você tentou dar um 2º toque seguido no baú, ele corrige a curva para te acertar em cheio
            if (ataqueTeleguiado && alvoEndy != null)
            {
                Vector3 posAlvo = new Vector3(alvoEndy.position.x, transform.position.y, alvoEndy.position.z);
                transform.LookAt(posAlvo);
                direcaoInvestida = (posAlvo - transform.position).normalized;
            }

            rb.linearVelocity = new Vector3(direcaoInvestida.x * velInvestida, rb.linearVelocity.y, direcaoInvestida.z * velInvestida);

            if (prefabAreia != null)
            {
                timerAreia += Time.fixedDeltaTime;
                if (timerAreia >= tempoEntrePocas)
                {
                    Instantiate(prefabAreia, transform.position, prefabAreia.transform.rotation);
                    timerAreia = 0f;
                }
            }
        }
        else if (estadoAtual == EstadoChefe.Perseguindo)
        {
            Vector3 posAlvo = new Vector3(alvoEndy.position.x, transform.position.y, alvoEndy.position.z);
            transform.LookAt(posAlvo);
            Vector3 dir = (posAlvo - transform.position).normalized;
            rb.linearVelocity = new Vector3(dir.x * velPerseguicao, rb.linearVelocity.y, dir.z * velPerseguicao);

            timerAtaque += Time.fixedDeltaTime;
            if (timerAtaque >= tempoEntreAtaques)
            {
                StartCoroutine(RotinaPrepararGiro());
            }
        }
    }

    public void AcordarChefe()
    {
        if (estadoAtual == EstadoChefe.Escondido)
        {
            estadoAtual = EstadoChefe.Perseguindo;
            if (animChefe != null) animChefe.SetTrigger("Perseguir");
        }
    }

    // --- CORTA O ATORDOAMENTO NA HORA E VEM DIRETO ATACAR ---
    public void InvestidaPunitiva(bool teleguiado)
    {
        if (estadoAtual == EstadoChefe.Derrotado) return;

        StopAllCoroutines(); // Cancela os segundos restantes do atordoamento imediatamente!
        StartCoroutine(RotinaInvestidaImediata(teleguiado));
    }

    IEnumerator RotinaInvestidaImediata(bool teleguiado)
    {
        ataqueTeleguiado = teleguiado;
        ignorarPedrasNoArranque = true; // Evita que ele se atordoe na mesma pedra de onde está saindo
        timerAtaque = 0f;
        timerAreia = tempoEntrePocas;

        if (alvoEndy != null)
        {
            Vector3 posAlvo = new Vector3(alvoEndy.position.x, transform.position.y, alvoEndy.position.z);
            transform.LookAt(posAlvo);
            direcaoInvestida = (posAlvo - transform.position).normalized;
        }

        if (animChefe != null)
        {
            animChefe.ResetTrigger("Perseguir");
            animChefe.ResetTrigger("Atordoar");
            animChefe.SetTrigger("Girar");
        }

        estadoAtual = EstadoChefe.Investindo;

        // Dispara em alta velocidade por 1.5s
        yield return new WaitForSeconds(1.5f);

        ignorarPedrasNoArranque = false;
        ataqueTeleguiado = false;

        if (estadoAtual == EstadoChefe.Investindo)
        {
            estadoAtual = EstadoChefe.Perseguindo;
            if (animChefe != null) animChefe.SetTrigger("Perseguir");
        }
    }

    IEnumerator RotinaPrepararGiro()
    {
        estadoAtual = EstadoChefe.PreparandoGiro;
        ataqueTeleguiado = false;
        ignorarPedrasNoArranque = false;
        timerAtaque = 0;

        if (animChefe != null)
        {
            animChefe.ResetTrigger("Perseguir");
            animChefe.SetTrigger("Girar");
        }

        float tempoMira = 1.5f;
        while (tempoMira > 0)
        {
            Vector3 posAlvo = new Vector3(alvoEndy.position.x, transform.position.y, alvoEndy.position.z);
            transform.LookAt(posAlvo);
            tempoMira -= Time.deltaTime;
            yield return null;
        }

        direcaoInvestida = (alvoEndy.position - transform.position).normalized;
        direcaoInvestida.y = 0;

        timerAreia = tempoEntrePocas;
        estadoAtual = EstadoChefe.Investindo;

        yield return new WaitForSeconds(1.5f);

        if (estadoAtual == EstadoChefe.Investindo)
        {
            estadoAtual = EstadoChefe.Perseguindo;
            if (animChefe != null) animChefe.SetTrigger("Perseguir");
        }
    }

    void OnCollisionEnter(Collision collision) { ProcessarColisao(collision.gameObject); }
    void OnCollisionStay(Collision collision) { ProcessarColisao(collision.gameObject); }

    void ProcessarColisao(GameObject obj)
    {
        if (estadoAtual == EstadoChefe.Investindo)
        {
            if (obj.CompareTag("Pedra") && !ignorarPedrasNoArranque)
            {
                StartCoroutine(RotinaAtordoado());
            }
            else if (obj.CompareTag("Player"))
            {
                AplicarDano(obj);
            }
        }
    }

    IEnumerator RotinaAtordoado()
    {
        estadoAtual = EstadoChefe.Atordoado;
        ataqueTeleguiado = false;
        ignorarPedrasNoArranque = false;
        NumeroAtordoamento++;

        if (animChefe != null) animChefe.SetTrigger("Atordoar");

        Debug.Log("CRASH! Chefe bateu na pedra!");
        yield return new WaitForSeconds(4f);

        if (estadoAtual != EstadoChefe.Derrotado)
        {
            estadoAtual = EstadoChefe.Perseguindo;
            if (animChefe != null) animChefe.SetTrigger("Perseguir");
        }
    }

    void AplicarDano(GameObject player)
    {
        ataqueTeleguiado = false;
        ignorarPedrasNoArranque = false;

        VidaEndy vida = player.GetComponent<VidaEndy>();
        if (vida != null) vida.TomarDano();

        estadoAtual = EstadoChefe.Perseguindo;
        if (animChefe != null) animChefe.SetTrigger("Perseguir");
    }

    public void TomarPocaoEncolhedora()
    {
        if (estadoAtual == EstadoChefe.Derrotado) return;

        estadoAtual = EstadoChefe.Derrotado;
        ataqueTeleguiado = false;
        ignorarPedrasNoArranque = false;
        StopAllCoroutines();

        if (animChefe != null) animChefe.SetTrigger("Atordoar");

        StartCoroutine(RotinaEncolher());
    }

    IEnumerator RotinaEncolher()
    {
        float tempo = 0;
        Vector3 tamanhoInicial = transform.localScale;
        Vector3 tamanhoFinal = tamanhoInicial * 0.15f;

        while (tempo < 2f)
        {
            transform.localScale = Vector3.Lerp(tamanhoInicial, tamanhoFinal, tempo / 2f);
            tempo += Time.deltaTime;
            yield return null;
        }

        transform.localScale = tamanhoFinal;
        Debug.Log("Poção funcionou! O Caranguejo Rei encolheu e parou de atacar!");
    }
}