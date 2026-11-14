using System.Collections;
using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(-100), DisallowMultipleComponent, RequireComponent(typeof(InventarioEndy))]
public class AutosaveEndy : AutosaveDeFase
{
    public static AutosaveEndy Instancia { get; private set; }
    InventarioEndy inventario;
    VidaEndy vida;
    CaranguejoChefe chefe;
    RecompensaDiario recompensa;
    AlavancaTriangulo[] alavancas;
    InteracaoBau[] baus;
    GatilhoArena[] entradas;
    PuzzleAltaresRuinas[] ruinas;
    CaldeiraoSopa[] sopas;
    BananaSopa[] bananas;
    DadosJogo carregado;
    bool arena;
    protected override bool JogadorMorto => vida != null && vida.EstaMorto;
    protected override float VidaAtual => vida != null ? vida.VidaAtual : 5;
    protected override bool PodeGravar => !EmCombate && inventario.isActiveAndEnabled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ReiniciarInstancia() { Instancia = null; }

    public static string Identificar(Component componente)
    {
        string caminho = componente.name;
        for (var pai = componente.transform.parent; pai != null; pai = pai.parent) caminho = pai.name + "/" + caminho;
        return componente.GetType().Name + "|" + caminho;
    }

    void Awake()
    {
        Instancia = this;
        inventario = GetComponent<InventarioEndy>();
        vida = GetComponent<VidaEndy>();
        movimento = GetComponent<StarterAssets.ThirdPersonController>();
        carregado = ProgressoJogo.ConsumirEntrada(out novaPartida, out string aviso);
        if (carregado != null && carregado.cena == ProgressoJogo.CenaCaverna)
        {
            carregado = null;
            aviso = "O checkpoint está na caverna. Use Continuar no menu para retomá-lo.";
        }
        saveInvalido = carregado == null && !novaPartida && ProgressoJogo.ExisteArquivo;
        UltimoAviso = aviso;
        if (carregado == null) return;
        var controle = GetComponent<CharacterController>();
        bool ativo = controle != null && controle.enabled;
        if (controle != null) controle.enabled = false;
        transform.SetPositionAndRotation(new Vector3(carregado.x, carregado.y, carregado.z), Quaternion.Euler(0, carregado.direcao, 0));
        if (movimento != null && movimento.CinemachineCameraTarget != null)
            movimento.CinemachineCameraTarget.transform.rotation = transform.rotation;
        if (controle != null) controle.enabled = ativo;
        Physics.SyncTransforms();
        pontoSeguro = transform.position;
        direcaoSegura = carregado.direcao;
        vidaSegura = carregado.vida;
        temPontoSeguro = true;
    }

    T[] NaCena<T>() where T : Component => FindObjectsByType<T>(FindObjectsInactive.Include)
        .Where(c => c.gameObject.scene == gameObject.scene).ToArray();

    IEnumerator Start()
    {
        // Aguarda os Start que apagam as tochas e inicializam a fila dos baús.
        yield return null;
        if (!ContextoDeJogo) yield break;
        chefe = NaCena<CaranguejoChefe>().FirstOrDefault();
        recompensa = NaCena<RecompensaDiario>().FirstOrDefault();
        alavancas = NaCena<AlavancaTriangulo>();
        baus = NaCena<InteracaoBau>();
        entradas = NaCena<GatilhoArena>();
        ruinas = NaCena<PuzzleAltaresRuinas>();
        sopas = NaCena<CaldeiraoSopa>();
        bananas = NaCena<BananaSopa>();
        if (carregado != null)
        {
            if (vida != null) vida.RestaurarVidaDoSave(carregado.vida);
            foreach (var alavanca in alavancas)
                if (carregado.alavancas.Contains(alavanca.idSave)) alavanca.RestaurarPuxada();
            if (carregado.chefeVencido)
            {
                if (chefe != null) chefe.RestaurarDerrota(new Vector3(carregado.chefeX, carregado.chefeY, carregado.chefeZ));
                foreach (var entrada in entradas) entrada.RestaurarArenaConcluida();
                foreach (var bau in baus)
                    if (carregado.baus.Contains(bau.idSave)) bau.RestaurarAberto();
                if (recompensa != null) recompensa.RestaurarRecompensa(carregado.bauDiarioAberto, carregado.temDiario);
            }
            if (carregado.temDiario) inventario.ColetarItem(false, true);
            var diario = GetComponent<DiarioEndy>();
            if (diario != null)
            {
                diario.RestaurarPagina(carregado.paginaDiario);
                diario.RestaurarLeitura(carregado.diarioLido);
            }
            if (carregado.gorila != null)
            {
                foreach (var puzzle in ruinas)
                    puzzle.RestaurarProgresso(carregado.gorila.ruinas.FirstOrDefault(r => r.id == puzzle.idSave));
                foreach (var sopa in sopas)
                    sopa.RestaurarProgresso(carregado.gorila.sopas.FirstOrDefault(s => s.id == sopa.idSave));
                if (!string.IsNullOrEmpty(carregado.gorila.bananaNaMao))
                    inventario.RestaurarBananaNaMao(bananas.FirstOrDefault(b => b.idSave == carregado.gorila.bananaNaMao));
                Physics.SyncTransforms();
            }
        }
        var limites = LimitesPrimeiraFase.Garantir(gameObject.scene);
        if (limites != null && limites.CorrigirCheckpointBloqueado())
        {
            pontoSeguro = transform.position;
            direcaoSegura = transform.eulerAngles.y;
            vidaSegura = vida != null ? vida.VidaAtual : 5;
            temPontoSeguro = true;
        }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        var inputs = GetComponent<StarterAssets.StarterAssetsInputs>();
        if (inputs != null) { inputs.cursorLocked = inputs.cursorInputForLook = true; inputs.look = inputs.move = Vector2.zero; }
        IniciarAutosave();
    }

