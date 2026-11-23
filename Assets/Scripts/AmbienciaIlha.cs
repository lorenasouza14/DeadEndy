using StarterAssets;
using UnityEngine;
using UnityEngine.SceneManagement;

// Acompanha o piso da ilha, independentemente da etapa do diario ou do chefe.
public class AmbienciaIlha : MonoBehaviour
{
    [Range(0f, 1f)] public float volumePraia = .12f;
    [Range(0f, 1f)] public float volumeFloresta = .10f;
    [Min(.1f)] public float duracaoTransicao = 2.5f;

    AudioSource praia, floresta;
    ThirdPersonController jogador;
    CharacterController capsula;
    bool naFloresta, suspenso, videoEmCurso;
    float proximaConsulta;

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
        if (!ProgressoJogo.EhCenaIlha(cena.path) && cena.name != "Demo_day_fase_gorilla") return;
        foreach (var raiz in cena.GetRootGameObjects())
            if (raiz.GetComponentInChildren<AmbienciaIlha>(true)) return;
        var objeto = new GameObject("Ambiencia praia e floresta");
        SceneManager.MoveGameObjectToScene(objeto, cena);
        objeto.AddComponent<AmbienciaIlha>();
    }

    void Awake()
    {
        naFloresta = gameObject.scene.name == "Demo_day_fase_gorilla";
        praia = CriarFonte("AmbientePraia");
        floresta = CriarFonte("AmbienteFloresta");
    }

    AudioSource CriarFonte(string nome)
    {
        var fonte = gameObject.AddComponent<AudioSource>();
        fonte.playOnAwake = false;
        fonte.loop = true;
        fonte.spatialBlend = 0f;
        fonte.volume = 0f;
        fonte.clip = Resources.Load<AudioClip>("EfeitosSonoros/" + nome);
        if (!fonte.clip) Debug.LogWarning("Audio de ambiente ausente: " + nome, this);
        return fonte;
    }

    void Update()
    {
        if (Time.unscaledTime >= proximaConsulta)
        {
            proximaConsulta = Time.unscaledTime + .25f;
            if (!jogador)
            {
                jogador = FindAnyObjectByType<ThirdPersonController>();
                capsula = jogador ? jogador.GetComponent<CharacterController>() : null;
            }
            videoEmCurso = FindAnyObjectByType<CutsceneInicio>() != null;
            if (jogador && capsula && capsula.enabled) AtualizarRegiao();
        }

        bool pausar = Time.timeScale <= 0f || AudioListener.pause;
        if (pausar)
        {
            if (!suspenso) { praia.Pause(); floresta.Pause(); }
            suspenso = true;
            return;
        }
        if (suspenso)
        {
            praia.UnPause(); floresta.UnPause();
            suspenso = false;
        }
        bool tocar = jogador && jogador.isActiveAndEnabled &&
            gameObject.scene == SceneManager.GetActiveScene() &&
            !MenuManager.EstaAberto && !TelaCarregamento.EmCurso && !videoEmCurso;
        AtualizarFonte(praia, tocar && !naFloresta ? volumePraia : 0f, volumePraia);
        AtualizarFonte(floresta, tocar && naFloresta ? volumeFloresta : 0f, volumeFloresta);
    }

    void AtualizarFonte(AudioSource fonte, float alvo, float volumeMaximo)
    {
        fonte.volume = Mathf.MoveTowards(fonte.volume, alvo,
            Mathf.Max(.01f, volumeMaximo) * Time.unscaledDeltaTime / Mathf.Max(.1f, duracaoTransicao));
        if (fonte.volume > 0f && fonte.clip && !fonte.isPlaying) fonte.Play();
        else if (fonte.volume == 0f && fonte.isPlaying) fonte.Stop();
    }

    void AtualizarRegiao()
    {
        // Consulta o Terrain mesmo sobre pedras/pontes e durante saltos.
        // Areia e grama indicam o ambiente; rocha pura preserva o ambiente anterior.
        Vector3 ponto = capsula.bounds.center;
        Terrain terreno = null;
        float alturaMaisProxima = float.NegativeInfinity;
        foreach (var candidato in Terrain.activeTerrains)
        {
            if (candidato.gameObject.scene != gameObject.scene || !candidato.terrainData) continue;
            Vector3 local = ponto - candidato.transform.position;
            Vector3 tamanho = candidato.terrainData.size;
            if (local.x < 0 || local.z < 0 || local.x > tamanho.x || local.z > tamanho.z) continue;
            float altura = candidato.SampleHeight(ponto) + candidato.transform.position.y;
            if (altura > ponto.y + 1f || altura <= alturaMaisProxima) continue;
            terreno = candidato;
            alturaMaisProxima = altura;
        }
        if (terreno)
        {
            var dados = terreno.terrainData;
            Vector3 local = ponto - terreno.transform.position;
            int x = Mathf.Clamp(Mathf.FloorToInt(local.x / dados.size.x * dados.alphamapWidth), 0, dados.alphamapWidth - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(local.z / dados.size.z * dados.alphamapHeight), 0, dados.alphamapHeight - 1);
            var pesos = dados.GetAlphamaps(x, z, 1, 1);
            var camadas = dados.terrainLayers;
            float areia = 0f, grama = 0f;
            for (int i = 0; i < camadas.Length && i < pesos.GetLength(2); i++)
            {
                if (!camadas[i]) continue;
                string tipo = SonsPassosEndy.Classificar(camadas[i].name);
                if (tipo == "Areia") areia += pesos[0, 0, i];
                else if (tipo == "Grama") grama += pesos[0, 0, i];
            }
            // Margem para nao oscilar a cada passo na borda entre areia e grama.
            if (areia > .2f && areia > grama + .15f) naFloresta = false;
            else if (grama > .2f && grama > areia + .15f) naFloresta = true;
            return;
        }

        // Tambem atende pisos de malha fora dos limites do Terrain.
        Vector3 pe = new Vector3(ponto.x, capsula.bounds.min.y, ponto.z);
        RaycastHit chao = default;
        float distancia = float.PositiveInfinity;
        foreach (var hit in Physics.RaycastAll(pe + Vector3.up * .3f, Vector3.down, 2f,
            jogador.GroundLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(jogador.transform) || hit.normal.y < .5f || hit.distance >= distancia) continue;
            chao = hit;
            distancia = hit.distance;
        }
        if (!chao.collider) return;
        string superficie = SonsPassosEndy.Identificar(chao);
        if (superficie == "Areia") naFloresta = false;
        else if (superficie == "Grama") naFloresta = true;
    }

    void OnDisable()
    {
        if (praia) { praia.Stop(); praia.volume = 0f; }
        if (floresta) { floresta.Stop(); floresta.volume = 0f; }
        suspenso = false;
    }
}

