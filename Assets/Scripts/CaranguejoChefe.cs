using UnityEngine;
using System.Collections;

public class CaranguejoChefe : MonoBehaviour
{
    // Novo estado "Atacando" adicionado
    public enum EstadoChefe { Escondido, Perseguindo, PreparandoGiro, Investindo, Atordoado, Derrotado, Atacando, Dancando, ReagindoComRaiva }

    [Header("Configurações do Chefe")]
    public EstadoChefe estadoAtual = EstadoChefe.Escondido;
    public Transform alvoEndy;
    public Animator animChefe;

    [Header("Atributos de Combate")]
    public float velPerseguicao = 6f;
    public float velInvestida = 25f;
    public float tempoEntreAtaques = 3.5f;
    private float velPerseguicaoBase;
    private float velInvestidaBase;
    private float fimEfeitoVelocidade;

    [Header("Reação ao rádio")]
    [Min(1f)] public float velocidadeRaivaRadio = 1.35f;
    [Min(1f)] public float velocidadeRaivaRadioDesligado = 1.75f;
    public int NivelRaivaRadio { get; private set; }
    public bool EstaDancando => estadoAtual == EstadoChefe.Dancando;
    private bool jaDistraiuComRadio;
    private EfeitoTocaDiscos radioDaDistracao;
    private float fimDanca;
    private DancaCaranguejo danca;
    [Header("Gesto de raiva")]
    [Min(0.3f)] public float duracaoGestoRaiva = 1.35f;
    public bool EstaReagindoComRaiva => estadoAtual == EstadoChefe.ReagindoComRaiva;
    public float DuracaoReacaoRaiva { get; private set; }
    private float fimGestoRaiva;
    private RaivaCaranguejo gestoRaiva;
    private float IntervaloAtaquesRadio => NivelRaivaRadio == 2 ? 0.55f : NivelRaivaRadio == 1 ? 0.8f : 1f;

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
    public bool EstaCalmo { get; private set; }

    [Header("Controle da Arena")]
    public GameObject barreirasInvisiveis;

