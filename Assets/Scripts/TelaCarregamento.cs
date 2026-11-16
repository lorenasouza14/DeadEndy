using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public sealed class TelaCarregamento : MonoBehaviour
{
    static TelaCarregamento atual;
    public static bool EmCurso => atual != null;
    Canvas canvas;
    VideoPlayer video;
    RenderTexture textura;
    RawImage imagem;
    Texture2D inicio;
    float tempoExibido;
    double ultimoAvanco;
    long ultimoQuadro = -1;
    const int Colunas = 6, Linhas = 6, QuadrosPorSegundo = 12;
    const float DuracaoInicio = (float)(Colunas * Linhas) / QuadrosPorSegundo;
    bool falhaNoVideo;
    float escalaAnterior;
    UnityEngine.ThreadPriority prioridadeAnterior;

    public static TelaCarregamento Preparar(Transform portal, VideoClip clipe)
    {
        var tela = new GameObject("Tela de carregamento", typeof(RectTransform), typeof(Canvas));
        tela.transform.SetParent(portal, false);
        var carregamento = tela.AddComponent<TelaCarregamento>();
        carregamento.Configurar(clipe);
        return carregamento;
    }

    void Configurar(VideoClip clipe)
    {
        canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        canvas.enabled = false;
        gameObject.AddComponent<Image>().color = Color.black;
        var quadro = new GameObject("Vídeo", typeof(RectTransform), typeof(RawImage));
        quadro.transform.SetParent(transform, false);
        imagem = quadro.GetComponent<RawImage>();
        inicio = Resources.Load<Texture2D>("CarregamentoInicio");
        var aspecto = quadro.AddComponent<AspectRatioFitter>();
        aspecto.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        aspecto.aspectRatio = 16f / 9f;
        video = quadro.AddComponent<VideoPlayer>();
        video.playOnAwake = false;
        video.isLooping = true;
        video.waitForFirstFrame = true;
        video.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        video.renderMode = VideoRenderMode.RenderTexture;
        video.clip = clipe;
        if (clipe == null) return;

        textura = new RenderTexture((int)clipe.width, (int)clipe.height, 0, RenderTextureFormat.ARGB32);
        textura.Create();
        video.targetTexture = textura;
        imagem.texture = textura;

        video.errorReceived += AoFalharVideo;
        video.Prepare();
    }

    public bool Iniciar(string destino)
    {
        if (EmCurso) return false;
        if (!Application.CanStreamedLevelBeLoaded(destino))
        {
            Debug.LogError("Cena indisponível para carregamento: " + destino);
            return false;
        }
        atual = this;
        transform.SetParent(null, false);
        DontDestroyOnLoad(gameObject);
        escalaAnterior = Time.timeScale;
        prioridadeAnterior = Application.backgroundLoadingPriority;
        Time.timeScale = 0f;
        Application.backgroundLoadingPriority = UnityEngine.ThreadPriority.Low;
        StartCoroutine(Carregar(destino));
        return true;
    }

    IEnumerator Carregar(string destino)
    {
        canvas.enabled = true;
        MostrarQuadroInicial();
        if (video.clip != null && !falhaNoVideo) video.Play();
        while (tempoExibido < .5f) yield return null;
        var carga = SceneManager.LoadSceneAsync(destino, LoadSceneMode.Single);
        carga.allowSceneActivation = false;
        while (carga.progress < .9f || tempoExibido < DuracaoInicio) yield return null;
        carga.allowSceneActivation = true;
        yield return carga;
        var piso = Object.FindAnyObjectByType<PisoSuaveCaverna>();
        while (piso != null && !piso.Pronto) yield return null;
        yield return null;
        Destroy(gameObject);
    }

    void Update()
    {
        if (atual != this) return;
        // Um quadro longo de integração da cena não conta como segundos de animação exibida.
        tempoExibido += Mathf.Min(Time.unscaledDeltaTime, 1f / QuadrosPorSegundo);
        if (video.frame != ultimoQuadro)
        {
            ultimoQuadro = video.frame;
            ultimoAvanco = Time.realtimeSinceStartupAsDouble;
        }
        if (tempoExibido >= DuracaoInicio && !falhaNoVideo && ultimoQuadro > 0 &&
            Time.realtimeSinceStartupAsDouble - ultimoAvanco < .35)
        {
            imagem.texture = textura;
            imagem.uvRect = new Rect(0, 0, 1, 1);
        }
        else MostrarQuadroInicial();
    }

    void MostrarQuadroInicial()
    {
        int quadro = Mathf.FloorToInt(tempoExibido * QuadrosPorSegundo) % (Colunas * Linhas);
        imagem.texture = inicio;
        imagem.uvRect = new Rect((float)(quadro % Colunas) / Colunas,
            (float)(Linhas - 1 - quadro / Colunas) / Linhas, 1f / Colunas, 1f / Linhas);
    }

    void AoFalharVideo(VideoPlayer fonte, string erro)
    {
        falhaNoVideo = true;
        Debug.LogWarning("Falha no vídeo de carregamento: " + erro, this);
    }

    void OnDestroy()
    {
        if (video != null)
        {

            video.errorReceived -= AoFalharVideo;
            video.targetTexture = null;
        }
        if (textura != null) { textura.Release(); Destroy(textura); }
        if (atual != this) return;
        Time.timeScale = escalaAnterior;
        Application.backgroundLoadingPriority = prioridadeAnterior;
        atual = null;
    }
}
