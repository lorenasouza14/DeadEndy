using UnityEngine;
using System.Collections.Generic;

public class GerenciadorDeBaus : MonoBehaviour
{
    // Instância única para os baús acharem este script facilmente
    public static GerenciadorDeBaus Instancia;

    [Header("Os 5 Baús da Cena (Para o RadioValidation)")]
    public Transform[] baus; // <-- Devolvemos a variável aqui para consertar o erro!

    [Header("Ordem de Entrega dos Itens")]
    [Tooltip("O que vai sair no 1º baú aberto (Ex: Toca-Discos)")]
    public GameObject itemPrimeiro;

    [Tooltip("Os 3 itens que saem misturados no 2º, 3º e 4º baú")]
    public GameObject[] itensDoMeio;

    [Tooltip("O que vai sair no 5º e último baú (Ex: Poção Verde e Diário)")]
    public GameObject itemUltimo;

    // A fila invisível que dita a ordem de aparecimento
    private Queue<GameObject> filaDeItens = new Queue<GameObject>();

    void Awake()
    {
        Instancia = this;
    }

    void Start()
    {
        // 1. Coloca o rádio como o PRIMEIRO da fila
        if (itemPrimeiro != null) filaDeItens.Enqueue(itemPrimeiro);

        // 2. Embaralha as 3 armadilhas/ajudas e coloca no MEIO da fila
        List<GameObject> meio = new List<GameObject>(itensDoMeio);
        for (int i = 0; i < meio.Count; i++)
        {
            GameObject temp = meio[i];
            int randomIndex = Random.Range(i, meio.Count);
            meio[i] = meio[randomIndex];
            meio[randomIndex] = temp;
        }
        foreach (GameObject item in meio)
        {
            filaDeItens.Enqueue(item);
        }

        // 3. Coloca a poção verde salvadora como a ÚLTIMA da fila
        if (itemUltimo != null) filaDeItens.Enqueue(itemUltimo);
    }

    // Chamado pelo baú no exato milissegundo em que ele é aberto!
    public void FazerItemNascer(Transform fundoDoBau)
    {
        if (filaDeItens.Count > 0)
        {
            // Pega o próximo da fila e o retira da lista
            GameObject proximoPrefab = filaDeItens.Dequeue();

            // Cria o item já visível dentro do baú
            GameObject itemSorteado = Instantiate(proximoPrefab, fundoDoBau.position, fundoDoBau.rotation);
            itemSorteado.transform.SetParent(fundoDoBau);

            // Pergunta se tem aquele nosso script de afinar a posição
            AjusteDeItem ajuste = itemSorteado.GetComponent<AjusteDeItem>();
            if (ajuste != null)
            {
                itemSorteado.transform.localPosition = ajuste.posicaoCustomizada;
                itemSorteado.transform.localEulerAngles = ajuste.rotacaoCustomizada;
            }
            else
            {
                itemSorteado.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            }

            Debug.Log("O baú distribuiu o item: " + itemSorteado.name);
        }
    }
}