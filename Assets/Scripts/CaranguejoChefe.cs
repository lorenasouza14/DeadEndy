using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CaranguejoChefe : MonoBehaviour
{
    // Define todos os modos de ação possíveis para o chefe
    public enum EstadoChefe { Escondido, Perseguindo, PrepararInvestida, Investindo, Atordoado }

    [Header("Configurações do Chefe")]
    public EstadoChefe estadoAtual = EstadoChefe.Escondido;
    public Transform alvoEndy;
    public float velocidadePerseguicao = 3f;
    public Animator animChefe;
    private Rigidbody corpo;

    [Header("Configurações da Investida")]
    public float velocidadeInvestida = 15f; // Muito mais rápido que o normal!
    private bool preparando = false;
    private bool investindo = false;
    private Vector3 direcaoInvestida;
    private float timerAtaque = 0f;

    void Awake()
    {
        corpo = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // Se ainda está escondido ou a preparar o ataque, não faz nada
        if (estadoAtual == EstadoChefe.Escondido || preparando) return;

        if (investindo)
        {
            // FASE 2: Arranque! Corre reto na direção guardada, ignorando onde o Endy está
            transform.position += direcaoInvestida * velocidadeInvestida * Time.deltaTime;
        }
        else if (estadoAtual == EstadoChefe.Perseguindo)
        {
            // FASE 0: Perseguição normal
            // (Coloque aqui o seu código normal de virar e andar na direção do alvoEndy)
            transform.LookAt(new Vector3(alvoEndy.position.x, transform.position.y, alvoEndy.position.z));
            transform.position = Vector3.MoveTowards(transform.position, alvoEndy.position, velocidadePerseguicao * Time.deltaTime);

            // Conta o tempo para dar a próxima investida (ex: a cada 4 segundos)
            timerAtaque += Time.deltaTime;
            if (timerAtaque >= 4f)
            {
                StartCoroutine(RotinaDeInvestida());
            }
        }
    }

    void FixedUpdate()
    {
        // Controla apenas X/Z: a gravidade e as colisões continuam responsáveis pela altura.
        Vector3 velocidade = corpo.linearVelocity;
        velocidade.x = 0f;
        velocidade.z = 0f;

        switch (estadoAtual)
        {
            case EstadoChefe.Escondido:
                // Fica enterrado na areia, ignorando o jogador
                break;

            case EstadoChefe.Perseguindo:
                if (alvoEndy != null)
                {
                    Vector3 direcao = alvoEndy.position - corpo.position;
                    direcao.y = 0f;

                    if (direcao.sqrMagnitude > 0.0001f)
                    {
                        direcao.Normalize();
                        corpo.MoveRotation(Quaternion.LookRotation(direcao));
                        velocidade.x = direcao.x * velocidadePerseguicao;
                        velocidade.z = direcao.z * velocidadePerseguicao;
                    }
                }
                // (No futuro, colocaremos aqui um cronómetro para ele decidir dar a investida)
                break;

            case EstadoChefe.PrepararInvestida:
                // Para de andar, faz uma animação de ameaça/grito
                break;

            case EstadoChefe.Investindo:
                // Corre em linha reta a alta velocidade para tentar atropelar o Endy
                break;

            case EstadoChefe.Atordoado:
                // Bateu na pedra! Fica parado a reproduzir animação de tontura
                break;
        }

        corpo.linearVelocity = velocidade;
    }

    // Método que vamos usar para "acordar" o chefe quando o Endy pisar na armadilha
    public void AcordarChefe()
    {
        estadoAtual = EstadoChefe.Perseguindo;
        if (animChefe != null) animChefe.SetTrigger("Perseguir");
        Debug.Log("O Caranguejo Rei despertou!");
    }

    System.Collections.IEnumerator RotinaDeInvestida()
    {
        // FASE 1: O Aviso (Para e olha para o Endy)
        preparando = true;
        timerAtaque = 0f;

        // Vira para o Endy uma última vez e guarda essa direção em linha reta
        Vector3 direcaoParaOEndy = (alvoEndy.position - transform.position).normalized;
        direcaoParaOEndy.y = 0; // Para ele não tentar voar
        direcaoInvestida = direcaoParaOEndy;
        transform.LookAt(transform.position + direcaoInvestida);

        // Se tiver uma animação de "Aviso/Grito", dispararia aqui!

        // Espera 1.5 segundos para o jogador ter tempo de fugir
        yield return new WaitForSeconds(1.5f);

        // FASE 2: Inicia o Arranque
        preparando = false;
        investindo = true;

        // Fica a correr reto no máximo 2 segundos. Se não bater em nada, para.
        yield return new WaitForSeconds(2f);
        investindo = false;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Pedra"))
        {
            if (investindo)
            {
                Debug.Log("CRASH! O Caranguejo bateu na pedra em cheio!");
                investindo = false;
                // FASE 3: Fica atordoado! (Podemos adicionar uma animação de tontura depois)
            }
            else
            {
                Debug.Log("O caranguejo só esbarrou na pedra a andar normal.");
            }
        }
    }
}
