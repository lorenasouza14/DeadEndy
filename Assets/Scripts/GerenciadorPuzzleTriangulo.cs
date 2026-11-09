using UnityEngine;
using System.Collections.Generic;

public class GerenciadorPuzzleTriangulo : MonoBehaviour
{
    [Tooltip("Arraste os objetos de FOGO (filhos das tochas) para cá")]
    public GameObject[] fogoDasTochas; // Mudamos o nome aqui!

    [Tooltip("O bilhete que vai aparecer no final")]
    public GameObject bilheteSecreto;

    private readonly HashSet<int> tochasAcesas = new HashSet<int>();
    public bool Concluido => fogoDasTochas != null && fogoDasTochas.Length > 0 && tochasAcesas.Count == fogoDasTochas.Length;

    void Start()
    {
        tochasAcesas.Clear();
        if (bilheteSecreto != null) bilheteSecreto.SetActive(false);

        // Garante que o fogo comece apagado via código também
        if (fogoDasTochas == null) return;
        foreach (GameObject fogo in fogoDasTochas)
        {
            if (fogo != null) fogo.SetActive(false);
        }
    }

    public void AcenderParteDoTriangulo(int indiceDaLuz)
    {
        if (fogoDasTochas == null || indiceDaLuz < 0 || indiceDaLuz >= fogoDasTochas.Length ||
            fogoDasTochas[indiceDaLuz] == null || !tochasAcesas.Add(indiceDaLuz)) return;
        fogoDasTochas[indiceDaLuz].SetActive(true);
        Debug.Log("Fogo " + tochasAcesas.Count + "/" + fogoDasTochas.Length + " aceso!");

        if (tochasAcesas.Count == fogoDasTochas.Length)
        {
            CompletarPuzzle();
        }
    }

    void CompletarPuzzle()
    {
        Debug.Log("As três tochas estão acesas! O Bilhete apareceu!");
        if (bilheteSecreto != null) bilheteSecreto.SetActive(true);
    }
}
