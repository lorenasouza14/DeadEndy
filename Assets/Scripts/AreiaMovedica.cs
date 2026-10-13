using UnityEngine;
using StarterAssets;

public class AreiaMovedica : MonoBehaviour
{
    public float velocidadeLenta = 1.5f;

    // Variáveis 'static' são compartilhadas por TODAS as poças ao mesmo tempo!
    // Assim, uma poça nunca apaga a velocidade original que a primeira poça salvou.
    private static int pocasPisadas = 0;
    private static float velocidadeOriginal;
    private static float sprintOriginal;
    private static ThirdPersonController controleSalvo;

    // Controla se o Endy está pisando especificamente NESTA poça
    private bool endyEstaAqui = false;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !endyEstaAqui)
        {
            var controle = other.GetComponent<ThirdPersonController>();
            if (controle != null)
            {
                // Segurança caso a fase tenha reiniciado
                if (controleSalvo != controle)
                {
                    pocasPisadas = 0;
                    controleSalvo = controle;
                }

                endyEstaAqui = true;
                pocasPisadas++;

                // Só salva a velocidade original na PRIMEIRA poça que o Endy pisar!
                if (pocasPisadas == 1)
                {
                    velocidadeOriginal = controle.MoveSpeed;
                    sprintOriginal = controle.SprintSpeed;
                }

                // Força a lentidão quer ele ande ou corra
                controle.MoveSpeed = velocidadeLenta;
                controle.SprintSpeed = velocidadeLenta;
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && endyEstaAqui)
        {
            SairDaPoca();
        }
    }

    // Se a poça sumir (após os 8 segundos) com o Endy ainda em cima dela,
    // a Unity aciona o OnDisable automaticamente e devolve a velocidade normal!
    void OnDisable()
    {
        if (endyEstaAqui)
        {
            SairDaPoca();
        }
    }

    void SairDaPoca()
    {
        endyEstaAqui = false;
        pocasPisadas--;

        // Só devolve a velocidade normal quando o Endy sair de TODAS as poças
        if (pocasPisadas <= 0)
        {
            pocasPisadas = 0;
            if (controleSalvo != null)
            {
                controleSalvo.MoveSpeed = velocidadeOriginal;
                controleSalvo.SprintSpeed = sprintOriginal;
            }
        }
    }
}