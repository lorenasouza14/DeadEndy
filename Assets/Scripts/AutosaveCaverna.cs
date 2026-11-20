using UnityEngine;
using StarterAssets;

// Fica no PontoEntrada da caverna. O jogador persiste ao viajar para o Kraken.
[DisallowMultipleComponent]
public sealed partial class AutosaveCaverna : AutosaveDeFase
{
    DadosJogo progresso;
    InteracaoBauLanterna bau;
    GerenciadorFantasmasCaverna fantasmas;
    VidaJogadorKraken vidaKraken;
    InventarioEndy inventario;
    DiarioEndy diario;

    protected override bool JogadorMorto => vidaKraken != null && vidaKraken.Morto;
    protected override float VidaAtual => vidaKraken.VidaAtual;
    protected override bool PodeGravar => movimento != null;
    // O checkpoint muda ao tocar uma pedra e fica fixo entre os toques.
    protected override bool PodeAtualizarPonto() => false;

    public Pose Preparar(ThirdPersonController jogador, Pose entrada)
    {
        movimento = jogador;
        inventario = jogador.GetComponent<InventarioEndy>();
        diario = jogador.GetComponent<DiarioEndy>();
        vidaKraken = jogador.GetComponent<VidaJogadorKraken>();
        bau = FindAnyObjectByType<InteracaoBauLanterna>();
        fantasmas = FindAnyObjectByType<GerenciadorFantasmasCaverna>();
        progresso = ProgressoJogo.ConsumirEntrada(out novaPartida, out string aviso);
        saveInvalido = progresso == null && !novaPartida && ProgressoJogo.ExisteArquivo;
        UltimoAviso = aviso;
        if (progresso == null)
            progresso = new DadosJogo { jogo = "DeadEndy", vida = 5, alavancas = new string[0], baus = new string[0] };
        if (progresso.gorila == null)
            progresso.gorila = new DadosGorila { ruinas = new DadosRuinas[0], sopas = new DadosSopa[0] };
        if (progresso.temDiario && inventario != null)
        {
            inventario.ColetarItem(false, true);
            if (diario != null)
            {
                diario.RestaurarProgresso(progresso.etapaDiario, progresso.paginasDiarioLidas);
                if (progresso.cena == ProgressoJogo.CenaCaverna) diario.RestaurarPagina(progresso.paginaDiario);
                diario.RestaurarLeitura(progresso.diarioLido);
            }
        }

        var caverna = progresso.cena == ProgressoJogo.CenaCaverna ? progresso.caverna : null;
        PedraBrilhante.RestaurarProgresso(caverna?.pedrasAcesas ?? 0);
        if (caverna != null)
        {
            entrada = new Pose(new Vector3(progresso.x, progresso.y, progresso.z),
                Quaternion.Euler(0f, progresso.direcao, 0f));
            if (caverna.temLanterna && bau != null) bau.RestaurarLanterna(caverna.lanternaLigada);
            if (caverna.fantasmasDespertos && fantasmas != null) fantasmas.AtivarSpawnsAposPedraVermelha();
        }
        pontoSeguro = entrada.position;
        direcaoSegura = entrada.rotation.eulerAngles.y;
        vidaSegura = vidaKraken.vidaMaxima;
        temPontoSeguro = true;
        PedraBrilhante.PedraSelecionada += SalvarAoTocarPedra;
        if (bau != null) bau.LanternaEntregue += SolicitarAutosave;
        IniciarAutosave();
        SolicitarAutosave();
        return entrada;
    }

    void SalvarAoTocarPedra()
    {
        if (JogadorMorto || !ContextoDeJogo) return;
        pontoSeguro = movimento.transform.position;
        direcaoSegura = movimento.transform.eulerAngles.y;
        if (!TentarSalvar()) SolicitarAutosave();
    }

    protected override DadosJogo CapturarDados()
    {
        var lanterna = inventario != null ? inventario.Lanterna : LanternaJogador.Instancia;
        if (progresso.temDiario && diario != null)
        {
            progresso.paginaDiario = diario.PaginaEsquerda;
            progresso.diarioLido = diario.FoiLido;
            progresso.etapaDiario = diario.EtapaDesbloqueada;
            progresso.paginasDiarioLidas = diario.PaginasLidas;
        }
        bool equipada = lanterna != null && lanterna.foiColetada;
        progresso.versao = 4;
        progresso.caverna = new DadosCaverna
        {
            pedrasAcesas = PedraBrilhante.ProgressoAtual,
            fantasmasDespertos = PedraBrilhante.ProgressoAtual > 0 || (fantasmas != null && fantasmas.Despertos),
            temLanterna = equipada,
            lanternaLigada = equipada && lanterna.Ligada
        };
        return progresso;
    }

    protected override void OnDestroy()
    {
        if (hudFuga != null) Destroy(hudFuga);
        PedraBrilhante.PedraSelecionada -= SalvarAoTocarPedra;
        if (bau != null) bau.LanternaEntregue -= SolicitarAutosave;
        base.OnDestroy();
    }
}
