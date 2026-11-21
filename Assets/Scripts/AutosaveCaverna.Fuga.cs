using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed partial class AutosaveCaverna
{
    const float DuracaoFuga = 50f;
    float tempoFuga = DuracaoFuga;
    bool fugaAtiva;
    bool animandoMorteFuga;
    bool cutscenePortaIniciada, cutscenePortaConcluida;
    GameObject hudFuga;
    TMP_Text textoFuga;

    void Update()
    {
        if (animandoMorteFuga) return;
        bool exibir = Pronto && ContextoDeJogo && !JogadorMorto &&
            !PedraBrilhante.ProximaCorEsperada.HasValue && !TelaCarregamento.EmCurso;
        if (PedraBrilhante.ProximaCorEsperada.HasValue)
            cutscenePortaIniciada = cutscenePortaConcluida = false;
        if (exibir && !cutscenePortaIniciada)
        {
            cutscenePortaIniciada = true;
            CutsceneInicio.IniciarPortaCaverna(() =>
            {
                if (this) cutscenePortaConcluida = true;
            });
        }
        exibir = exibir && cutscenePortaConcluida;
        if (hudFuga != null) hudFuga.SetActive(exibir);
        if (!exibir) { fugaAtiva = false; return; }
        if (!fugaAtiva)
        {
            fugaAtiva = true;
            tempoFuga = DuracaoFuga;
            if (hudFuga == null) CriarCronometro();
        }
        else tempoFuga = Mathf.Max(0f, tempoFuga - Time.deltaTime);
        if (tempoFuga <= 0f)
        {
            StartCoroutine(MorrerAntesDeReiniciarFuga());
            return;
        }
        int segundosRestantes = Mathf.CeilToInt(tempoFuga);
        textoFuga.text = "ENCONTRE A SAÍDA\n" + (segundosRestantes / 60) + ":" + (segundosRestantes % 60).ToString("00")
            + "\n<size=26>Corra antes que a porta se feche!</size>";
        textoFuga.color = tempoFuga <= 10f ? new Color(1f, .3f, .2f) : new Color(1f, .91f, .76f);
    }

    System.Collections.IEnumerator MorrerAntesDeReiniciarFuga()
    {
        animandoMorteFuga = true;
        if (hudFuga) hudFuga.SetActive(false);
        var efeito = movimento.GetComponent<VidaEndy>();
        var animator = efeito && efeito.animEndy ? efeito.animEndy : movimento.GetComponentInChildren<Animator>();
        bool movimentoAtivo = movimento.enabled;
        bool inventarioAtivo = inventario && inventario.enabled;
        float escalaAnterior = Time.timeScale;
        var modoAnterior = animator ? animator.updateMode : AnimatorUpdateMode.Normal;
        // Congela ataques e controles; somente a animacao de morte segue em tempo real.
        movimento.enabled = false;
        if (inventario) inventario.enabled = false;
        Time.timeScale = 0f;
        try
        {
            if (animator)
            {
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                animator.ResetTrigger("TomarDano");
                animator.SetTrigger("Morrer");
            }
            yield return new WaitForSecondsRealtime(efeito ? Mathf.Max(.1f, efeito.tempoAnimacaoMorte) : 2.5f);
            if (movimento && ContextoDeJogo)
            {
                ReiniciarFuga();
                if (vidaKraken) vidaKraken.RestaurarVida(vidaKraken.vidaMaxima);
                if (animator) { animator.Rebind(); animator.Update(0f); }
            }
        }
        finally
        {
            if (animator) animator.updateMode = modoAnterior;
            if (movimento) movimento.enabled = movimentoAtivo;
            if (inventario) inventario.enabled = inventarioAtivo;
            Time.timeScale = escalaAnterior;
            animandoMorteFuga = false;
        }
    }

    void ReiniciarFuga()
    {
        var capsula = movimento.GetComponent<CharacterController>();
        bool estavaAtiva = capsula != null && capsula.enabled;
        if (capsula != null) capsula.enabled = false;
        movimento.transform.SetPositionAndRotation(pontoSeguro, Quaternion.Euler(0f, direcaoSegura, 0f));
        Physics.SyncTransforms();
        if (capsula != null) capsula.enabled = estavaAtiva;
        movimento.PrepararTrocaDeMundo(Camera.main);
        foreach (var camera in FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsSortMode.None))
            camera.PreviousStateIsValid = false;
        if (fantasmas != null) fantasmas.ReiniciarTentativaDeFuga();
        tempoFuga = DuracaoFuga;
    }

    void CriarCronometro()
    {
        hudFuga = new GameObject("Cronometro da fuga", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        hudFuga.transform.SetParent(transform, false);
        var canvas = hudFuga.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;
        var scaler = hudFuga.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        var go = new GameObject("Tempo restante", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(hudFuga.transform, false);
        textoFuga = go.GetComponent<TextMeshProUGUI>();
        var indicador = movimento.GetComponent<IndicadorItemEndy>();
        if (indicador != null && indicador.fonte != null) textoFuga.font = indicador.fonte;
        textoFuga.fontSize = 36;
        textoFuga.fontStyle = FontStyles.Bold;
        textoFuga.alignment = TextAlignmentOptions.Center;
        textoFuga.outlineColor = new Color(.08f, .04f, .02f);
        textoFuga.outlineWidth = .2f;
        textoFuga.raycastTarget = false;
        var rect = textoFuga.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1f);
        rect.anchoredPosition = new Vector2(0, -24);
        rect.sizeDelta = new Vector2(700, 150);
    }
}


