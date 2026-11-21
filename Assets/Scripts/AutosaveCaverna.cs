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
    int pedrasDoCheckpoint;

    protected override bool JogadorMorto => vidaKraken != null && vidaKraken.Morto;
    protected override float VidaAtual => vidaKraken.VidaAtual;
    protected override bool PodeGravar => movimento != null;
    // A posicao so avanca ao completar um par de pedras, nunca pelo save periodico.
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
        // Saves antigos em pedras impares nao possuem a posicao do par anterior.
        pedrasDoCheckpoint = caverna != null && DadosCaverna.EhCheckpointDePedras(caverna.pedrasAcesas)
            ? caverna.pedrasAcesas : 0;
        PedraBrilhante.RestaurarProgresso(pedrasDoCheckpoint);
        if (caverna != null)
        {
            if (pedrasDoCheckpoint > 0)
                entrada = new Pose(new Vector3(progresso.x, progresso.y, progresso.z),
                    Quaternion.Euler(0f, progresso.direcao, 0f));
            if (caverna.temLanterna && bau != null) bau.RestaurarLanterna(caverna.lanternaLigada);
            if (pedrasDoCheckpoint > 0 && fantasmas != null) fantasmas.AtivarSpawnsAposPedraVermelha();
        }
        pontoSeguro = entrada.position;
        direcaoSegura = entrada.rotation.eulerAngles.y;
        vidaSegura = vidaKraken.vidaMaxima;
        temPontoSeguro = true;
        PedraBrilhante.PedraSelecionada += SalvarParDePedras;
        if (bau != null) bau.LanternaEntregue += SolicitarAutosave;
        IniciarAutosave();
        SolicitarAutosave();
        return entrada;
    }

    void SalvarParDePedras()
    {
        int quantidade = PedraBrilhante.ProgressoAtual;
        // Um erro ou uma pedra isolada nao substitui o ultimo par salvo.
        if (JogadorMorto || !ContextoDeJogo || quantidade <= pedrasDoCheckpoint ||
            !DadosCaverna.EhCheckpointDePedras(quantidade)) return;
        pedrasDoCheckpoint = quantidade;
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
            // Autosaves de pausa, saida e intervalo tambem respeitam o checkpoint fixo.
            pedrasAcesas = pedrasDoCheckpoint,
            fantasmasDespertos = pedrasDoCheckpoint > 0,
            temLanterna = equipada,
            lanternaLigada = equipada && lanterna.Ligada
        };
        return progresso;
    }

    protected override void OnDestroy()
    {
        if (hudFuga != null) Destroy(hudFuga);
        PedraBrilhante.PedraSelecionada -= SalvarParDePedras;
        if (bau != null) bau.LanternaEntregue -= SolicitarAutosave;
        base.OnDestroy();
    }
}