    void Awake()
    {
        velPerseguicaoBase = velPerseguicao;
        velInvestidaBase = velInvestida;

        rb = GetComponent<Rigidbody>();
        if (rb != null) rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    void FixedUpdate()
    {
        // Não depende das corrotinas de ataque, que são interrompidas pelos baús.
        if (fimEfeitoVelocidade > 0f && Time.time >= fimEfeitoVelocidade)
            RestaurarVelocidadePocao();

        if (EstaDancando && Time.time >= fimDanca) EnfurecerComRadio(1);
        if (EstaReagindoComRaiva && Time.time >= fimGestoRaiva)
        {
            PararGestoRaiva();
            estadoAtual = EstadoChefe.Perseguindo;
            InvestidaPunitiva(true);
        }

        // Se estiver num estado parado (incluindo o novo Atacando), não se move
        if (estadoAtual == EstadoChefe.Derrotado || estadoAtual == EstadoChefe.Escondido ||
            estadoAtual == EstadoChefe.Atordoado || estadoAtual == EstadoChefe.PreparandoGiro ||
            estadoAtual == EstadoChefe.Atacando || EstaDancando || EstaReagindoComRaiva)
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
            if (distanciaDoEndy <= distanciaSoco && timerSoco >= tempoEntreSocos * IntervaloAtaquesRadio)
            {
                StartCoroutine(RotinaAtaqueCorpoACorpo());
            }
            // 2ª Prioridade: Dar o ataque de giro se o tempo estourou
            else if (timerAtaque >= tempoEntreAtaques * IntervaloAtaquesRadio)
            {
                StartCoroutine(RotinaPrepararGiro());
            }
            // 3ª Prioridade: Apenas caminhar até o Endy
            else if (alvoEndy != null && rb != null)
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
        if (estadoAtual == EstadoChefe.Escondido && !EstaCalmo)
        {
            estadoAtual = EstadoChefe.Perseguindo;
            if (animChefe != null) animChefe.SetTrigger("Perseguir");
        }
    }

    public void InvestidaPunitiva(bool teleguiado)
    {
        if (estadoAtual == EstadoChefe.Derrotado || estadoAtual == EstadoChefe.Atordoado ||
            EstaCalmo || EstaDancando || EstaReagindoComRaiva) return;

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
            if (obj.GetComponentInParent<BarreiraDaAgua>() != null)
            {
                // A margem encerra a corrida sem contar como pedra ou atordoamento.
                StopAllCoroutines();
                ataqueTeleguiado = false;
                ignorarPedrasNoArranque = false;
                timerAtaque = 0f;
                if (rb != null) rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
                estadoAtual = EstadoChefe.Perseguindo;
                if (animChefe != null) animChefe.SetTrigger("Perseguir");
                return;
            }
            if (obj.CompareTag("Pedra") && !ignorarPedrasNoArranque)
            {
                StopAllCoroutines();
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
        timerAtaque = timerSoco = 0f;
        if (rb != null) rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);

        // Die e apenas a pose de atordoamento; a derrota continua sendo um estado separado.
        if (!TocarAnimacaoReacao("Die") && animChefe != null) animChefe.SetTrigger("Atordoar");

        Debug.Log("CRASH! Chefe bateu na pedra!");
        yield return new WaitForSeconds(4f);

        if (estadoAtual == EstadoChefe.Atordoado)
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

        PararDanca();
        PararGestoRaiva();

        estadoAtual = EstadoChefe.Derrotado;
        EstaCalmo = false;
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

    public void FicarCalmo(float tempoDePaz)
    {
        if (estadoAtual == EstadoChefe.Derrotado) return;

        PararDanca();
        PararGestoRaiva();

        // Para qualquer investida ou ataque que ele esteja fazendo agora
        StopAllCoroutines();
        StartCoroutine(RotinaCalmo(tempoDePaz));
    }

    IEnumerator RotinaCalmo(float tempo)
    {
        EstaCalmo = true;
        ataqueTeleguiado = false;
        ignorarPedrasNoArranque = false;
        Debug.Log("O Chefe pegou o pirulito! Ele entrou num coma açucarado e está inofensivo!");

        // Usamos o estado "Escondido" para ele congelar no lugar sem fazer animação de dor
        estadoAtual = EstadoChefe.Escondido;

        // Tira ele da animação de correr/atacar para ele ficar apenas respirando (Idle)
        if (animChefe != null)
        {
            animChefe.ResetTrigger("Perseguir");
            animChefe.ResetTrigger("Investir");
            animChefe.ResetTrigger("Girar");
            // Se tiver uma animação base de parado, a Unity volta para ela sozinha
        }

        // Espera os segundos de paz
        yield return new WaitForSeconds(tempo);
        EstaCalmo = false;
        timerAtaque = 0f;
        timerSoco = 0f;

        if (estadoAtual != EstadoChefe.Derrotado)
        {
            Debug.Log("O açúcar acabou! O Caranguejo voltou a ficar furioso!");
            estadoAtual = EstadoChefe.Perseguindo;
            if (animChefe != null) animChefe.SetTrigger("Perseguir");
        }
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

    public void FicarRapido(float tempoDeEfeito)
    {
        if (estadoAtual == EstadoChefe.Derrotado) return;
        fimEfeitoVelocidade = Time.time + Mathf.Max(0.1f, tempoDeEfeito);
        AtualizarVelocidade();
    }

    void RestaurarVelocidadePocao()
    {
        fimEfeitoVelocidade = 0f;
        AtualizarVelocidade();
    }

    void AtualizarVelocidade()
    {
        float raiva = NivelRaivaRadio == 2 ? velocidadeRaivaRadioDesligado : NivelRaivaRadio == 1 ? velocidadeRaivaRadio : 1f;
        float investida = NivelRaivaRadio == 2 ? 1.4f : NivelRaivaRadio == 1 ? 1.2f : 1f;
        // A poção expira sem apagar a raiva que o rádio provocou.
        velPerseguicao = velPerseguicaoBase * raiva * (fimEfeitoVelocidade > 0f ? 2f : 1f);
        velInvestida = velInvestidaBase * investida * (fimEfeitoVelocidade > 0f ? 1.5f : 1f);
    }

    public bool OuvirRadio(EfeitoTocaDiscos radio, float duracao)
    {
        if (radio == null || !radio.Ligado || estadoAtual == EstadoChefe.Derrotado) return false;
        if (jaDistraiuComRadio) return radioDaDistracao == radio;
        if (EstaCalmo || EstaReagindoComRaiva || estadoAtual == EstadoChefe.Escondido || estadoAtual == EstadoChefe.Atordoado) return false;
        jaDistraiuComRadio = true;
        radioDaDistracao = radio;
        StopAllCoroutines();
        ataqueTeleguiado = false;
        ignorarPedrasNoArranque = false;
        timerAtaque = timerSoco = 0f;
        estadoAtual = EstadoChefe.Dancando;
        fimDanca = Time.time + Mathf.Max(0.1f, duracao);
        if (rb != null) rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        if (animChefe != null)
        {
            animChefe.ResetTrigger("Perseguir");
            animChefe.ResetTrigger("Investir");
            animChefe.ResetTrigger("Girar");
            animChefe.ResetTrigger("Atordoar");
            if (animChefe.HasState(0, Animator.StringToHash("Base Layer.Idle"))) animChefe.Play("Base Layer.Idle", 0, 0);
        }
        danca = GetComponent<DancaCaranguejo>() ?? gameObject.AddComponent<DancaCaranguejo>();
        danca.Iniciar(this);
        return true;
    }

    public void RadioDesligado(EfeitoTocaDiscos radio)
    {
        if (radioDaDistracao != radio || !jaDistraiuComRadio || estadoAtual == EstadoChefe.Derrotado) return;
        EnfurecerComRadio(2);
    }

    void EnfurecerComRadio(int nivel)
    {
        NivelRaivaRadio = Mathf.Max(NivelRaivaRadio, nivel);
        AtualizarVelocidade();
        ReagirComRaiva();
    }

    public void ReagirComRaiva()
    {
        // Repetir o botão não prolonga o gesto nem cancela pirulito/atordoamento.
        if (!isActiveAndEnabled || EstaReagindoComRaiva || EstaCalmo ||
            estadoAtual == EstadoChefe.Derrotado || estadoAtual == EstadoChefe.Atordoado) return;
        PararDanca();
        StopAllCoroutines();
        ataqueTeleguiado = ignorarPedrasNoArranque = false;
        timerAtaque = timerSoco = 0f;
        if (rb != null) rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        if (alvoEndy != null)
        {
            Vector3 direcao = alvoEndy.position - transform.position;
            direcao.y = 0;
            if (direcao.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direcao);
        }
        estadoAtual = EstadoChefe.ReagindoComRaiva;
        float duracao = Mathf.Max(0.3f, duracaoGestoRaiva);
        gestoRaiva = GetComponent<RaivaCaranguejo>();
        if (gestoRaiva != null) gestoRaiva.Encerrar();
        if (TocarAnimacaoReacao("Summon Attack"))
        {
            // Nao corta o clipe caso ele seja mais longo que o tempo configurado.
            foreach (AnimationClip clip in animChefe.runtimeAnimatorController.animationClips)
                if (clip.name == "Summon Attack") duracao = Mathf.Max(duracao, clip.length + 0.08f);
        }
        else
        {
            // Mantem compatibilidade com modelos sem os clipes do Hermit King.
            TocarAnimacaoReacao("Idle");
            if (gestoRaiva == null) gestoRaiva = gameObject.AddComponent<RaivaCaranguejo>();
            gestoRaiva.Iniciar(this, duracao);
        }
        DuracaoReacaoRaiva = duracao;
        fimGestoRaiva = Time.time + duracao;
    }

    bool TocarAnimacaoReacao(string nome)
    {
        if (animChefe == null || !animChefe.isActiveAndEnabled) return false;
        int estado = Animator.StringToHash("Base Layer." + nome);
        if (!animChefe.HasState(0, estado)) return false;
        animChefe.ResetTrigger("Perseguir");
        animChefe.ResetTrigger("Investir");
        animChefe.ResetTrigger("Girar");
        animChefe.ResetTrigger("Atordoar");
        animChefe.CrossFadeInFixedTime(estado, 0.08f, 0, 0f);
        return true;
    }

    void PararGestoRaiva()
    {
        if (EstaReagindoComRaiva) TocarAnimacaoReacao("Idle");
        fimGestoRaiva = 0;
        if (gestoRaiva != null) gestoRaiva.Encerrar();
    }

    void PararDanca()
    {
        fimDanca = 0;
        if (danca != null) danca.Encerrar();
    }

    void OnDisable()
    {
        PararDanca();
        PararGestoRaiva();
        if (EstaDancando || EstaReagindoComRaiva) estadoAtual = EstadoChefe.Perseguindo;
        if (fimEfeitoVelocidade > 0f) RestaurarVelocidadePocao();
    }
}
