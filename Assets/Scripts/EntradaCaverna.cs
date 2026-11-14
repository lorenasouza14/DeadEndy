using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed class EntradaCaverna : MonoBehaviour
{
    [SerializeField] string cenaDestino = "Level_Cave_3";
    bool carregando;

    void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        var corpo = GetComponent<Rigidbody>();
        corpo.isKinematic = true;
        corpo.useGravity = false;
    }

    void OnTriggerEnter(Collider outro)
    {
        if (carregando) return;
        var jogador = outro.GetComponentInParent<StarterAssets.ThirdPersonController>();
        if (jogador == null || !jogador.CompareTag("Player") || !jogador.isActiveAndEnabled) return;
        var vida = jogador.GetComponent<VidaEndy>();
        if (vida != null && vida.EstaMorto) return;
        if (!Application.CanStreamedLevelBeLoaded(cenaDestino))
        {
            Debug.LogError("A cena da caverna não está disponível: " + cenaDestino, this);
            return;
        }

        carregando = true;
        // A caverna já possui seu Endy, câmera e PontoEntrada.
        // O modo Single descarrega a fase anterior e evita duplicar o jogador.
        var operacao = SceneManager.LoadSceneAsync(cenaDestino, LoadSceneMode.Single);
        if (operacao == null) { carregando = false; return; }
        jogador.enabled = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
