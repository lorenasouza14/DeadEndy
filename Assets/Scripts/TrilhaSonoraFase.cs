using UnityEngine;
using UnityEngine.SceneManagement;

// Vive na cena: sair da fase destroi a fonte e encerra a musica.
[DisallowMultipleComponent]
public class TrilhaSonoraFase : MonoBehaviour
{
    [Range(0f, 1f)] public float volume = .15f;
    [Min(.1f)] public float duracaoEntrada = 2f;
    AudioSource fonte;
    AudioSource desmoronamento;
    BatalhaKraken batalha;
    EscolhaTesouroFinal escolha;
    bool faseFinal, iniciada, pausada;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Registrar()
    {
        SceneManager.sceneLoaded -= AoCarregar;
        SceneManager.sceneLoaded += AoCarregar;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InstalarNasCenasAbertas()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            AoCarregar(SceneManager.GetSceneAt(i), LoadSceneMode.Additive);
    }

    static void AoCarregar(Scene cena, LoadSceneMode modo)
    {
        if (cena.path != ProgressoJogo.CenaCaverna && cena.name != "Level_Kraken_4") return;
        foreach (var raiz in cena.GetRootGameObjects())
            if (raiz.GetComponentInChildren<TrilhaSonoraFase>(true)) return;
        var objeto = new GameObject("Trilha sonora da fase");
        SceneManager.MoveGameObjectToScene(objeto, cena);
        objeto.AddComponent<TrilhaSonoraFase>();
    }

    void Awake()
    {
        faseFinal = gameObject.scene.name == "Level_Kraken_4";
        fonte = gameObject.AddComponent<AudioSource>();
        fonte.playOnAwake = false;
        fonte.loop = true;
        fonte.spatialBlend = 0f;
        fonte.volume = 0f;
        string nome = faseFinal ? "TrilhaKraken" : "TrilhaCaverna";
        fonte.clip = Resources.Load<AudioClip>("EfeitosSonoros/" + nome);
        if (!fonte.clip) Debug.LogWarning("Trilha sonora ausente: " + nome, this);
        if (faseFinal)
        {
            desmoronamento = gameObject.AddComponent<AudioSource>();
            desmoronamento.playOnAwake = false;
            desmoronamento.loop = true;
            desmoronamento.spatialBlend = 0f;
            desmoronamento.volume = .12f;
            desmoronamento.clip = EfeitosSonoros.Carregar("Desmoronamento");
        }
    }

    void Update()
    {
        if (faseFinal)
        {
            if (!batalha) batalha = FindAnyObjectByType<BatalhaKraken>();
            if (!escolha && batalha) escolha = batalha.GetComponent<EscolhaTesouroFinal>();
        }

        // A vitoria sobre o Kraken ainda deixa o parkour para completar.
        // A escolha final encerra a trilha; voltar pelo bau permite inicia-la novamente.
        bool terminou = escolha && !string.IsNullOrEmpty(escolha.Escolha);
        if (terminou || TelaCarregamento.EmCurso || MenuManager.EstaAberto ||
            gameObject.scene != SceneManager.GetActiveScene())
        {
            Parar();
            return;
        }
        if (Time.timeScale <= 0f || AudioListener.pause ||
            (faseFinal && (!batalha || batalha.EmIntroducao)) ||
            FindAnyObjectByType<CutsceneInicio>() != null)
        {
            if (iniciada && !pausada) { fonte.Pause(); pausada = true; }
            if (desmoronamento) desmoronamento.Pause();
            return;
        }
        if (desmoronamento && desmoronamento.clip)
        {
            if (batalha && batalha.Estado == BatalhaKraken.Etapa.Derrotado)
            {
                desmoronamento.UnPause();
                if (!desmoronamento.isPlaying) desmoronamento.Play();
            }
            else desmoronamento.Stop();
        }
        if (!fonte.clip) return;
        if (!iniciada)
        {
            fonte.Play();
            iniciada = true;
        }
        else if (pausada) fonte.UnPause();
        pausada = false;
        float volumeAlvo = faseFinal ? volume * .5f : volume;
        fonte.volume = Mathf.MoveTowards(fonte.volume, volumeAlvo,
            volumeAlvo * Time.unscaledDeltaTime / Mathf.Max(.1f, duracaoEntrada));
    }

    void Parar()
    {
        if (fonte) { fonte.Stop(); fonte.volume = 0f; }
        if (desmoronamento) desmoronamento.Stop();
        iniciada = pausada = false;
    }

    void OnDisable() { Parar(); }
}
