using UnityEngine;
using UnityEngine.SceneManagement;

// Prepara o diario; somente BatalhaKraken aguarda a leitura. A exploracao fica livre.
public static class BloqueioLeituraKraken
{
    public static void Preparar(DiarioEndy livro)
    {
        if (SceneManager.GetActiveScene().name != "Level_Kraken_4" || livro == null || livro.LeuFinal) return;
        // A fase final pressupoe a coleta na ilha, inclusive ao testar a cena isolada.
        var inventario = livro.GetComponent<InventarioEndy>();
        if (inventario != null && !inventario.temDiario) inventario.ColetarItem(false, true);
    }
}
