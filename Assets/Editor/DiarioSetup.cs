using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class DiarioSetup
{
    const string Cena = "Assets/Cenario/PNB_Tropical_Jungle/Scenes/Demo_Day.unity";
    const string Pendente = "DiarioSetup.Validar";
    static DiarioSetup() { EditorApplication.playModeStateChanged += MudouModo; }

    [MenuItem("Tools/Diario/Atualizar imagens das paginas")]
    public static void AtualizarPaginas()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Saia do Play Mode antes de atualizar as páginas.");

        var paginas = Enumerable.Range(1, 12).Select(i =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Imagens/pagina{i}.png")).ToArray();
        if (paginas.Any(p => p == null))
            throw new InvalidOperationException("Uma das 12 imagens do diário não foi importada como Sprite.");

        var livros = Object.FindObjectsByType<DiarioEndy>(FindObjectsInactive.Include)
            .Where(l => ProgressoJogo.EhCenaJogo(l.gameObject.scene.path)).ToArray();
        if (livros.Length == 0)
            throw new InvalidOperationException("Abra uma das cenas do jogo antes de atualizar o diário.");

        Directory.CreateDirectory("Logs");
        string relatorio = "";
        foreach (var grupo in livros.GroupBy(l => l.gameObject.scene))
        {
            var scene = grupo.Key;
            EditorSceneManager.SaveScene(scene,
                $"Logs/diario-paginas-before-{scene.name}-{DateTime.Now:yyyyMMdd-HHmmss}.unity", true);
            foreach (var livro in grupo)
            {
                relatorio += $"{scene.name}/{livro.name}: {livro.TotalPaginas} -> {paginas.Length} páginas\n";
                Undo.RecordObject(livro, "Atualizar páginas do diário");
                livro.paginas = (Sprite[])paginas.Clone();
                PrefabUtility.RecordPrefabInstancePropertyModifications(livro);
                EditorUtility.SetDirty(livro);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Não foi possível salvar as páginas em " + scene.path);
        }
        File.WriteAllText("Logs/diario-paginas.txt", relatorio);
        Debug.Log(relatorio);
    }

    [MenuItem("Tools/Diario/Configurar diario na cena")]
    public static void Configurar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != Cena) throw new Exception("Abra a cena Demo_Day antes de configurar o diário.");
        var objetos = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t => t.gameObject.scene == scene).ToArray();
        var roxo = objetos.Single(t => t.name == "Chest_Purple");
        var pilar = objetos.Single(t => t.name == "PP_Pillar_Stone_08 (3)");
        var visual = roxo.GetComponentsInChildren<Transform>(true).Single(t => t.name == "diario");
        var endy = Object.FindAnyObjectByType<InventarioEndy>();
        var chefe = Object.FindObjectsByType<CaranguejoChefe>(FindObjectsInactive.Include).Single(c => c.gameObject.scene == scene);
        Directory.CreateDirectory("Logs");
        EditorSceneManager.SaveScene(scene, "Logs/diario-before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
        var livro = endy.GetComponent<DiarioEndy>() ?? Undo.AddComponent<DiarioEndy>(endy.gameObject);
        livro.fonte = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/doctor_soos/Doctor Soos Bold 2.asset");
        var bau = roxo.GetComponent<BauDiario>() ?? Undo.AddComponent<BauDiario>(roxo.gameObject);
        bau.endy = endy;
        bau.animBau = roxo.GetComponentInChildren<Animator>(true);
        bau.diarioVisual = visual.gameObject;
        var dica = roxo.GetComponent<DicaInteracao>() ?? Undo.AddComponent<DicaInteracao>(roxo.gameObject);
        dica.modeloAviso = Object.FindObjectsByType<DicaInteracao>(FindObjectsInactive.Include).FirstOrDefault(d => d.modeloAviso != null)?.modeloAviso;
        var recompensa = Object.FindAnyObjectByType<RecompensaDiario>();
        if (recompensa == null)
        {
            var go = new GameObject("Recompensa do chefe - diário");
            Undo.RegisterCreatedObjectUndo(go, "Configurar recompensa");
            recompensa = Undo.AddComponent<RecompensaDiario>(go);
        }
        recompensa.chefe = chefe;
        recompensa.pilar = pilar.gameObject;
        recompensa.bau = bau;
        foreach (var obj in new Object[] { livro, bau, dica, recompensa }) EditorUtility.SetDirty(obj);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    public static void ConfigurarEValidar()
    {
        EditorSceneManager.OpenScene(Cena);
        Configurar();
        Validar();
    }

    [MenuItem("Tools/Diario/Validar diario em Play Mode")]
    public static void Validar()
    {
        SessionState.SetBool(Pendente, true);
        EditorApplication.EnterPlaymode();
    }

    static void MudouModo(PlayModeStateChange estado)
    {
        if (estado != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pendente, false)) return;
        SessionState.SetBool(Pendente, false);
        new GameObject("Validação do diário").AddComponent<DiarioValidation>().Run();
    }
}

