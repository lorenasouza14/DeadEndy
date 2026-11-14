using UnityEngine;

// Reaproveita apenas a mao e a exclusividade dos objetos existentes, sem inventario de bananas.
public partial class InventarioEndy
{
    public BananaSopa BananaNaMao { get; private set; }
    public bool TemBanana => BananaNaMao != null;

    public bool PodeInteragirComSopa
    {
        get
        {
            var vida = GetComponent<VidaEndy>();
            var controle = GetComponent<StarterAssets.ThirdPersonController>();
            return isActiveAndEnabled && Time.timeScale > 0f && !InteracaoBau.PuzzleAtivo &&
                (vida == null || !vida.EstaMorto) &&
                (controle == null || (controle.isActiveAndEnabled && !controle.EstaArremessado && !controle.IsDodging));
        }
    }

    public bool PegarBanana(BananaSopa banana)
    {
        if (!PodeInteragirComSopa || !PodeColetarPocao || encaixeMao == null ||
            Time.frameCount == frameColeta || banana == null || !banana.PodeColetar(this)) return false;
        BananaNaMao = banana;
        banana.ColocarNaMao(encaixeMao);
        frameColeta = Time.frameCount;
        AutosaveEndy.Instancia?.SolicitarAutosave();
        return true;
    }

    public bool RestaurarBananaNaMao(BananaSopa banana)
    {
        if (banana == null || banana.Depositada || TemBanana || !PodeColetarPocao || encaixeMao == null) return false;
        banana.gameObject.SetActive(true);
        BananaNaMao = banana;
        banana.ColocarNaMao(encaixeMao);
        frameColeta = Time.frameCount;
        return true;
    }

    public bool EntregarBanana(CaldeiraoSopa caldeirao)
    {
        if (!PodeInteragirComSopa || !TemBanana || Time.frameCount == frameColeta ||
            caldeirao == null || !caldeirao.PodeDepositar(this)) return false;
        var banana = BananaNaMao;
        if (!caldeirao.ReceberBanana(this, banana)) return false;
        BananaNaMao = null;
        frameColeta = Time.frameCount;
        AutosaveEndy.Instancia?.SolicitarAutosave();
        return true;
    }

    private void AtualizarBananaCarregada()
    {
        if (!TemBanana) return;
        var vida = GetComponent<VidaEndy>();
        // O golpe interrompe a interacao, mas nao desfaz uma coleta ja concluida.
        // PodeInteragirComSopa libera a entrega quando o arremesso terminar.
        if (vida != null && vida.EstaMorto) DevolverBanana();
    }

    public void DevolverBanana()
    {
        if (!TemBanana) return;
        var banana = BananaNaMao;
        BananaNaMao = null;
        banana.DevolverAoLocalDaColeta();
    }
}
