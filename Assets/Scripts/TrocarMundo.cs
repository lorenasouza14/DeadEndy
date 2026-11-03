using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
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
        if (instancia != this || trocando) return;
#if ENABLE_INPUT_SYSTEM
        bool pressionou = Keyboard.current != null && Keyboard.current.zKey.wasPressedThisFrame;
#else
        bool pressionou = Input.GetKeyDown(KeyCode.Z);
#endif
        if (pressionou) TrocarCena();
    }

    public void TrocarCena()
    {
        if (instancia != this || trocando) return;
        string atual = SceneManager.GetActiveScene().name;
        string destino = atual == mundo1 ? mundo2 : atual == mundo2 ? mundo1 : null;
        if (destino == null) return;
        if (!Application.CanStreamedLevelBeLoaded(destino))
        {
            Debug.LogError("Adicione a cena " + destino + " na Scene List do Build Profiles.", this);
            return;
        }
        trocando = true;
        SceneManager.LoadScene(destino, LoadSceneMode.Single);
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
        bool estavaAtiva = capsula.enabled;
        capsula.enabled = false;
        transform.SetPositionAndRotation(entrada.position, Quaternion.Euler(0f, entrada.eulerAngles.y, 0f));
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
