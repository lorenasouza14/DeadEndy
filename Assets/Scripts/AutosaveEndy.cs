using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100), DisallowMultipleComponent, RequireComponent(typeof(InventarioEndy))]
public class AutosaveEndy : MonoBehaviour
{
    public static AutosaveEndy Instancia { get; private set; }
    [Min(300f)] public float intervalo = 300f;
    public bool Pronto { get; private set; }
    public string UltimoAviso { get; private set; }
    InventarioEndy inventario;
    VidaEndy vida;
    StarterAssets.ThirdPersonController movimento;
    CaranguejoChefe chefe;
    RecompensaDiario recompensa;
    AlavancaTriangulo[] alavancas;
    InteracaoBau[] baus;
    GatilhoArena[] entradas;
    DadosJogo carregado;
    bool novaPartida, saveInvalido, arena, pendente, temPontoSeguro;
    Vector3 pontoSeguro;
    float direcaoSegura, tempoDesdeSave, tempoNoChao;
    int vidaSegura;
    GameObject hud;
    TMP_Text textoStatus;
    float ocultarStatusEm;
    public bool ContextoDeJogo => gameObject.scene.path == ProgressoJogo.CenaJogo &&
        SceneManager.GetActiveScene() == gameObject.scene && !MenuManager.EstaAberto;

