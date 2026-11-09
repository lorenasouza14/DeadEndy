using UnityEngine;
using System.Collections;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class InventarioEndy : MonoBehaviour
{
    public enum TipoItemNaMao { Nenhum, PocaoVerde, PocaoAzul, Pirulito, Trompete, Radio }
    public TipoItemNaMao ItemNaMao => TemRadio ? TipoItemNaMao.Radio :
        TemTrompete ? TipoItemNaMao.Trompete : TemPirulito ? TipoItemNaMao.Pirulito :
        pocaoSegurada != null ? (pocaoSegurada.tipo == PocaoMagica.TipoPocao.Encolher ? TipoItemNaMao.PocaoVerde : TipoItemNaMao.PocaoAzul) : TipoItemNaMao.Nenhum;
    public bool ExecutandoAcao => estaJogando || EstaBebendoPocao || EstaTocandoTrompete;
    public bool EstaEncolhido { get; private set; }
    public bool TemEfeitoDePocao => EstaEncolhido || fimVelocidade > 0f;
    [Header("Itens do Inventário")]
    public bool temDiario;
    public bool temPocao;
    [Header("Animações do Endy")]
    public Animator animEndy;
    public float tempoSincroniaArremesso = 0.35f;
    [Header("Arremesso (F)")]
    public GameObject prefabPocao;
    public Transform pontoDeSaida;
    public float forcaFrente = 14f;
    public float forcaParaCima = 4.5f;
    [Header("Mão e interface")]
    public GameObject iconeDiarioUI;
    public GameObject iconePocaoUI;
    [Tooltip("Objeto de exemplo já posicionado na mão. Seu encaixe será usado pelas duas poções.")]
    public GameObject pocaoNaMao3D;
    public Transform encaixeMao;
    public bool mostrarControles = true;

    private PocaoMagica pocaoSegurada;
    private EfeitoPirulito pirulitoSegurado;
    private EfeitoTrompete trompeteSegurado;
    private EfeitoTocaDiscos radioSegurado;
    public bool TemRadio => radioSegurado != null;
    private AnimacaoTrompete gestoTrompete;
    private Coroutine rotinaTrompete;
    public bool TemTrompete => trompeteSegurado != null;
    public bool EstaTocandoTrompete { get; private set; }
    public bool TemPirulito => pirulitoSegurado != null;
    private bool estaJogando;
    public bool EstaBebendoPocao { get; private set; }
    private Coroutine rotinaBeber;
    private int camadaBeber = -1;
    private int frameColeta = -1;
    private Vector3 posicaoNaMao;
    private Quaternion rotacaoNaMao = Quaternion.identity;
    private Vector3 escalaNaMao = Vector3.one;
    private StarterAssets.ThirdPersonController movimento;
    private float fimVelocidade;
    public bool PodeColetarPocao => !temPocao && !TemPirulito && !TemTrompete && !TemRadio && !estaJogando && !EstaTocandoTrompete && !EstaBebendoPocao;

    void Awake()
    {
        movimento = GetComponent<StarterAssets.ThirdPersonController>();
        if (animEndy == null) animEndy = GetComponentInChildren<Animator>();
        if (pocaoNaMao3D != null)
        {
            Transform exemplo = pocaoNaMao3D.transform;
            if (encaixeMao == null) encaixeMao = exemplo.parent;
            posicaoNaMao = exemplo.localPosition;
            rotacaoNaMao = exemplo.localRotation;
            escalaNaMao = exemplo.localScale;
            pocaoNaMao3D.SetActive(false);
        }
        if (encaixeMao == null)
        {
            foreach (Transform filho in GetComponentsInChildren<Transform>(true))
                if (filho.name == "handslot.r") { encaixeMao = filho; break; }
        }
        if (encaixeMao == null && animEndy != null && animEndy.isHuman)
            encaixeMao = animEndy.GetBoneTransform(HumanBodyBones.RightHand);
        AtualizarVisuais();
    }

    void Start()
    {
        if (temPocao && pocaoSegurada == null)
        {
            temPocao = false;
            ColetarItem(true, false);
        }
    }

    void Update()
    {
        if (fimVelocidade > 0 && Time.time >= fimVelocidade) RestaurarVelocidade();
        var vida = GetComponent<VidaEndy>();
        if (vida != null && vida.EstaMorto) { CancelarTrompete(); CancelarBebida(); return; }
        if ((!temPocao && !TemPirulito && !TemTrompete && !TemRadio) || estaJogando || EstaTocandoTrompete || EstaBebendoPocao || Time.frameCount == frameColeta || Time.timeScale == 0) return;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return;
        if (Keyboard.current.rKey.wasPressedThisFrame) BeberPocao();
        else if (Keyboard.current.fKey.wasPressedThisFrame) JogarPocao();
#else
        if (Input.GetKeyDown(KeyCode.R)) BeberPocao();
        else if (Input.GetKeyDown(KeyCode.F)) JogarPocao();
#endif
    }

    public bool ColetarPocao(PocaoMagica pocao)
    {
        if (!PodeColetarPocao || pocao == null || !pocao.DisponivelParaColeta) return false;
        if (encaixeMao == null)
        {
            Debug.LogWarning("Defina o Encaixe Mão ou o objeto de exemplo em InventarioEndy.", this);
            return false;
        }
        pocaoSegurada = pocao;
        pocao.PrepararNaMao();
        pocao.transform.SetParent(encaixeMao, false);
        pocao.transform.localPosition = posicaoNaMao;
        pocao.transform.localRotation = rotacaoNaMao;
        pocao.transform.localScale = escalaNaMao;
        temPocao = true;
        frameColeta = Time.frameCount;
        AtualizarVisuais();
        return true;
    }

    public void BeberPocao()
    {
        var vida = GetComponent<VidaEndy>();
        if (!isActiveAndEnabled || pocaoSegurada == null || estaJogando || EstaBebendoPocao ||
            Time.timeScale == 0 || (vida != null && vida.EstaMorto)) return;
        camadaBeber = animEndy != null && animEndy.isActiveAndEnabled ? animEndy.GetLayerIndex("Arremesso") : -1;
        AnimationClip clipe = null;
        if (camadaBeber >= 0 && animEndy.HasState(camadaBeber, Animator.StringToHash("Arremesso.BeberPocao")))
            foreach (var candidato in animEndy.runtimeAnimatorController.animationClips)
                if (candidato.name == "Use_Item") { clipe = candidato; break; }
        if (clipe != null) rotinaBeber = StartCoroutine(RotinaBeberPocao(clipe.length));
        else ConsumirPocao(); // Compatibilidade com personagens sem o clipe de beber.
    }

    IEnumerator RotinaBeberPocao(float duracao)
    {
        EstaBebendoPocao = true;
        animEndy.CrossFadeInFixedTime("Arremesso.BeberPocao", 0.1f, camadaBeber, 0f);
        // O frasco acompanha a mão até o gole; o bônus só começa nesse momento.
        yield return new WaitForSeconds(duracao * 0.6f);
        ConsumirPocao();
        yield return new WaitForSeconds(duracao * 0.4f + 0.1f);
        EstaBebendoPocao = false;
        rotinaBeber = null;
        camadaBeber = -1;
    }

    void CancelarBebida()
    {
        if (!EstaBebendoPocao) return;
        if (rotinaBeber != null) StopCoroutine(rotinaBeber);
        rotinaBeber = null;
        if (animEndy != null && animEndy.isActiveAndEnabled && camadaBeber >= 0)
            animEndy.Play("Arremesso.Vazio", camadaBeber, 0f);
        EstaBebendoPocao = false;
        camadaBeber = -1;
    }

    void ConsumirPocao()
    {
        if (pocaoSegurada == null) return;
        if (pocaoSegurada.tipo == PocaoMagica.TipoPocao.Encolher)
        {
            if (!EstaEncolhido)
                transform.localScale *= Mathf.Clamp(pocaoSegurada.fatorEncolhimento, 0.1f, 1f);
            EstaEncolhido = true;
        }
        else if (movimento != null)
        {
            movimento.MultiplicadorPocao = Mathf.Max(1f, pocaoSegurada.multiplicadorVelocidade);
            fimVelocidade = Time.time + Mathf.Max(0.1f, pocaoSegurada.tempoDeEfeito);
        }
        Destroy(pocaoSegurada.gameObject);
        pocaoSegurada = null;
        temPocao = false;
        AtualizarVisuais();
    }

    public void JogarPocao()
    {
        if (EstaBebendoPocao) return;
        if (TemRadio) { ColocarRadio(); return; }
        if (TemTrompete) { TocarTrompete(); return; }
        if ((pocaoSegurada != null || TemPirulito) && !estaJogando) StartCoroutine(RotinaJogarPocao());
    }

    public bool ColetarRadio(EfeitoTocaDiscos radio)
    {
        var vida = GetComponent<VidaEndy>();
        if (!PodeColetarPocao || radio == null || !radio.DisponivelParaColeta || encaixeMao == null || (vida != null && vida.EstaMorto)) return false;
        radioSegurado = radio;
        radio.PrepararNaMao(encaixeMao);
        frameColeta = Time.frameCount;
        return true;
    }

    public bool ColocarRadio()
    {
        var vida = GetComponent<VidaEndy>();
        if (!TemRadio || (vida != null && vida.EstaMorto) || !radioSegurado.TentarColocar(transform)) return false;
        radioSegurado = null;
        return true;
    }

    public bool ColetarTrompete(EfeitoTrompete trompete)
    {
        if (!PodeColetarPocao || trompete == null || !trompete.DisponivelParaColeta || encaixeMao == null) return false;
        trompeteSegurado = trompete;
        trompete.PrepararNaMao();
        trompete.transform.SetParent(encaixeMao, false);
        trompete.transform.localPosition = trompete.posicaoNaMao;
        trompete.transform.localRotation = Quaternion.Euler(trompete.rotacaoNaMao);
        trompete.transform.localScale = escalaNaMao;
        frameColeta = Time.frameCount;
        AtualizarVisuais();
        return true;
    }

    public void TocarTrompete()
    {
        var vida = GetComponent<VidaEndy>();
        if (!TemTrompete || EstaTocandoTrompete || estaJogando || (vida != null && vida.EstaMorto)) return;
        rotinaTrompete = StartCoroutine(RotinaTocarTrompete());
    }

    IEnumerator RotinaTocarTrompete()
    {
        EstaTocandoTrompete = true;
        EfeitoTrompete item = trompeteSegurado;
        if (gestoTrompete == null)
            gestoTrompete = GetComponent<AnimacaoTrompete>() ?? gameObject.AddComponent<AnimacaoTrompete>();
        gestoTrompete.Preparar(animEndy, item);
        yield return AnimarTrompete(0, 1, item.tempoLevantar);
        if (item != null)
        {
            item.AplicarEfeito();
            yield return new WaitForSeconds(item.tempoTocando);
            if (item != null) yield return AnimarTrompete(1, 0, item.tempoBaixar);
        }
        gestoTrompete.Encerrar();
        if (item != null) Destroy(item.gameObject);
        trompeteSegurado = null;
        EstaTocandoTrompete = false;
        rotinaTrompete = null;
        AtualizarVisuais();
    }

    IEnumerator AnimarTrompete(float inicio, float fim, float duracao)
    {
        duracao = Mathf.Max(0.1f, duracao);
        for (float tempo = 0; tempo < duracao; tempo += Time.deltaTime)
        {
            gestoTrompete.peso = Mathf.Lerp(inicio, fim, Mathf.SmoothStep(0, 1, tempo / duracao));
            yield return null;
        }
        gestoTrompete.peso = fim;
        // Garante que o bocal chegue à boca no LateUpdate antes de aplicar o efeito.
        yield return null;
    }

    void CancelarTrompete()
    {
        if (rotinaTrompete != null) StopCoroutine(rotinaTrompete);
        rotinaTrompete = null;
        if (gestoTrompete != null) gestoTrompete.Encerrar();
        if (trompeteSegurado != null && trompeteSegurado.JaTocou)
        {
            Destroy(trompeteSegurado.gameObject);
            trompeteSegurado = null;
        }
        EstaTocandoTrompete = false;
    }

    public bool ColetarPirulito(EfeitoPirulito pirulito)
    {
        if (!PodeColetarPocao || pirulito == null || !pirulito.DisponivelParaColeta || encaixeMao == null) return false;
        pirulitoSegurado = pirulito;
        pirulito.PrepararNaMao();
        pirulito.transform.SetParent(encaixeMao, false);
        pirulito.transform.localPosition = posicaoNaMao;
        pirulito.transform.localRotation = rotacaoNaMao;
        pirulito.transform.localScale = escalaNaMao;
        frameColeta = Time.frameCount;
        AtualizarVisuais();
        return true;
    }

    IEnumerator RotinaJogarPocao()
    {
        estaJogando = true;
        if (animEndy != null)
        {
            foreach (AnimatorControllerParameter parametro in animEndy.parameters)
                if (parametro.name == "Jogar" && parametro.type == AnimatorControllerParameterType.Trigger)
                    animEndy.SetTrigger("Jogar");
        }
        yield return new WaitForSeconds(tempoSincroniaArremesso);
        if (pirulitoSegurado != null)
        {
            pirulitoSegurado.transform.SetParent(null, true);
            if (pontoDeSaida != null) pirulitoSegurado.transform.position = pontoDeSaida.position;
            pirulitoSegurado.ConfigurarArremesso(transform,
                transform.forward * forcaFrente + Vector3.up * forcaParaCima);
            pirulitoSegurado = null;
        }
        PocaoMagica arremessada = pocaoSegurada;
        if (arremessada != null)
        {
            arremessada.transform.SetParent(null, true);
            if (pontoDeSaida != null) arremessada.transform.position = pontoDeSaida.position;
            arremessada.ConfigurarArremesso(GetComponent<VidaEndy>(), GetComponent<Collider>(),
                transform.forward * forcaFrente + Vector3.up * forcaParaCima);
        }
        pocaoSegurada = null;
        temPocao = false;
        estaJogando = false;
        AtualizarVisuais();
    }

    void AtualizarVisuais()
    {
        if (iconeDiarioUI != null) iconeDiarioUI.SetActive(temDiario);
        if (iconePocaoUI != null) iconePocaoUI.SetActive(temPocao);
        // O frasco coletado é o visual; o exemplo só fornece o encaixe.
        if (pocaoNaMao3D != null) pocaoNaMao3D.SetActive(false);
    }

    public void ColetarItem(bool pegouPocao, bool pegouDiario)
    {
        if (pegouDiario) temDiario = true;
        if (pegouPocao && PodeColetarPocao && prefabPocao != null)
        {
            GameObject instancia = Instantiate(prefabPocao);
            if (!ColetarPocao(instancia.GetComponent<PocaoMagica>())) Destroy(instancia);
        }
        AtualizarVisuais();
    }

    void RestaurarVelocidade()
    {
        if (fimVelocidade > 0 && movimento != null) movimento.MultiplicadorPocao = 1f;
        fimVelocidade = 0;
    }

    void OnDisable()
    {
        CancelarBebida();
        CancelarTrompete();
        StopAllCoroutines();
        estaJogando = false;
        RestaurarVelocidade();
    }

}
