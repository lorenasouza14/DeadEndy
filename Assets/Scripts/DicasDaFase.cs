using TMPro;
using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(IndicadorItemEndy))]
public class DicasDaFase : MonoBehaviour
{
    const string DicaAlavanca = "Procure a alavanca nas árvores ao redor.";
    [SerializeField] Sprite icone;
    [Tooltip("A segunda alavanca da lista é a escondida nas árvores: Switch (1).")]
    [SerializeField] AlavancaTriangulo[] alavancas = System.Array.Empty<AlavancaTriangulo>();
    [SerializeField] CaranguejoChefe chefe;
    [SerializeField] GameObject[] paineisBilhete = System.Array.Empty<GameObject>();
    [SerializeField, Min(1f)] float esperaAlavanca = 45f;
    [SerializeField, Min(1f)] float duracaoDica = 12f;

    InventarioEndy inventario;
    VidaEndy vida;
    GameObject painel;
    TMP_Text mensagem;
    string textoAtual;
    float tempoBusca, tempoRestante;
    bool encontrouEscondida, mostrouChefe;
#if UNITY_EDITOR
    float tempoPreviaAlavanca;

    [UnityEditor.MenuItem("Tools/Dicas/Testar dica da alavanca _F8")]
    static void TestarDicaAlavanca()
    {
        var dicas = FindAnyObjectByType<DicasDaFase>();
        if (dicas != null && Application.isPlaying)
            dicas.tempoPreviaAlavanca = dicas.duracaoDica;
        else
            Debug.Log("Para testar a dica, entre na fase pelo Play Mode.");
    }
#endif

    void Start()
    {
        inventario = GetComponent<InventarioEndy>();
        vida = GetComponent<VidaEndy>();
        painel = GetComponent<IndicadorItemEndy>().CriarDicaDeFase(icone, out mensagem);
    }

    void Update()
    {
        if (!inventario.isActiveAndEnabled || !inventario.mostrarControles || (vida != null && vida.EstaMorto) ||
            Time.timeScale == 0 || MenuManager.EstaAberto || BilheteAberto())
        {
            painel.SetActive(false);
            return;
        }

        tempoRestante = Mathf.Max(0f, tempoRestante - Time.deltaTime);
        if (chefe != null && chefe.isActiveAndEnabled && chefe.estadoAtual != CaranguejoChefe.EstadoChefe.Escondido)
        {
            if (chefe.estadoAtual == CaranguejoChefe.EstadoChefe.Derrotado)
                tempoRestante = 0f;
            else if (!mostrouChefe && chefe.NumeroAtordoamento > 0)
            {
                mostrouChefe = true;
                Mostrar("Fique na frente ou atrás da pedra. Na investida, esquive com AA ou DD para o caranguejo bater e ficar atordoado!");
            }
            else if (!mostrouChefe)
                tempoRestante = 0f;
        }
        else
            AtualizarBusca(Time.deltaTime);

#if UNITY_EDITOR
        // A prévia também funciona em checkpoints com as três alavancas puxadas.
        if (tempoPreviaAlavanca > 0f)
        {
            tempoPreviaAlavanca -= Time.deltaTime;
            mensagem.text = DicaAlavanca;
            painel.SetActive(true);
            return;
        }
#endif
        if (tempoRestante > 0f) mensagem.text = textoAtual;
        painel.SetActive(tempoRestante > 0f);
    }

    void AtualizarBusca(float delta)
    {
        var escondida = alavancas.Length == 3 ? alavancas[1] : null;
        if (escondida != null && escondida.MostrarDica) encontrouEscondida = true;
        if (escondida == null || !escondida.isActiveAndEnabled || escondida.Concluida || encontrouEscondida ||
            alavancas[0] == null || !alavancas[0].Concluida || alavancas[2] == null || !alavancas[2].Concluida)
        {
            tempoBusca = tempoRestante = 0f;
            return;
        }
        if (tempoRestante > 0f) return;
        tempoBusca += delta;
        if (tempoBusca >= esperaAlavanca)
        {
            tempoBusca = 0f;
            Mostrar(DicaAlavanca);
        }
    }

    bool BilheteAberto()
    {
        foreach (var bilhete in paineisBilhete)
            if (bilhete != null && bilhete.activeInHierarchy) return true;
        return false;
    }

    void Mostrar(string texto)
    {
        textoAtual = texto;
        tempoRestante = duracaoDica;
    }

    void OnDisable() { if (painel != null) painel.SetActive(false); }
}
