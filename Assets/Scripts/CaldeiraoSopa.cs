using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class CaldeiraoSopa : MonoBehaviour
{
    [HideInInspector] public string idSave;
    [Tooltip("Exatamente as tres bananas dos desafios, sem repetir referencias.")]
    public BananaSopa[] bananasDaReceita = new BananaSopa[3];
    public GorilaSopa gorila;
    public InventarioEndy jogador;
    [Tooltip("Ponto junto da borda, na altura em que Endy deposita a banana.")]
    public Transform pontoInteracao;
    [Min(0.1f)] public float alcanceDeposito = 2.5f;
    public ParticleSystem vaporSopa;
    [Tooltip("Opcional: objeto visual que aparece quando a sopa esta pronta.")]
    public GameObject visualSopaPronta;
    public int Quantidade => recebidas.Count;
    public bool SopaPronta => Quantidade == 3;
    public Vector3 PontoInteracao => pontoInteracao != null ? pontoInteracao.position : transform.position + Vector3.up;
    private readonly HashSet<BananaSopa> recebidas = new HashSet<BananaSopa>();

    private void Awake()
    {
        if (string.IsNullOrEmpty(idSave)) idSave = AutosaveEndy.Identificar(this);
    }

    public DadosSopa CapturarProgresso()
    {
        return new DadosSopa { id = idSave, bananas = recebidas.Select(b => b.idSave).ToArray(),
            passagemLiberada = gorila != null && gorila.PassagemLiberada };
    }

    public void RestaurarProgresso(DadosSopa dados)
    {
        if (dados == null || !ReceitaValida()) return;
        recebidas.Clear();
        foreach (var banana in bananasDaReceita)
            if (System.Array.IndexOf(dados.bananas, banana.idSave) >= 0)
            {
                recebidas.Add(banana);
                banana.RestaurarDepositada();
            }
        if (!SopaPronta) return;
        MostrarSopaPronta();
        if (dados.passagemLiberada) gorila.RestaurarPassagemLiberada();
        else gorila.AtrairPelaSopa(this);
    }

    private void MostrarSopaPronta()
    {
        if (visualSopaPronta != null) visualSopaPronta.SetActive(true);
        if (vaporSopa != null) vaporSopa.Play();
    }

    private void Start()
    {
        if (jogador == null) jogador = FindAnyObjectByType<InventarioEndy>();
        if (visualSopaPronta != null && visualSopaPronta != gameObject) visualSopaPronta.SetActive(false);
        if (vaporSopa != null) vaporSopa.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (!ReceitaValida()) Debug.LogError("CaldeiraoSopa: associe tres bananas diferentes e o GorilaSopa com percurso e animacoes validos.", this);
    }

    public bool ReceitaValida()
    {
        return bananasDaReceita != null && bananasDaReceita.Length == 3 &&
            bananasDaReceita[0] != null && bananasDaReceita[1] != null && bananasDaReceita[2] != null &&
            new HashSet<BananaSopa>(bananasDaReceita).Count == 3 && gorila != null && gorila.Configurado;
    }

    public bool PodeDepositar(InventarioEndy portador)
    {
        return isActiveAndEnabled && !SopaPronta && ReceitaValida() && portador != null &&
            portador.PodeInteragirComSopa && portador.TemBanana &&
            System.Array.IndexOf(bananasDaReceita, portador.BananaNaMao) >= 0 &&
            !recebidas.Contains(portador.BananaNaMao) &&
            InteracaoSopa.AoAlcance(portador, PontoInteracao, alcanceDeposito, transform);
    }

    internal bool ReceberBanana(InventarioEndy portador, BananaSopa banana)
    {
        if (!PodeDepositar(portador) || banana == null || portador.BananaNaMao != banana || !banana.NaMao) return false;
        recebidas.Add(banana);
        banana.Consumir();
        if (SopaPronta)
        {
            MostrarSopaPronta();
            gorila.AtrairPelaSopa(this);
        }
        return true;
    }

    private void Update()
    {
        if (jogador == null || Time.timeScale <= 0f) return;
#if ENABLE_INPUT_SYSTEM
        bool apertou = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        bool apertou = Input.GetKeyDown(KeyCode.E);
#endif
        if (apertou) jogador.EntregarBanana(this);
    }

    public string ObterDica()
    {
        if (jogador == null || !jogador.mostrarControles || !jogador.PodeInteragirComSopa ||
            !InteracaoSopa.AoAlcance(jogador, PontoInteracao, alcanceDeposito, transform)) return null;
        if (SopaPronta) return "Sopa pronta! 3/3";
        return PodeDepositar(jogador) ? $"[E] Colocar banana ({Quantidade}/3)" : $"Bananas no caldeirão: {Quantidade}/3";
    }
}
