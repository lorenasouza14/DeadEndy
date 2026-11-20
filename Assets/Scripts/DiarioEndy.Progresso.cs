using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class DiarioEndy
{
    public int PaginasLidas { get; private set; }
    public int EtapaDesbloqueada { get; private set; }
    public int PaginasLiberadas => Mathf.Min(TotalPaginas, EtapaDesbloqueada >= 2 ? TotalPaginas : EtapaDesbloqueada == 1 ? 20 : 13);
    public bool LeuFinal => LeuIntervalo(20, TotalPaginas);
    GameObject dicaNovasPaginas;
    Scene cenaDoDiario;

    void CarregarPaginasCompletas()
    {
        var novas = Resources.LoadAll<Sprite>("PaginasDiario")
            .OrderBy(p => int.TryParse(p.name.TrimStart('p'), out int n) ? n : int.MaxValue).ToArray();
        if (novas.Length > 0) paginas = novas;
    }

    public void RestaurarProgresso(int etapa, int lidas)
    {
        EtapaDesbloqueada = Mathf.Clamp(etapa, 0, 2);
        PaginasLidas = lidas & 0xFFFFFF;
        AtualizarEtapa();
    }

    public void AtualizarEtapa()
    {
        var cena = SceneManager.GetActiveScene();
        int etapa = cena.name == "Level_Kraken_4" ? 2 : cena.name == "Level_Cave_3" ? 1 : 0;
        EtapaDesbloqueada = Mathf.Max(EtapaDesbloqueada, etapa);
        if (cenaDoDiario != cena)
        {
            cenaDoDiario = cena;
            if (etapa > 0 && !LeuIntervalo(etapa == 1 ? 13 : 20, etapa == 1 ? 20 : TotalPaginas))
                RestaurarPagina(etapa == 1 ? 13 : 20);
            if (dicaNovasPaginas != null) Destroy(dicaNovasPaginas);
        }
    }

    bool LeuIntervalo(int inicio, int fim)
    {
        if (fim <= inicio || fim > 24) return false;
        for (int i = inicio; i < fim; i++)
            if ((PaginasLidas & (1 << i)) == 0) return false;
        return true;
    }

    void RegistrarPaginasVisiveis()
    {
        if (!Aberto || Folheando) return;
        for (int i = PaginaEsquerda; i <= PaginaEsquerda + 1 && i < PaginasLiberadas && i < 24; i++)
            if (paginas != null && i < paginas.Length && paginas[i] != null) PaginasLidas |= 1 << i;
    }

    void AtualizarDicaDeLeitura()
    {
        AtualizarEtapa();
        string cena = SceneManager.GetActiveScene().name;
        bool final = cena == "Level_Kraken_4";
        bool pendente = inventario.temDiario && (final ? !LeuFinal : cena == "Level_Cave_3" && !LeuIntervalo(13, 20));
        if (pendente && dicaNovasPaginas == null && TryGetComponent<IndicadorItemEndy>(out var indicador))
        {
            dicaNovasPaginas = indicador.CriarDicaDeFase(indicador.diario, out var mensagem);
            mensagem.text = final
                ? "Novas páginas no diário! Pressione J e leia as páginas 21 a " + TotalPaginas + ". Feche o diário para começar."
                : "Novas páginas no diário! Pressione J para ler as páginas 14 a 20.";
        }
        if (dicaNovasPaginas != null)
            dicaNovasPaginas.SetActive(pendente && !Aberto && Time.timeScale > 0 && !MenuManager.EstaAberto && !TelaCarregamento.EmCurso);
    }

    void SalvarLeitura()
    {
        AutosaveEndy.Instancia?.SolicitarAutosave();
        FindAnyObjectByType<AutosaveCaverna>()?.SolicitarAutosave();
    }
}
