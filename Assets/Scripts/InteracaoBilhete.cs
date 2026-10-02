using UnityEngine;

public class InteracaoBilhete : MonoBehaviour
{
    [Header("Interface do Bilhete")]
    public GameObject PanelBilhete; // Arraste o Panel do Canvas para cá no Inspector

    private bool pertoDoBilhete = false;

    void Update()
    {
        // Verifica se o Endy está na área e apertou a tecla de interação (ex: 'E')
        if (pertoDoBilhete && Input.GetKeyDown(KeyCode.E))
        {
            MostrarBilhete();
        }
    }

    // Quando o Endy entra na área do gatilho
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            pertoDoBilhete = true;
            // Dica: Aqui você pode ativar um ícone flutuante de "Aperte E para ler"
        }
    }

    // Quando o Endy sai da área do gatilho
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            pertoDoBilhete = false;
            PanelBilhete.SetActive(false); // Esconde o bilhete se ele for embora
        }
    }

    void MostrarBilhete()
    {
        // Inverte o estado da UI (se está desligada, liga; se está ligada, desliga)
        bool estadoAtual = PanelBilhete.activeSelf;
        PanelBilhete.SetActive(!estadoAtual);

        // Opcional: Se quiser que o jogo pause enquanto ele lê, você pode usar:
        // Time.timeScale = PanelBilhete.activeSelf ? 0f : 1f;
    }
}