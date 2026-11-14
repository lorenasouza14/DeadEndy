using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public abstract class AutosaveDeFase : MonoBehaviour
{
    [Min(300f)] public float intervalo = 300f;
    public bool Pronto { get; protected set; }
    public string UltimoAviso { get; protected set; }
    protected StarterAssets.ThirdPersonController movimento;
    protected bool novaPartida, saveInvalido, temPontoSeguro;
    protected Vector3 pontoSeguro;
    protected float direcaoSegura, vidaSegura;
    bool pendente;
    float tempoDesdeSave, tempoNoChao;
    GameObject hud;
    TMP_Text textoStatus;
    float ocultarStatusEm;
    public bool ContextoDeJogo => SceneManager.GetActiveScene() == gameObject.scene && !MenuManager.EstaAberto &&
        ProgressoJogo.EhCenaJogo(gameObject.scene.path);

    protected abstract bool JogadorMorto { get; }
    protected abstract bool PodeGravar { get; }
    protected abstract bool PodeAtualizarPonto();
    protected abstract DadosJogo CapturarDados();
    protected abstract float VidaAtual { get; }

    protected void OnValidate() { intervalo = Mathf.Max(300f, intervalo); }

    protected void IniciarAutosave()
    {
        intervalo = Mathf.Max(300f, intervalo);
        CriarStatus();
        pendente = false;
        Pronto = true;
        if (!string.IsNullOrEmpty(UltimoAviso)) MostrarStatus(UltimoAviso, 10f);
    }

    protected bool SobrePisoSeguro(int mascaraExcluida = 0)
    {
        return movimento != null && movimento.isActiveAndEnabled && movimento.Grounded && Time.timeScale > 0 &&
            Physics.Raycast(movimento.transform.position + Vector3.up * 0.3f, Vector3.down, out var hit, 1f,
                movimento.GroundLayers, QueryTriggerInteraction.Ignore) && hit.normal.y > 0.5f &&
            (mascaraExcluida & (1 << hit.collider.gameObject.layer)) == 0 && hit.collider.GetComponentInParent<AreiaTemporaria>() == null;
    }

    protected void LateUpdate()
    {
        if (!Pronto || !ContextoDeJogo) return;
        if (textoStatus != null && Time.unscaledTime > ocultarStatusEm) textoStatus.gameObject.SetActive(false);
        if (JogadorMorto) return;
        if (PodeAtualizarPonto())
        {
            tempoNoChao += Time.unscaledDeltaTime;
            if (tempoNoChao >= 0.5f)
            {
                pontoSeguro = movimento.transform.position;
                direcaoSegura = movimento.transform.eulerAngles.y;
                vidaSegura = VidaAtual;
                temPontoSeguro = true;
            }
        }
        else tempoNoChao = 0;
        if (Time.timeScale > 0 && Application.isFocused) tempoDesdeSave += Time.unscaledDeltaTime;
        if (pendente || tempoDesdeSave >= intervalo) TentarSalvar();
    }

    public void SolicitarAutosave() { pendente = true; }

    public bool TentarSalvar()
    {
        if (!Pronto || !ContextoDeJogo || saveInvalido || !temPontoSeguro || JogadorMorto || !PodeGravar) return false;
        var dados = CapturarDados();
        dados.cena = gameObject.scene.path;
        dados.salvoEmUtc = DateTime.UtcNow.ToString("O");
        dados.x = pontoSeguro.x;
        dados.y = pontoSeguro.y;
        dados.z = pontoSeguro.z;
        dados.direcao = direcaoSegura;
        dados.vida = vidaSegura;
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
        var fonte = movimento != null ? movimento.GetComponent<IndicadorItemEndy>() : null;
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

    protected void OnApplicationPause(bool pausado) { if (pausado) TentarSalvar(); }
    protected void OnApplicationQuit() { TentarSalvar(); }
    protected void OnDisable() { if (hud != null) hud.SetActive(false); }
    protected virtual void OnDestroy() { if (hud != null) Destroy(hud); }
}
