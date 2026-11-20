using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Validação opt-in: não altera saves nem cenas. Execute com a fase 4 aberta.
[InitializeOnLoad]
public static class DiarioProgressaoValidation
{
    const string Pedido = "Logs/validar-diario.request";
    const string Sessao = "DiarioProgressaoValidation.EmCurso";
    static double inicio, passoEm;
    static int passo;
    static DiarioEndy diario;
    static BatalhaKraken batalha;
    static readonly System.Collections.Generic.List<string> resultados = new System.Collections.Generic.List<string>();
    static DiarioProgressaoValidation() { EditorApplication.update += Atualizar; }

    [MenuItem("Tools/Diario/Validar progressao e bloqueio")]
    static void Iniciar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name != "Level_Kraken_4") return;
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Salve a cena antes de validar.");
        SessionState.SetBool(Sessao, true);
        inicio = passoEm = 0;
        passo = 0;
        resultados.Clear();
        EditorApplication.isPlaying = true;
    }

    static void Exigir(bool condicao, string descricao)
    {
        if (!condicao) throw new Exception(descricao);
        resultados.Add("PASS: " + descricao);
    }
    static void Definir(string nome, object valor) => typeof(DiarioEndy).GetField("<" + nome + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(diario, valor);
    static void Terminar(string erro = null)
    {
        if (erro != null) resultados.Add("FAIL: " + erro);
        File.WriteAllLines("Logs/diario-progressao-validation.txt", resultados);
        SessionState.SetBool(Sessao, false);
        if (diario != null && diario.Aberto) diario.Fechar();
        EditorApplication.isPlaying = false;
        Debug.Log(string.Join("\n", resultados));
    }
    static void Atualizar()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(Pedido))
        {
            File.Delete(Pedido);
            Iniciar();
            return;
        }
        if (!SessionState.GetBool(Sessao, false) || !EditorApplication.isPlaying) return;
        try
        {
            if (inicio == 0) inicio = EditorApplication.timeSinceStartup;
            double agora = EditorApplication.timeSinceStartup;
            if (agora - inicio > 75) throw new Exception("Tempo limite da validação.");
            if (passo == 0)
            {
                diario = UnityEngine.Object.FindAnyObjectByType<DiarioEndy>();
                batalha = UnityEngine.Object.FindAnyObjectByType<BatalhaKraken>();
                if (diario == null || batalha == null || Time.timeSinceLevelLoad < 2) return;
                Exigir(diario.TotalPaginas == 24 && diario.paginas.Select((p, i) => p != null && p.name == "p" + (i + 1)).All(b => b), "24 artes em ordem numérica");
                Definir("EtapaDesbloqueada", 0);
                Exigir(diario.PaginasLiberadas == 13, "Ilha libera somente p1-p13");
                diario.RestaurarPagina(12);
                Exigir(!diario.PodeAvancar, "Não avança para p15 na ilha");
                var painel = new GameObject("Teste página", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                var texto = new GameObject("Teste bloqueio", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
                try
                {
                    var imagem = painel.GetComponent<UnityEngine.UI.Image>();
                    var label = texto.GetComponent<TMPro.TMP_Text>();
                    var mostrar = typeof(DiarioEndy).GetMethod("MostrarPagina", BindingFlags.NonPublic | BindingFlags.Instance);
                    mostrar.Invoke(diario, new object[] { imagem, label, 13 });
                    Exigir(!imagem.enabled && imagem.sprite == null && label.text.Contains("bloqueada"), "p14 oculta inclusive na folha animada");
                    Definir("EtapaDesbloqueada", 1);
                    Exigir(diario.PaginasLiberadas == 20, "Caverna libera até p20");
                    mostrar.Invoke(diario, new object[] { imagem, label, 20 });
                    Exigir(!imagem.enabled && imagem.sprite == null, "p21 oculta na caverna");
                }
                finally { UnityEngine.Object.Destroy(painel); UnityEngine.Object.Destroy(texto); }
                Definir("EtapaDesbloqueada", 2);
                diario.RestaurarPagina(20);
                Exigir(diario.PaginasLiberadas == 24 && !diario.LeuFinal, "Fase 4 libera as 24 sem herdar leitura antiga");
                passo = 1; passoEm = agora;
            }
            else if (passo == 1 && agora - passoEm > 7)
            {
                var bloqueio = UnityEngine.Object.FindAnyObjectByType<BloqueioLeituraKraken>();
                Exigir(bloqueio != null && bloqueio.GetComponentsInChildren<BoxCollider>().Length == 2, "Duas paredes invisíveis na entrada");
                Exigir(!diario.GetComponent<StarterAssets.ThirdPersonController>().enabled && batalha.EmIntroducao, "Movimento e Kraken aguardam leitura mesmo após sete segundos");
                Exigir(diario.Abrir(), "Diário abre com jogador bloqueado");
                Exigir(diario.PaginaEsquerda == 20 && !diario.LeuFinal, "Abrir p21-p22 não conclui o trecho");
                diario.Fechar();
                passo = 2; passoEm = agora;
            }
            else if (passo == 2 && agora - passoEm > 1)
            {
                Exigir(!diario.GetComponent<StarterAssets.ThirdPersonController>().enabled && batalha.EmIntroducao, "Fechar cedo mantém bloqueio");
                Exigir(diario.Abrir() && diario.Folhear(1), "Folheia para p23-p24");
                passo = 3; passoEm = agora;
            }
            else if (passo == 3 && agora - passoEm > 3 && !diario.Folheando)
            {
                Exigir(diario.LeuFinal && diario.Aberto && batalha.EmIntroducao, "As quatro páginas foram vistas; combate ainda espera fechar");
                ScreenCapture.CaptureScreenshot("Logs/diario-p23-p24.png");
                passo = 5; passoEm = agora;
            }
            else if (passo == 5 && agora - passoEm > 2)
            {
                diario.Fechar();
                passo = 4; passoEm = agora;
            }
            else if (passo == 4 && agora - passoEm > 7)
            {
                Exigir(UnityEngine.Object.FindAnyObjectByType<BloqueioLeituraKraken>() == null, "Paredes removidas após leitura");
                Exigir(diario.GetComponent<StarterAssets.ThirdPersonController>().enabled, "Movimento restaurado");
                Exigir(!batalha.EmIntroducao, "Kraken inicia após fechar o diário");
                Terminar();
            }
        }
        catch (Exception e) { Terminar(e.ToString()); }
    }
}
