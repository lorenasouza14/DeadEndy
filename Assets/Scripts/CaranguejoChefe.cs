using UnityEngine;
using System.Collections;

public class CaranguejoChefe : MonoBehaviour
{
    // Novo estado "Atacando" adicionado
    public enum EstadoChefe { Escondido, Perseguindo, PreparandoGiro, Investindo, Atordoado, Derrotado, Atacando }

    [Header("Configurações do Chefe")]
    public EstadoChefe estadoAtual = EstadoChefe.Escondido;
    public Transform alvoEndy;
    public Animator animChefe;

    [Header("Atributos de Combate")]
    public float velPerseguicao = 6f;
    public float velInvestida = 25f;
    public float tempoEntreAtaques = 3.5f;

    [Header("Ataque Corpo a Corpo")]
    public float distanciaSoco = 3f; // Distância que o Endy precisa estar para tomar um soco
    public float tempoEntreSocos = 2f; // Cooldown entre um soco e outro
    private float timerSoco;

    [Header("Magia da Areia")]
    public GameObject prefabAreia;
    public float tempoEntrePocas = 0.25f;
    private float timerAreia;

    public int NumeroAtordoamento { get; private set; }

    private Vector3 direcaoInvestida;
    private Rigidbody rb;
    private float timerAtaque; // Timer do ataque de giro
    private bool ataqueTeleguiado = false;
    private bool ignorarPedrasNoArranque = false;

    [Header("Controle da Arena")]
    public GameObject barreirasInvisiveis;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null) rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    void FixedUpdate()
    {
        // Se estiver num estado parado (incluindo o novo Atacando), não se move
        if (estadoAtual == EstadoChefe.Derrotado || estadoAtual == EstadoChefe.Escondido ||
            estadoAtual == EstadoChefe.Atordoado || estadoAtual == EstadoChefe.PreparandoGiro ||
            estadoAtual == EstadoChefe.Atacando)
        {
            if (rb != null) rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            return;
        }

        if (estadoAtual == EstadoChefe.Investindo)
        {
            if (ataqueTeleguiado && alvoEndy != null)
            {
                Vector3 posAlvo = new Vector3(alvoEndy.position.x, transform.position.y, alvoEndy.position.z);
                transform.LookAt(posAlvo);
                direcaoInvestida = (posAlvo - transform.position).normalized; // Erro de digitação corrigido aqui!
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
            timerAtaque += Time.fixedDeltaTime;
            timerSoco += Time.fixedDeltaTime;

            float distanciaDoEndy = 100f;
            if (alvoEndy != null)
            {
                distanciaDoEndy = Vector3.Distance(transform.position, alvoEndy.position);
            }

            // 1ª Prioridade: Bater se o Endy estiver colado
            if (distanciaDoEndy <= distanciaSoco && timerSoco >= tempoEntreSocos)
            {
                StartCoroutine(RotinaAtaqueCorpoACorpo());
            }
            // 2ª Prioridade: Dar o ataque de giro se o tempo estourou
            else if (timerAtaque >= tempoEntreAtaques)
            {
                StartCoroutine(RotinaPrepararGiro());
            }
            // 3ª Prioridade: Apenas caminhar até o Endy
            else
            {
                Vector3 posAlvo = new Vector3(alvoEndy.position.x, transform.position.y, alvoEndy.position.z);
                transform.LookAt(posAlvo);
                Vector3 dir = (posAlvo - transform.position).normalized;
                rb.linearVelocity = new Vector3(dir.x * velPerseguicao, rb.linearVelocity.y, dir.z * velPerseguicao);
            }
        }
    }

    // --- NOVA MECÂNICA DE SOCO ---
    IEnumerator RotinaAtaqueCorpoACorpo()
    {
        estadoAtual = EstadoChefe.Atacando;
        timerSoco = 0f;

        // Olha para o Endy antes de bater
        if (alvoEndy != null)
        {
            Vector3 posAlvo = new Vector3(alvoEndy.position.x, transform.position.y, alvoEndy.position.z);
            transform.LookAt(posAlvo);
        }

        // Toca a animação. Se o seu trigger no Animator tiver outro nome (como "Bater"), mude a palavra "Atacar" abaixo.
        if (animChefe != null) animChefe.SetTrigger("Investir");

        // Tempo até a "garra" bater no Endy durante a animação (ajuste conforme a sua animação)
        yield return new WaitForSeconds(0.4f);

        // Confere se o Endy não desviou correndo no meio do soco
        if (alvoEndy != null && Vector3.Distance(transform.position, alvoEndy.position) <= distanciaSoco + 0.5f)
        {
            VidaEndy vida = alvoEndy.GetComponent<VidaEndy>();
            if (vida != null) vida.TomarDano();
        }

        // Tempo para a animação terminar de tocar antes dele voltar a andar
        yield return new WaitForSeconds(1.0f);

        if (estadoAtual == EstadoChefe.Atacando)
        {
            estadoAtual = EstadoChefe.Perseguindo;
            if (animChefe != null) animChefe.SetTrigger("Perseguir");
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

    public void InvestidaPunitiva(bool teleguiado)
    {
        if (estadoAtual == EstadoChefe.Derrotado) return;

        StopAllCoroutines();
        StartCoroutine(RotinaInvestidaImediata(teleguiado));
    }

    IEnumerator RotinaInvestidaImediata(bool teleguiado)
    {
        ataqueTeleguiado = teleguiado;
        ignorarPedrasNoArranque = true;
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

        if (barreirasInvisiveis != null)
        {
            barreirasInvisiveis.SetActive(false);
        }

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