using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CutsceneInicio : MonoBehaviour
{
    VideoPlayer video;
    RawImage imagem;
    Image preto;
    RenderTexture textura;
    bool terminou;
    AudioListener listenerTemporario;
    public static void Iniciar(string cena)
    {
        var go = new GameObject("Cutscene inicial");
        DontDestroyOnLoad(go);
        go.AddComponent<CutsceneInicio>().StartCoroutine(go.GetComponent<CutsceneInicio>().Reproduzir(cena));
    }
    public static void IniciarCreditos(System.Action concluir)
    {
        var go = new GameObject("Creditos do menu");
        DontDestroyOnLoad(go);
        var player = go.AddComponent<CutsceneInicio>();
        player.StartCoroutine(player.Reproduzir(null, "Creditos.mp4", aposTransicao: concluir));
    }

    public static void IniciarEntradaKraken(System.Action concluir)
    {
        var go = new GameObject("Cutscene entrada Kraken");
        DontDestroyOnLoad(go);
        var player = go.AddComponent<CutsceneInicio>();
        player.StartCoroutine(player.Reproduzir(null, "KrakenAparecendo.mp4", null, true, concluir));
    }

    public static void IniciarPortaCaverna(System.Action concluir)
    {
        var go = new GameObject("Cutscene porta caverna");
        DontDestroyOnLoad(go);
        var player = go.AddComponent<CutsceneInicio>();
        player.StartCoroutine(player.Reproduzir(null, "PortaCaverna.mp4", null, true, concluir));
    }

    public static void IniciarDesmoronamento(System.Action concluir)
    {
        var go = new GameObject("Cutscene desmoronamento Kraken");
        DontDestroyOnLoad(go);
        var player = go.AddComponent<CutsceneInicio>();
        player.StartCoroutine(player.Reproduzir(null, "KrakenDesmoronamento.mp4", concluir, true));
    }

    public static void IniciarFinal(bool quadro, System.Action retornar)
    {
        var go = new GameObject("Cutscene final");
        DontDestroyOnLoad(go);
        var player = go.AddComponent<CutsceneInicio>();
        player.StartCoroutine(player.Reproduzir(quadro ? "MenuPrincipal" : null,
            quadro ? "FinalQuadro.mp4" : "FinalBau.mp4", retornar, true,
            encerrarJogo: quadro, videoSeguinte: quadro ? null : "VideoFinal2.mp4"));
    }
    IEnumerator Reproduzir(string cena, string arquivo = "Inicio.mp4", System.Action retornar = null, bool final = false, System.Action aposTransicao = null, bool encerrarJogo = false, string videoSeguinte = null)
    {
        float tempoAnterior = Time.timeScale;
        if (final) Time.timeScale = 0;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;
        gameObject.AddComponent<GraphicRaycaster>();
        preto = Criar<Image>("Fundo"); preto.color = Color.black;
        imagem = Criar<RawImage>("Video"); imagem.color = Color.black;
        var audio = gameObject.AddComponent<AudioSource>();
        audio.ignoreListenerPause = true;
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;
        audio.volume = 1f;
        audio.mute = false;
        bool temListener = false;
        foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            if (listener.isActiveAndEnabled) { temListener = true; break; }
        if (!temListener) listenerTemporario = gameObject.AddComponent<AudioListener>();
        video = gameObject.AddComponent<VideoPlayer>();
        video.playOnAwake = false;
        video.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        video.source = VideoSource.Url;
        video.url = Path.Combine(Application.streamingAssetsPath, "Cutscenes", arquivo);
        video.renderMode = VideoRenderMode.RenderTexture;
        video.aspectRatio = VideoAspectRatio.FitInside;
        video.audioOutputMode = VideoAudioOutputMode.AudioSource;
        video.controlledAudioTrackCount = 1;
        video.EnableAudioTrack(0, true);
        video.SetTargetAudioSource(0, audio);
        textura = new RenderTexture(1920,1080,0);
        video.targetTexture = textura; imagem.texture = textura;
        video.loopPointReached += AoTerminar;
        video.errorReceived += AoErro;
        video.isLooping = false;
        bool temVideoSeguinte = !string.IsNullOrEmpty(videoSeguinte);
        yield return ReproduzirVideo(arquivo, audio, final, false,
            temVideoSeguinte ? 1.2f : 0.6f, temVideoSeguinte);
        if (temVideoSeguinte)
            yield return ReproduzirVideo(videoSeguinte, audio, true, true, 1.2f, true);
        if (encerrarJogo)
        {
            yield return ReproduzirVideo("TheEndy.mp4", audio, true, true);
            yield return ReproduzirVideo("Creditos.mp4", audio, true, true);
            ProgressoJogo.MarcarConcluido();
        }
        if (listenerTemporario != null) listenerTemporario.enabled = false;
        if (!string.IsNullOrEmpty(cena))
        {
            Time.timeScale = 1;
            var carga = SceneManager.LoadSceneAsync(cena, LoadSceneMode.Single);
            while (!carga.isDone) yield return null;
        }
        else
        {
            retornar?.Invoke();
            Time.timeScale = tempoAnterior;
        }
        yield return null;
        imagem.enabled=false;
        for(float t=0;t<1.2f;t+=Time.unscaledDeltaTime)
        {
            preto.color=new Color(0,0,0,1-t/1.2f);
            yield return null;
        }
        aposTransicao?.Invoke();
        Destroy(gameObject);
    }
    IEnumerator ReproduzirVideo(string arquivo, AudioSource audio, bool final, bool esmaecerEntrada,
        float duracaoTransicao = 0.6f, bool anteciparSaida = false)
    {
        terminou = false;
        imagem.color = Color.black;
        audio.volume = esmaecerEntrada ? 0f : 1f;
        video.url = Path.Combine(Application.streamingAssetsPath, "Cutscenes", arquivo);
        video.Prepare();
        float prazo = Time.realtimeSinceStartup + 30;
        while (!video.isPrepared && !terminou && Time.realtimeSinceStartup < prazo)
        {
            if (!final && Pular()) terminou = true;
            yield return null;
        }
        if (video.isPrepared && !terminou)
        {
            video.Play();
            if (esmaecerEntrada)
            {
                for (float t = 0; t < duracaoTransicao && !terminou; t += Time.unscaledDeltaTime)
                {
                    float a = Mathf.Clamp01(t / duracaoTransicao);
                    imagem.color = new Color(a, a, a, 1);
                    audio.volume = a;
                    yield return null;
                }
            }
            imagem.color = Color.white;
            audio.volume = 1f;
            while (!terminou)
            {
                if (!final && Pular()) terminou = true;
                // Escurece os ultimos quadros enquanto ainda ha imagem e audio,
                // em vez de esperar um eventual quadro preto no fim do arquivo.
                if (anteciparSaida && video.length > 0 &&
                    video.time >= System.Math.Max(0, video.length - duracaoTransicao)) break;
                yield return null;
            }
        }
        float volume = audio.volume;
        for (float t=0;t<duracaoTransicao;t+=Time.unscaledDeltaTime)
        {
            float a=1-t/duracaoTransicao;
            imagem.color=new Color(a,a,a,1); audio.volume=volume*a;
            yield return null;
        }
        imagem.color=Color.black; video.Stop();
    }

    T Criar<T>(string nome) where T : Graphic
    {
        var go=new GameObject(nome,typeof(RectTransform)); go.transform.SetParent(transform,false);
        var r=go.GetComponent<RectTransform>(); r.anchorMin=Vector2.zero; r.anchorMax=Vector2.one;
        r.offsetMin=r.offsetMax=Vector2.zero;
        return go.AddComponent<T>();
    }
    bool Pular()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Space);
#endif
    }
    void AoTerminar(VideoPlayer p) { terminou=true; }
    void AoErro(VideoPlayer p,string erro) { Debug.LogWarning("Cutscene inicial: "+erro); terminou=true; }
    void OnDestroy()
    {
        if(video!=null) { video.Stop(); video.targetTexture=null; }
        if(textura!=null) { textura.Release(); Destroy(textura); }
    }
}