    bool EmCombate => chefe != null && !chefe.EncolhimentoConcluido &&
        (arena || (chefe.gameObject.activeInHierarchy && chefe.estadoAtual != CaranguejoChefe.EstadoChefe.Escondido) || baus.Any(b => b.BauAberto));

    protected override bool PodeAtualizarPonto()
    {
        if (EmCombate || InteracaoBau.PuzzleAtivo || inventario.ExecutandoAcao ||
            inventario.ItemNaMao != InventarioEndy.TipoItemNaMao.Nenhum || inventario.TemEfeitoDePocao ||
            AreiaMovedica.EndyNaAreia || (vida != null && Time.unscaledTime - vida.UltimoDanoEm < 5f)) return false;
        if (chefe == null || !chefe.EncolhimentoConcluido)
            foreach (var entrada in entradas)
                if (entrada.ContemOuEstaPerto(transform.position, 1.5f)) return false;
        return SobrePisoSeguro();
    }

    public void EntrarNaArena()
    {
        if (chefe != null && chefe.EncolhimentoConcluido) return;
        // A posição usada é a última fora do gatilho, nunca o ponto que fecha a porta.
        if (!arena) TentarSalvar();
        arena = true;
    }

    public void PrepararTravessia()
    {
        if (Pronto && !saveInvalido)
            ProgressoJogo.PrepararEntradaCaverna(CapturarDados(), novaPartida);
        else if (novaPartida) ProgressoJogo.PrepararNovoJogo();
    }

    protected override DadosJogo CapturarDados()
    {
        var diario = GetComponent<DiarioEndy>();
        bool venceu = chefe != null && chefe.EncolhimentoConcluido;
        return new DadosJogo
        {
            versao = 2, jogo = "DeadEndy", cena = gameObject.scene.path,
            vida = VidaAtual,
            temDiario = inventario.temDiario, paginaDiario = diario != null ? diario.PaginaEsquerda : 0,
            diarioLido = inventario.temDiario && diario != null && diario.FoiLido,
            chefeVencido = venceu,
            chefeX = chefe != null ? chefe.transform.position.x : 0,
            chefeY = chefe != null ? chefe.transform.position.y : 0,
            chefeZ = chefe != null ? chefe.transform.position.z : 0,
            bauDiarioAberto = recompensa != null && recompensa.bau != null && recompensa.bau.Aberto,
            alavancas = alavancas.Where(a => a.Concluida).Select(a => a.idSave).ToArray(),
            baus = venceu ? baus.Where(b => b.BauAberto).Select(b => b.idSave).ToArray() : new string[0],
            gorila = new DadosGorila
            {
                bananaNaMao = inventario.TemBanana ? inventario.BananaNaMao.idSave : null,
                ruinas = ruinas.Select(r => r.CapturarProgresso()).Where(r => r != null).ToArray(),
                sopas = sopas.Select(s => s.CapturarProgresso()).ToArray()
            }
        };
    }

    protected override void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
        base.OnDestroy();
    }
}
