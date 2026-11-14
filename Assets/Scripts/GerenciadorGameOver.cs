using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GerenciadorGameOver : MonoBehaviour
{
    private bool reiniciando;

    public static void MostrarNaFase()
    {
        var tela = Instantiate(Resources.Load<GerenciadorGameOver>("TelaGameOver"));
        GerenciadorPause.GarantirEventSystem(tela.transform);
        EventSystem.current.SetSelectedGameObject(tela.GetComponentInChildren<Button>().gameObject);
        Time.timeScale = 0f;
    }

    void OnEnable()
    {
        reiniciando = false;
        SoltarRatoParaInterface();
    }

    public void ReiniciarJogo()
    {
        if (reiniciando) return;
        reiniciando = true;
        if (ProgressoJogo.EhCenaIlha(gameObject.scene.path)) ProgressoJogo.PrepararContinuar(out _);
        Debug.Log("Reiniciando a batalha...");
        Time.timeScale = 1f;

        // Devolve o controle ao Endy ANTES de recarregar a cena
        TravarRatoNoJogo();

        SceneManager.LoadScene(gameObject.scene.path);
    }

    void TravarRatoNoJogo()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        StarterAssets.StarterAssetsInputs inputs = FindAnyObjectByType<StarterAssets.StarterAssetsInputs>();
        if (inputs != null)
        {
            inputs.cursorLocked = true;
            inputs.cursorInputForLook = true;
        }
    }

    void SoltarRatoParaInterface()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        StarterAssets.StarterAssetsInputs inputs = FindAnyObjectByType<StarterAssets.StarterAssetsInputs>();
        if (inputs != null)
        {
            inputs.cursorLocked = false;
            inputs.cursorInputForLook = false;
            inputs.look = Vector2.zero;
            inputs.move = Vector2.zero;
            inputs.jump = false;
            inputs.sprint = false;
        }
    }
}
