using UnityEngine;
using StarterAssets;

// Fica no PontoEntrada da caverna. O jogador persiste ao viajar para o Kraken.
[DisallowMultipleComponent]
public sealed class AutosaveCaverna : AutosaveDeFase
{
    DadosJogo progresso;
    InteracaoBauLanterna bau;
    VidaJogadorKraken vidaKraken;
    InventarioEndy inventario;
    DiarioEndy diario;

    protected override bool JogadorMorto => vidaKraken != null && vidaKraken.Morto;
    protected override float VidaAtual => vidaKraken.VidaAtual;
    protected override bool PodeGravar => movimento != null;
    // A caverna tem um unico checkpoint. Caminhar e acertar pedras nao o deslocam.
    protected override bool PodeAtualizarPonto() => false;

    public Pose Preparar(ThirdPersonController jogador, Pose entrada)
    {
        movimento = jogador;
        inventario = jogador.GetComponent<InventarioEndy>();
        diario = jogador.GetComponent<DiarioEndy>();
        vidaKraken = jogador.GetComponent<VidaJogadorKraken>();
        bau = FindAnyObjectByType<InteracaoBauLanterna>();
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
            if (caverna.temLanterna && bau != null) bau.RestaurarLanterna(caverna.lanternaLigada);
        }
        // Ignora checkpoints antigos no meio da fase, incluindo os de saves anteriores.
        pontoSeguro = entrada.position;
        direcaoSegura = entrada.rotation.eulerAngles.y;
        vidaSegura = vidaKraken.vidaMaxima;
        temPontoSeguro = true;
        PedraBrilhante.RestaurarProgresso(0);
        if (bau != null) bau.LanternaEntregue += SolicitarAutosave;
        IniciarAutosave();
        SolicitarAutosave();
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
            // As pedras e a horda pertencem a tentativa atual, nao ao checkpoint.
            pedrasAcesas = 0,
            fantasmasDespertos = false,
            temLanterna = equipada,
            lanternaLigada = equipada && lanterna.Ligada
        };
        return progresso;
    }

    protected override void OnDestroy()
    {
        if (bau != null) bau.LanternaEntregue -= SolicitarAutosave;
        base.OnDestroy();
    }
}