public class DiarioValidation : MonoBehaviour
{
    string relatorio = "";
    public void Run() { StartCoroutine(Executar()); }
    void Check(bool ok, string mensagem)
    {
        if (!ok) throw new Exception(mensagem);
        relatorio += "PASS: " + mensagem + "\n";
        File.WriteAllText("Logs/diario-validation.txt", relatorio);
    }
    IEnumerator Executar()
    {
        var testes = Testes();
        while (true)
        {
            object proximo;
            try
            {
                if (!testes.MoveNext())
                {
                    if (Application.isBatchMode) EditorApplication.Exit(0);
                    else EditorApplication.ExitPlaymode();
                    yield break;
                }
                proximo = testes.Current;
            }
            catch (Exception erro)
            {
                File.WriteAllText("Logs/diario-validation.txt", relatorio + "FAIL: " + erro);
                Debug.LogException(erro);
                Time.timeScale = 1;
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else EditorApplication.ExitPlaymode();
                yield break;
            }
            yield return proximo;
        }
    }

    IEnumerator Testes()
    {
        yield return null;
        var recompensa = Object.FindAnyObjectByType<RecompensaDiario>();
        var bau = recompensa.bau;
        var endy = bau.endy;
        var livro = endy.GetComponent<DiarioEndy>();
        var movimento = endy.GetComponent<StarterAssets.ThirdPersonController>();
        movimento.enabled = false;
        var controller = endy.GetComponent<CharacterController>();
        controller.enabled = false;
        endy.transform.position = bau.transform.position + Vector3.right * 20;
        Check(!recompensa.pilar.activeSelf && !bau.gameObject.activeSelf, "Pilar e baú começam ocultos");
        Check(!livro.Abrir(), "Não lê antes de coletar");
        recompensa.chefe.gameObject.SetActive(true);
        recompensa.chefe.TomarPocaoEncolhedora();
        yield return new WaitForSeconds(0.5f);
        Check(!recompensa.Revelada, "Espera terminar o encolhimento");
        yield return new WaitForSeconds(1.8f);
        Check(recompensa.Revelada && recompensa.pilar.activeSelf && bau.gameObject.activeSelf, "Encolher revela pilar e baú");
        Check(!bau.Interagir(), "Não coleta à distância");
        endy.transform.position = bau.transform.position + new Vector3(0, -1, -2);
        Check(bau.Interagir() && !endy.temDiario, "Primeiro E só abre o baú");
        Check(!bau.Interagir(), "Ignora repetição durante a animação");
        yield return new WaitForSeconds(0.8f);
        Check(bau.Aberto && bau.diarioVisual.activeSelf, "Diário visível no baú aberto");
        Check(bau.Interagir() && endy.temDiario && !bau.diarioVisual.activeSelf, "Segundo E guarda o diário");
        Check(!bau.Interagir() && endy.PodeColetarPocao, "Diário é único e não ocupa a mão");
        controller.enabled = true;
        movimento.enabled = true;
        Time.timeScale = 0.75f;
        Check(livro.Abrir() && Time.timeScale == 0 && !movimento.enabled && Cursor.visible, "Leitura pausa mundo e libera cursor");
        Check(!livro.Folhear(-1), "Primeira página não volta");
        Check(livro.Folhear(1) && !livro.Folhear(1), "Uma folheada por vez");
        yield return new WaitForSecondsRealtime(0.15f);
        ScreenCapture.CaptureScreenshot("Logs/diario-folheando.png");
        yield return new WaitForSecondsRealtime(0.5f);
        Check(livro.PaginaEsquerda == 2 && !livro.Folheando, "Animação funciona durante pausa");
        ScreenCapture.CaptureScreenshot("Logs/diario-aberto.png");
        yield return new WaitForSecondsRealtime(0.2f);
        livro.Fechar();
        Check(Time.timeScale == 0.75f && movimento.enabled && endy.temDiario, "Fechar restaura velocidade e controle sem consumir diário");
        yield return null;
        Check(livro.Abrir() && livro.PaginaEsquerda == 2, "Reabre na última página");
        Check(livro.Folhear(1), "Avança à última dupla");
        yield return new WaitForSecondsRealtime(0.6f);
        Check(!livro.PodeAvancar && !livro.Folhear(1), "Não ultrapassa a última página");
        Check(livro.Folhear(-1), "Volta uma dupla");
        yield return new WaitForSecondsRealtime(0.6f);
        livro.Fechar();
        yield return null;
        var textura = new Texture2D(4, 8);
        var sprite = Sprite.Create(textura, new Rect(0, 0, 4, 8), Vector2.one * 0.5f);
        livro.paginas = new[] { sprite, null, sprite };
        Check(livro.Abrir() && livro.TotalPaginas == 3 && livro.PaginaEsquerda == 2 && !livro.PodeAvancar, "Aceita imagens, lacunas e quantidade ímpar");
        var direita = GameObject.Find("Leitura do diário").transform.Find("Capa/Página direita/Imagem da página").GetComponent<UnityEngine.UI.Image>();
        Check(!direita.enabled, "Página final excedente fica em branco");
        livro.Folhear(-1);
        livro.enabled = false;
        Check(!livro.Aberto && !livro.Folheando && Time.timeScale == 0.75f && movimento.enabled, "Desativar durante folheada libera a pausa");
        livro.enabled = true;
        yield return null;
        livro.paginas = new Sprite[0];
        Check(livro.Abrir(), "Reabre após interrupção");
        endy.GetComponent<VidaEndy>().ForcarDerrota();
        yield return null;
        Check(!livro.Aberto && !movimento.enabled && Cursor.visible, "Morte fecha livro e mantém controle do Game Over");
        Time.timeScale = 1;
        Destroy(sprite);
        Destroy(textura);
    }
}
