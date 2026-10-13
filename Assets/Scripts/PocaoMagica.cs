using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class PocaoMagica : MonoBehaviour
{
    private VidaEndy vidaEndy;
    private bool jaBateu = false;

    public void ConfigurarArremesso(VidaEndy vidaDoJogador, Collider colisorDoJogador, Vector3 velocidadeInicial)
    {
        vidaEndy = vidaDoJogador;

        // Impede que a poção bata no próprio corpo do Endy na hora que sai da mão dele!
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

        // Verifica se acertou o Caranguejo Chefe (mesmo que bata nas patas ou na concha filha)
        CaranguejoChefe chefe = collision.collider.GetComponentInParent<CaranguejoChefe>();

        if (chefe != null)
        {
            Debug.Log("NA MOSCA! A poção quebrou no Caranguejo Rei!");
            chefe.TomarPocaoEncolhedora();
            Destroy(gameObject);
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