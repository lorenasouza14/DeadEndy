using UnityEngine;
using UnityEngine.SceneManagement;
using StarterAssets;

// As paredes pertencem à cena; o diário e o jogador podem persistir entre mundos.
[DefaultExecutionOrder(-150)]
public sealed class BloqueioLeituraKraken : MonoBehaviour
{
    DiarioEndy diario;
    ThirdPersonController movimento;
    StarterAssetsInputs inputs;
    bool movimentoAnterior;
    bool liberado;

    public static void Preparar(DiarioEndy livro)
    {
        if (SceneManager.GetActiveScene().name != "Level_Kraken_4" || livro == null || livro.LeuFinal) return;
        var existente = FindAnyObjectByType<BloqueioLeituraKraken>();
        if (existente != null) return;
        // A fase final pressupoe a coleta na ilha, inclusive ao testar a cena isolada.
        var inventario = livro.GetComponent<InventarioEndy>();
        if (!inventario.temDiario) inventario.ColetarItem(false, true);
        var go = new GameObject("Bloqueio de entrada - ler diario");
        var bloqueio = go.AddComponent<BloqueioLeituraKraken>();
        bloqueio.diario = livro;
        bloqueio.movimento = livro.GetComponent<ThirdPersonController>();
        bloqueio.inputs = livro.GetComponent<StarterAssetsInputs>();
        bloqueio.movimentoAnterior = bloqueio.movimento != null && bloqueio.movimento.enabled;
        go.transform.SetPositionAndRotation(livro.transform.position, Quaternion.Euler(0, livro.transform.eulerAngles.y, 0));
        for (int i = 0; i < 2; i++)
        {
            var parede = new GameObject("Parede invisivel do diario " + (i + 1));
            parede.transform.SetParent(go.transform, false);
            // Deixa espaco para a camera de terceira pessoa atras do personagem.
            parede.transform.localPosition = new Vector3(0, 5, i == 0 ? 3 : -10);
            parede.AddComponent<BoxCollider>().size = new Vector3(20, 20, .5f);
        }
        bloqueio.Travar();
    }

    void Travar()
    {
        if (movimento != null) movimento.enabled = false;
        if (inputs != null) { inputs.move = Vector2.zero; inputs.jump = inputs.sprint = false; }
    }
    void Update()
    {
        if (diario == null) return;
        if (diario.LeuFinal && !diario.Aberto)
        {
            liberado = true;
            RestaurarMovimento();
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }
        Travar();
    }
    void RestaurarMovimento()
    {
        if (movimento != null && !(movimento.GetComponent<VidaJogadorKraken>()?.Morto ?? false))
            movimento.enabled = movimentoAnterior;
    }
    void OnDestroy() { if (!liberado) RestaurarMovimento(); }
}
