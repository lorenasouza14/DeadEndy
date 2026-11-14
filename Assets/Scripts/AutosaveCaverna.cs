using UnityEngine;
using StarterAssets;

// Fica no PontoEntrada da caverna. O jogador persiste ao viajar para o Kraken.
[DisallowMultipleComponent]
public sealed class AutosaveCaverna : AutosaveDeFase
{
    DadosJogo progresso;
    InteracaoBauLanterna bau;
    GerenciadorFantasmasCaverna fantasmas;
    VidaJogadorKraken vidaKraken;
    PerigosContatoCaverna perigos;
    InventarioEndy inventario;
    DiarioEndy diario;
    int superficiesTemporarias;

    protected override bool JogadorMorto => vidaKraken != null && vidaKraken.Morto;
    protected override float VidaAtual => vidaKraken.VidaAtual;
    protected override bool PodeGravar => movimento != null;
    protected override bool PodeAtualizarPonto() => perigos.LongeDePerigo && SobrePisoSeguro(superficiesTemporarias);

    public Pose Preparar(ThirdPersonController jogador, Pose entrada, bool usarPosicaoSalva)
    {
        movimento = jogador;
        inventario = jogador.GetComponent<InventarioEndy>();
        diario = jogador.GetComponent<DiarioEndy>();
        vidaKraken = jogador.GetComponent<VidaJogadorKraken>();
        perigos = GetComponent<PerigosContatoCaverna>();
        superficiesTemporarias = LayerMask.GetMask("PonteFantasma", "Water", "Fantasma");
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
                diario.RestaurarPagina(progresso.paginaDiario);
                diario.RestaurarLeitura(progresso.diarioLido);
            }
        }

        if (progresso.cena == ProgressoJogo.CenaCaverna)
        {
            var caverna = progresso.caverna;
            PedraBrilhante.RestaurarProgresso(caverna.pedrasAcesas);
            if (caverna.fantasmasDespertos && fantasmas != null) fantasmas.AtivarSpawnsAposPedraVermelha();
            if (caverna.temLanterna && bau != null) bau.RestaurarLanterna(caverna.lanternaLigada);
            if (usarPosicaoSalva)
            {
                pontoSeguro = new Vector3(progresso.x, progresso.y, progresso.z);
                direcaoSegura = progresso.direcao;
                entrada = new Pose(pontoSeguro, Quaternion.Euler(0, direcaoSegura, 0));
                if (progresso.versao >= 4 && !vidaKraken.Morto)
                    vidaKraken.RestaurarVida(Mathf.RoundToInt(progresso.vida));
                vidaSegura = vidaKraken.Morto ? vidaKraken.vidaMaxima : vidaKraken.VidaAtual;
                temPontoSeguro = true;
            }
        }
        PedraBrilhante.ProgressoAlterado += SolicitarAutosave;
        if (bau != null) bau.LanternaEntregue += SolicitarAutosave;
        IniciarAutosave();
        if (progresso.cena != ProgressoJogo.CenaCaverna) SolicitarAutosave();
        return entrada;
    }

    protected override DadosJogo CapturarDados()
    {
        var lanterna = inventario != null ? inventario.Lanterna : LanternaJogador.Instancia;
        if (progresso.temDiario && diario != null)
        {
            progresso.paginaDiario = diario.PaginaEsquerda;
            progresso.diarioLido = diario.FoiLido;
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
        PedraBrilhante.ProgressoAlterado -= SolicitarAutosave;
        if (bau != null) bau.LanternaEntregue -= SolicitarAutosave;
        base.OnDestroy();
    }
}
