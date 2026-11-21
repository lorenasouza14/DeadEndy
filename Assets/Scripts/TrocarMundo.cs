using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(-200), DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class TrocarMundo : MonoBehaviour
{
    private static TrocarMundo instancia;
    private const string mundo1 = "Level_Cave_3";
    private const string mundo2 = "Level_Kraken_4";
    private Transform alvoCamera;
    private StarterAssets.ThirdPersonController controlador;
    private CharacterController capsula;
    private bool trocando;
    private Scene cenaPreparada;
    private AutosaveCaverna autosaveCaverna;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ReiniciarInstancia() { instancia = null; }

    private void Awake()
    {
        if (instancia != null && instancia != this)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }
        if (transform.parent != null)
        {
            Debug.LogError("TrocarMundo deve ficar no PlayerArmature, na raiz da cena.", this);
            enabled = false;
            return;
        }
        instancia = this;
        controlador = GetComponent<StarterAssets.ThirdPersonController>();
        capsula = GetComponent<CharacterController>();
        alvoCamera = controlador != null && controlador.CinemachineCameraTarget != null
            ? controlador.CinemachineCameraTarget.transform : transform.Find("PlayerCameraRoot");
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += AoCarregarCena;
    }

    private void Start()
    {
        if (instancia == this) PrepararCena(SceneManager.GetActiveScene());
    }

    private void Update()
    {
        if (instancia != this || trocando || Time.timeScale == 0) return;
#if ENABLE_INPUT_SYSTEM
        bool pressionou = Keyboard.current != null && Keyboard.current.zKey.wasPressedThisFrame;
#else
        bool pressionou = Input.GetKeyDown(KeyCode.Z);
#endif
        if (pressionou) TrocarCena();
    }

    public void TrocarCena()
    {
        if (instancia != this || trocando || TelaCarregamento.EmCurso) return;
        var vida = GetComponent<VidaJogadorKraken>();
        if (vida && vida.Morto) return;
        string atual = SceneManager.GetActiveScene().name;
        if (atual == mundo1)
        {
            var porta = FindAnyObjectByType<PortaPuzzleCaverna>();
            if (porta != null) { porta.IniciarTravessia(this); return; }
        }
        string destino = atual == mundo1 ? mundo2 : atual == mundo2 ? mundo1 : null;
        if (destino == null) return;
        if (!Application.CanStreamedLevelBeLoaded(destino))
        {
            Debug.LogError("Adicione a cena " + destino + " na Scene List do Build Profiles.", this);
            return;
        }
        trocando = true;
        if (autosaveCaverna != null) autosaveCaverna.TentarSalvar();
        SceneManager.LoadScene(destino, LoadSceneMode.Single);
    }

    public void PassarPelaPorta(TelaCarregamento tela)
    {
        if (instancia != this || trocando || SceneManager.GetActiveScene().name != mundo1) return;
        if (TryGetComponent<VidaJogadorKraken>(out var vida) && vida.Morto) return;
        if (!tela.Iniciar(mundo2)) return;
        trocando = true;
        if (autosaveCaverna != null) autosaveCaverna.TentarSalvar();
    }

    private void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        if (instancia == this && (cena.name == mundo1 || cena.name == mundo2)) PrepararCena(cena);
    }

    private void PrepararCena(Scene cena)
    {
        if (cenaPreparada == cena) return;
        Transform entrada = null;
        foreach (GameObject raiz in cena.GetRootGameObjects())
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
                if (t.name == "PontoEntrada" && t.gameObject.activeInHierarchy)
                {
                    if (entrada != null) { Falhar("Mais de um PontoEntrada ativo em " + cena.name); return; }
                    entrada = t;
                }
        if (entrada == null) { Falhar("Crie um PontoEntrada ativo sobre o chao de " + cena.name); return; }
        var destino = new Pose(entrada.position, Quaternion.Euler(0, entrada.eulerAngles.y, 0));
        var vida = GetComponent<VidaJogadorKraken>();
        if (vida == null) vida = gameObject.AddComponent<VidaJogadorKraken>();
        if (vida.TentarObterCheckpointVitoria(cena, out var checkpointVitoria)) destino = checkpointVitoria;
        if (!MenuManager.EstaAberto) GerenciadorPause.Instalar(vida, cena);
        if (cena.path == ProgressoJogo.CenaCaverna && !MenuManager.EstaAberto && cena == SceneManager.GetActiveScene())
        {
            PisoSuaveCaverna.Garantir(cena, capsula);
            var perigos = entrada.gameObject.AddComponent<PerigosContatoCaverna>();
            perigos.Preparar(capsula, vida);
            autosaveCaverna = entrada.gameObject.AddComponent<AutosaveCaverna>();
            destino = autosaveCaverna.Preparar(controlador, destino);
        }
        bool estavaAtiva = capsula.enabled;
        capsula.enabled = false;
        transform.SetPositionAndRotation(destino.position, destino.rotation);
        Physics.SyncTransforms();
        capsula.enabled = estavaAtiva;
        if (controlador != null) controlador.PrepararTrocaDeMundo(Camera.main);
        foreach (GameObject raiz in cena.GetRootGameObjects())
            foreach (CinemachineCamera camera in raiz.GetComponentsInChildren<CinemachineCamera>(true))
                if (camera.name == "PlayerFollowCamera")
                {
                    camera.Follow = alvoCamera;
                    camera.LookAt = alvoCamera;
                    camera.PreviousStateIsValid = false;
                }
        var diario = GetComponent<DiarioEndy>();
        if (diario != null)
        {
            diario.AtualizarEtapa();
            BloqueioLeituraKraken.Preparar(diario);
        }
        // Ao continuar pelo menu, o jogador persistente pode nao receber Start novamente.
        if (!MenuManager.EstaAberto && !vida.Morto && (diario == null || !diario.Aberto))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            var inputs = GetComponent<StarterAssets.StarterAssetsInputs>();
            if (inputs != null) inputs.cursorLocked = inputs.cursorInputForLook = true;
        }
        cenaPreparada = cena;
        trocando = false;
    }

    private void Falhar(string mensagem)
    {
        Debug.LogError(mensagem + ". Movimento suspenso para evitar cair no vazio.", this);
        if (controlador != null) controlador.enabled = false;
        trocando = false;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= AoCarregarCena;
        if (instancia == this) instancia = null;
    }
}
