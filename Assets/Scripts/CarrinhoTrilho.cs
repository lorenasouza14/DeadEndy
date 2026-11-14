using UnityEngine;

public class CarrinhoTrilho : MonoBehaviour
{
    public Transform[] pontosDoCaminho; // Arraste os pontos do trilho aqui
    public float velocidade = 5f;
    private int indiceAtual = 0;

    private bool aMover = false;

    void Update()
    {
        if (!aMover || pontosDoCaminho.Length == 0) return;

        // Move o carrinho em direção ao ponto atual do trilho
        Transform alvo = pontosDoCaminho[indiceAtual];
        transform.position = Vector3.MoveTowards(transform.position, alvo.position, velocidade * Time.deltaTime);

        // Faz o carrinho rodar suavemente para olhar na direção da curva
        Vector3 direcao = (alvo.position - transform.position).normalized;
        if (direcao != Vector3.zero)
        {
            Quaternion rotAlvo = Quaternion.LookRotation(direcao);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotAlvo, Time.deltaTime * 10f);
        }

        // Se chegou perto do ponto, passa para o próximo
        if (Vector3.Distance(transform.position, alvo.position) < 0.2f)
        {
            indiceAtual++;
            if (indiceAtual >= pontosDoCaminho.Length)
            {
                indiceAtual = pontosDoCaminho.Length - 1; // Fim do trilho
                aMover = false;
            }
        }
    }

    public void IniciarViagem()
    {
        aMover = true;
    }
}