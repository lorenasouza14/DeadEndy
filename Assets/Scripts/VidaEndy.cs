using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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
        if (estaMorto) return;

        vidaAtual--;
        Debug.Log("Endy tomou dano! Vida restante: " + vidaAtual);

        StartCoroutine(PiscarTela());

        if (vidaAtual <= 0)
        {
            StartCoroutine(RotinaMorte());
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

        if (animEndy != null)
        {
            animEndy.SetTrigger("Morrer");
        }

        // Se o Endy tiver um script de movimento, desative-o aqui para ele não deslizar morto.
        // Exemplo: GetComponent<MovimentoEndy>().enabled = false;

        yield return new WaitForSeconds(tempoAnimacaoMorte);

        ReiniciarFase();
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