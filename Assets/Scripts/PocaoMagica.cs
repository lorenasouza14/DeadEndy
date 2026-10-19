using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class PocaoMagica : MonoBehaviour
{
    [Header("Efeitos Visuais")]
    public GameObject prefabEfeitoSplash; // <-- Arraste o Prefab de partículas aqui!

    private VidaEndy vidaEndy;
    private bool jaBateu = false;

    public void ConfigurarArremesso(VidaEndy vidaDoJogador, Collider colisorDoJogador, Vector3 velocidadeInicial)
    {
        vidaEndy = vidaDoJogador;

        // Impede que a poção bata no próprio corpo do Endy
        Collider meuColisor = GetComponent<Collider>();
        if (meuColisor != null && colisorDoJogador != null)
        {
            Physics.IgnoreCollision(meuColisor, colisorDoJogador);
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.linearVelocity = velocidadeInicial;

        // Segurança: se jogar a poção para fora do mapa e ela cair no vazio por 5s, também perde
        Invoke(nameof(ErrouArremesso), 5f);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (jaBateu) return;

        // Ignora o próprio jogador caso tenha mais de um colisor
        if (collision.collider.CompareTag("Player")) return;

        jaBateu = true;
        CancelInvoke(nameof(ErrouArremesso));

        // --- A MAGIA DO SPLASH ACONTECE AQUI ---
        if (prefabEfeitoSplash != null)
        {
            // Pega o ponto exato de contato da colisão
            Vector3 pontoImpacto = collision.contacts[0].point;

            // Instancia o splash de partículas no local do impacto
            GameObject splash = Instantiate(prefabEfeitoSplash, pontoImpacto, Quaternion.identity);

            // Garante que o efeito de partículas vai se autodestruir depois de 3 segundos para não pesar o jogo
            Destroy(splash, 3f);
        }

        // Verifica se acertou o Caranguejo Chefe
        CaranguejoChefe chefe = collision.collider.GetComponentInParent<CaranguejoChefe>();

        if (chefe != null)
        {
            Debug.Log("NA MOSCA! A poção quebrou no Caranguejo Rei!");
            chefe.TomarPocaoEncolhedora();
            Destroy(gameObject); // Some com o frasco de poção
        }
        else
        {
            // Bateu na areia, na pedra ou no baú!
            ErrouArremesso();
        }
    }

    void ErrouArremesso()
    {
        Debug.Log("NÃÃÃO! Você errou a única poção! O Caranguejo venceu!");
        if (vidaEndy != null)
        {
            vidaEndy.ForcarDerrota();
        }
        Destroy(gameObject);
    }

}