// Instalado em cada fase; limita apenas malhas de cenario sem scripts de gameplay.
public sealed class RenderizacaoCenarioPorDistancia : MonoBehaviour
{
    const float Distancia = 180f;
    readonly System.Collections.Generic.List<MeshRenderer> malhas = new();
    readonly System.Collections.Generic.List<bool> estadosOriginais = new();
    CullingGroup grupo;
    ThirdPersonController jogador;
    bool suspenso = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Registrar()
    {
        SceneManager.sceneLoaded -= Instalar;
        SceneManager.sceneLoaded += Instalar;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InstalarAbertas()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            Instalar(SceneManager.GetSceneAt(i), LoadSceneMode.Additive);
    }

    static void Instalar(Scene cena, LoadSceneMode modo)
    {
        if (!ProgressoJogo.EhCenaJogo(cena.path) && cena.name != "Level_Kraken_4") return;
        foreach (var raiz in cena.GetRootGameObjects())
            if (raiz.GetComponent<RenderizacaoCenarioPorDistancia>()) return;
        var objeto = new GameObject("Renderizacao do cenario por distancia");
        SceneManager.MoveGameObjectToScene(objeto, cena);
        objeto.AddComponent<RenderizacaoCenarioPorDistancia>();
    }

    void Start()
    {
        var esferas = new System.Collections.Generic.List<BoundingSphere>();
        foreach (var raiz in gameObject.scene.GetRootGameObjects())
        foreach (var malha in raiz.GetComponentsInChildren<MeshRenderer>(true))
        {
            // Mantem personagens, puzzles, plataformas moveis, agua e paisagens grandes.
            if (malha.GetComponentInParent<MonoBehaviour>() || malha.GetComponentInParent<Rigidbody>() ||
                malha.GetComponentInParent<Animator>() || malha.GetComponentInParent<Canvas>() ||
                malha.gameObject.layer == LayerMask.NameToLayer("Water") ||
                malha.name.ToLowerInvariant().Contains("water") ||
                malha.bounds.extents.magnitude > 150f) continue;
            malhas.Add(malha);
            estadosOriginais.Add(malha.forceRenderingOff);
            esferas.Add(new BoundingSphere(malha.bounds.center, malha.bounds.extents.magnitude));
        }
        grupo = new CullingGroup();
        grupo.SetBoundingSpheres(esferas.ToArray());
        grupo.SetBoundingSphereCount(esferas.Count);
        grupo.SetBoundingDistances(new[] { Distancia });
        grupo.onStateChanged = MudouDistancia;
    }

    void LateUpdate()
    {
        if (grupo == null) return;
        if (!jogador) jogador = FindAnyObjectByType<ThirdPersonController>();
        var camera = Camera.main;
        bool pausar = !jogador || !camera || MenuManager.EstaAberto ||
            TelaCarregamento.EmCurso || gameObject.scene != SceneManager.GetActiveScene() ||
            FindAnyObjectByType<CutsceneInicio>() != null;
        grupo.targetCamera = camera;
        if (jogador) grupo.SetDistanceReferencePoint(jogador.transform);
        if (pausar == suspenso) return;
        suspenso = pausar;
        for (int i = 0; i < malhas.Count; i++)
            if (malhas[i]) malhas[i].forceRenderingOff = estadosOriginais[i] ||
                (!suspenso && grupo.GetDistance(i) > 0);
    }

    void MudouDistancia(CullingGroupEvent evento)
    {
        if (!suspenso && malhas[evento.index])
            malhas[evento.index].forceRenderingOff = estadosOriginais[evento.index] || evento.currentDistance > 0;
    }

    void OnDisable()
    {
        suspenso = true;
        for (int i = 0; i < malhas.Count; i++)
            if (malhas[i]) malhas[i].forceRenderingOff = estadosOriginais[i];
    }

    void OnDestroy() { grupo?.Dispose(); }
}