    void OnValidate() { intervalo = Mathf.Max(300f, intervalo); }

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
        intervalo = Mathf.Max(300f, intervalo);
        Instancia = this;
        inventario = GetComponent<InventarioEndy>();
        vida = GetComponent<VidaEndy>();
        movimento = GetComponent<StarterAssets.ThirdPersonController>();
        carregado = ProgressoJogo.ConsumirEntrada(out novaPartida, out string aviso);
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
            pendente = false;
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
        CriarStatus();
        Pronto = true;
        tempoDesdeSave = 0;
        if (!string.IsNullOrEmpty(UltimoAviso)) MostrarStatus(UltimoAviso, 10f);
    }

    bool EmCombate => chefe != null && !chefe.EncolhimentoConcluido &&
        (arena || (chefe.gameObject.activeInHierarchy && chefe.estadoAtual != CaranguejoChefe.EstadoChefe.Escondido) || baus.Any(b => b.BauAberto));

    bool PodeAtualizarPonto()
    {
        if (EmCombate || movimento == null || !movimento.Grounded || !movimento.enabled ||
            Time.timeScale == 0 || InteracaoBau.PuzzleAtivo || inventario.ExecutandoAcao ||
            inventario.ItemNaMao != InventarioEndy.TipoItemNaMao.Nenhum || inventario.TemEfeitoDePocao ||
            AreiaMovedica.EndyNaAreia || (vida != null && Time.unscaledTime - vida.UltimoDanoEm < 5f)) return false;
        if (chefe == null || !chefe.EncolhimentoConcluido)
            foreach (var entrada in entradas)
                if (entrada.ContemOuEstaPerto(transform.position, 1.5f)) return false;
        return Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, out var hit, 1f,
            movimento.GroundLayers, QueryTriggerInteraction.Ignore) &&
            hit.normal.y > 0.5f && hit.collider.GetComponentInParent<AreiaTemporaria>() == null;
    }

    void LateUpdate()
    {
        if (!Pronto || !ContextoDeJogo) return;
        if (textoStatus != null && Time.unscaledTime > ocultarStatusEm) textoStatus.gameObject.SetActive(false);
        if (vida != null && vida.EstaMorto) return;
        if (PodeAtualizarPonto())
        {
            tempoNoChao += Time.unscaledDeltaTime;
            if (tempoNoChao >= 0.5f)
            {
                pontoSeguro = transform.position;
                direcaoSegura = transform.eulerAngles.y;
                vidaSegura = vida != null ? vida.VidaAtual : 5;
                temPontoSeguro = true;
            }
        }
        else tempoNoChao = 0;
        if (Time.timeScale > 0 && Application.isFocused) tempoDesdeSave += Time.unscaledDeltaTime;
        if (pendente || tempoDesdeSave >= intervalo) TentarSalvar();
    }

    public void SolicitarAutosave() { pendente = true; }

    public void EntrarNaArena()
    {
        if (chefe != null && chefe.EncolhimentoConcluido) return;
        // A posição usada é a última fora do gatilho, nunca o ponto que fecha a porta.
        if (!arena) TentarSalvar();
        arena = true;
    }

    public bool TentarSalvar()
    {
        if (!Pronto || !ContextoDeJogo || saveInvalido || !temPontoSeguro || EmCombate ||
            !inventario.isActiveAndEnabled || (vida != null && vida.EstaMorto)) return false;
        var diario = GetComponent<DiarioEndy>();
        bool venceu = chefe != null && chefe.EncolhimentoConcluido;
        var dados = new DadosJogo
        {
            versao = 1, jogo = "DeadEndy", cena = gameObject.scene.path, salvoEmUtc = DateTime.UtcNow.ToString("O"),
            x = pontoSeguro.x, y = pontoSeguro.y, z = pontoSeguro.z, direcao = direcaoSegura, vida = vidaSegura,
            temDiario = inventario.temDiario, paginaDiario = diario != null ? diario.PaginaEsquerda : 0,
            diarioLido = inventario.temDiario && diario != null && diario.FoiLido,
            chefeVencido = venceu,
            chefeX = chefe != null ? chefe.transform.position.x : 0,
            chefeY = chefe != null ? chefe.transform.position.y : 0,
            chefeZ = chefe != null ? chefe.transform.position.z : 0,
            bauDiarioAberto = recompensa != null && recompensa.bau != null && recompensa.bau.Aberto,
            alavancas = alavancas.Where(a => a.Concluida).Select(a => a.idSave).ToArray(),
            baus = venceu ? baus.Where(b => b.BauAberto).Select(b => b.idSave).ToArray() : new string[0]
        };
        bool salvo = ProgressoJogo.Salvar(dados, novaPartida, out string erro);
        pendente = false;
        tempoDesdeSave = 0;
        if (salvo)
        {
            novaPartida = false;
            UltimoAviso = null;
            MostrarStatus("Progresso salvo", 2.5f);
        }
        else
        {
            UltimoAviso = erro;
            MostrarStatus("Não foi possível salvar. Verifique o espaço e a permissão da pasta do jogo.", 10f);
            Debug.LogWarning("Autosave: " + erro, this);
        }
        return salvo;
    }

    void CriarStatus()
    {
        hud = new GameObject("Aviso de autosave", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        hud.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        hud.GetComponent<Canvas>().sortingOrder = 300;
        var scaler = hud.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        var go = new GameObject("Estado", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(hud.transform, false);
        textoStatus = go.GetComponent<TextMeshProUGUI>();
        var fonte = GetComponent<IndicadorItemEndy>();
        if (fonte != null && fonte.fonte != null) textoStatus.font = fonte.fonte;
        textoStatus.fontSize = 24;
        textoStatus.color = new Color(1, 0.91f, 0.76f);
        textoStatus.outlineColor = new Color(0.2f, 0.1f, 0.05f);
        textoStatus.outlineWidth = 0.18f;
        textoStatus.alignment = TextAlignmentOptions.TopRight;
        textoStatus.raycastTarget = false;
        var rect = textoStatus.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-32, -24);
        rect.sizeDelta = new Vector2(650, 100);
        go.SetActive(false);
    }

    void MostrarStatus(string texto, float segundos)
    {
        if (textoStatus == null) return;
        textoStatus.text = texto;
        textoStatus.gameObject.SetActive(true);
        ocultarStatusEm = Time.unscaledTime + segundos;
    }

    void OnApplicationPause(bool pausado) { if (pausado) TentarSalvar(); }
    void OnApplicationQuit() { TentarSalvar(); }
    void OnDisable() { if (hud != null) hud.SetActive(false); }
    void OnDestroy() { if (Instancia == this) Instancia = null; if (hud != null) Destroy(hud); }
}
