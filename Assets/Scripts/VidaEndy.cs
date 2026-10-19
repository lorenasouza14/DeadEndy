using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public class VidaEndy : MonoBehaviour
{
    [Header("Configurações de Vida")]
    public int vidaMaxima = 5;
    private int vidaAtual;
    private bool estaMorto = false;

    [Header("Componentes")]
    public Animator animEndy;
    public Image telaVermelha;

    [Header("Tempos de Animação")]
    public float tempoAnimacaoMorte = 2.5f;

    [Header("Física e Movimento")]
    public MonoBehaviour scriptDeMovimento; // O script que faz ele andar
    public CharacterController controleDeColisao; // A cápsula que o mantém em pé

    [Header("Sistema de Game Over")]
    public GameObject telaGameOverUI; // O Canvas que montamos
    public VideoPlayer videoGameOver; // O Projetor que toca o vídeo

    [Header("Inimigos")]
    public GameObject caranguejoChefe; 


    void Start()
    {
        vidaAtual = vidaMaxima;

        if (animEndy != null)
        {
            animEndy.SetTrigger("Nascer");
        }
    }

    public void TomarDano()
    {
        // Se já está morto, ignora golpes extras
        if (estaMorto) return;

        vidaAtual--;
        Debug.Log("Endy tomou dano! Vida restante: " + vidaAtual);

        StartCoroutine(PiscarTela());

        // Verifica se o golpe foi fatal
        if (vidaAtual <= 0)
        {
            // Dispara APENAS a rotina de morte
            StartCoroutine(RotinaMorte());
        }
        else
        {
            // Se ainda tem vida, toca a animação de tomar dano
            if (animEndy != null) animEndy.SetTrigger("TomarDano");
        }
    }

    System.Collections.IEnumerator PiscarTela()
    {
        if (telaVermelha != null)
        {
            telaVermelha.color = new Color(1f, 0f, 0f, 0.5f);
            yield return new WaitForSeconds(0.15f);
            telaVermelha.color = new Color(1f, 0f, 0f, 0f);
        }
    }

    System.Collections.IEnumerator RotinaMorte()
    {
        estaMorto = true;
        Debug.Log("GAME OVER! O Endy foi esmagado.");

        if (scriptDeMovimento != null) scriptDeMovimento.enabled = false;
        if (controleDeColisao != null) controleDeColisao.enabled = false;

        if (caranguejoChefe != null)
        {
            // Pega em todos os scripts do chefe e desliga-os para ele parar de pensar/atacar
            MonoBehaviour[] scriptsDoChefe = caranguejoChefe.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour script in scriptsDoChefe)
            {
                script.enabled = false;
            }

            // Se ele usar NavMesh (sistema de navegação da Unity), trava as pernas dele
            UnityEngine.AI.NavMeshAgent agente = caranguejoChefe.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agente != null) agente.enabled = false;
        }

        if (animEndy != null)
        {
            animEndy.SetTrigger("Morrer");
        }

        if (telaGameOverUI != null) telaGameOverUI.SetActive(true); // Liga a TV
        if (videoGameOver != null) videoGameOver.Play();            // Dá o Play no vídeo!

        // Remova o ReiniciarFase() daqui para a cena não dar refresh sozinha!
        yield return null;
    }

    void ReiniciarFase()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ForcarDerrota()
    {
        if (estaMorto) return;
        StartCoroutine(RotinaMorte());
    }
}