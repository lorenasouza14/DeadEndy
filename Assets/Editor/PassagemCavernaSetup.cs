using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PassagemCavernaSetup
{
    const string Cena = "Assets/Cenario/PNB_Tropical_Jungle/Scenes/juncao_fase1-2.unity";
    const string Pasta = "Logs/entrada-caverna/";
    static PassagemCavernaSetup()
    {
        EditorApplication.update += Comando;
        EditorApplication.playModeStateChanged += Modo;
    }
    static void Comando()
    {
        string caminho = Pasta + "comando.txt";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(caminho)) return;
        string comando = File.ReadAllText(caminho).Trim();
        File.Delete(caminho);
        try
        {
            if (comando == "configurar") Configurar();
            else if (comando == "testar") Testar();
        }
        catch (Exception e) { File.WriteAllText(Pasta + "erro.txt", e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Tools/Mapa/Caverna/Configurar entrada da fase unificada")]
    public static void Configurar()
    {
        var cena = SceneManager.GetSceneByPath(Cena);
        if (!cena.isLoaded || cena.isDirty) throw new InvalidOperationException("Abra e salve a cena unificada antes de configurar a entrada.");
        var entrada = cena.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
            .Single(t => t.name == "fundo_entrada_caverna").gameObject;
        Undo.RegisterFullObjectHierarchyUndo(entrada, "Configurar entrada da caverna");
        var parede = entrada.GetComponent<BoxCollider>();
        // O gorila desativa o collider do painel ao liberar a passagem.
        // O sensor filho permanece ativo e preserva esse bloqueio existente.
        var sensor = entrada.transform.Find("GatilhoEntradaCaverna");
        if (sensor == null)
        {
            var novo = new GameObject("GatilhoEntradaCaverna");
            Undo.RegisterCreatedObjectUndo(novo, "Criar sensor da caverna");
            novo.transform.SetParent(entrada.transform, false);
            sensor = novo.transform;
        }
        var gatilho = sensor.GetComponent<BoxCollider>();
        if (gatilho == null) gatilho = Undo.AddComponent<BoxCollider>(sensor.gameObject);
        gatilho.center = parede.center;
        gatilho.size = parede.size + Vector3.one * .1f;
        gatilho.isTrigger = true;
        gatilho.enabled = true;
        var corpo = sensor.GetComponent<Rigidbody>();
        if (corpo == null) corpo = Undo.AddComponent<Rigidbody>(sensor.gameObject);
        corpo.isKinematic = true;
        corpo.useGravity = false;
        if (sensor.GetComponent<EntradaCaverna>() == null) Undo.AddComponent<EntradaCaverna>(sensor.gameObject);
        EditorSceneManager.MarkSceneDirty(cena);
        if (!EditorSceneManager.SaveScene(cena)) throw new IOException("Não foi possível salvar a entrada.");
        Directory.CreateDirectory(Pasta);
        File.WriteAllText(Pasta + "configuracao.txt", "PASS: fundo_entrada_caverna configurado para Level_Cave_3.\n");
    }
    [MenuItem("Tools/Mapa/Caverna/Testar passagem em Play Mode")]
    public static void Testar()
    {
        Directory.CreateDirectory(Pasta);
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Salve as cenas antes do teste.");
        SessionState.SetString("EntradaCaverna.CenaInicial", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool("EntradaCaverna.Testando", true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Cena);
        EditorApplication.EnterPlaymode();
    }
    static void Modo(PlayModeStateChange estado)
    {
        if (!SessionState.GetBool("EntradaCaverna.Testando", false)) return;
        if (estado == PlayModeStateChange.EnteredPlayMode)
        {
            foreach (var save in Object.FindObjectsByType<AutosaveEndy>()) save.enabled = false;
            new GameObject("Teste temporario da entrada").AddComponent<EntradaCavernaPlayProbe>();
        }
        if (estado == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool("EntradaCaverna.Testando", false);
            string anterior = SessionState.GetString("EntradaCaverna.CenaInicial", "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(anterior) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(anterior);
        }
    }
}

public class EntradaCavernaPlayProbe : MonoBehaviour
{
    const string Relatorio = "Logs/entrada-caverna/play.txt";
    void Awake() { DontDestroyOnLoad(gameObject); }
    void Check(bool condicao, string mensagem)
    {
        File.AppendAllText(Relatorio, (condicao ? "PASS: " : "FAIL: ") + mensagem + "\n");
        if (!condicao) throw new Exception(mensagem);
    }
    IEnumerator Start()
    {
        File.WriteAllText(Relatorio, "");
        // O wrapper encerra o teste e restaura o editor mesmo em caso de falha.
        var teste = Executar();
        while (true)
        {
            object atual;
            try { if (!teste.MoveNext()) break; atual = teste.Current; }
            catch (Exception e) { File.AppendAllText(Relatorio, "FAIL: " + e + "\n"); break; }
            yield return atual;
        }
        EditorApplication.ExitPlaymode();
    }
    IEnumerator Executar()
    {
        yield return new WaitForSecondsRealtime(3);
        var entrada = Object.FindAnyObjectByType<EntradaCaverna>();
        Check(entrada != null, "Entrada instalada na cena unificada");
        var area = entrada.GetComponent<BoxCollider>();
        Check(area.isTrigger && entrada.GetComponent<Rigidbody>().isKinematic, "Area de passagem ativa");
        var estranho = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        estranho.transform.position = area.bounds.center;
        estranho.AddComponent<Rigidbody>().useGravity = false;
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Check(SceneManager.GetActiveScene().name == "juncao_fase1-2", "Objetos sem jogador nao carregam a caverna");
        Destroy(estranho);
        var jogador = Object.FindObjectsByType<StarterAssets.ThirdPersonController>().Single(p => p.CompareTag("Player"));
        var capsula = jogador.GetComponent<CharacterController>();
        capsula.enabled = false;
        jogador.transform.position = area.bounds.center - Vector3.up * capsula.center.y;
        capsula.enabled = true;
        Physics.SyncTransforms();
        capsula.Move(Vector3.forward * .01f);
        float limite = Time.realtimeSinceStartup + 90;
        while (SceneManager.GetActiveScene().name != "Level_Cave_3" && Time.realtimeSinceStartup < limite)
            yield return null;
        Check(SceneManager.GetActiveScene().name == "Level_Cave_3", "Contato fisico do Endy carrega Level_Cave_3");
        yield return null;
        yield return null;
        var jogadores = Object.FindObjectsByType<StarterAssets.ThirdPersonController>().Where(p => p.CompareTag("Player")).ToArray();
        Check(jogadores.Length == 1 && jogadores[0].enabled, "Um unico Endy ativo na caverna");
        Check(!SceneManager.GetSceneByName("juncao_fase1-2").isLoaded, "Fase anterior descarregada");
        var ponto = GameObject.Find("PontoEntrada");
        Check(ponto != null && Vector3.Distance(jogadores[0].transform.position, ponto.transform.position) < 3f, "Endy nasce no PontoEntrada da caverna");
        Check(Camera.main != null, "Camera principal ativa");
        var camera = Object.FindObjectsByType<Unity.Cinemachine.CinemachineCamera>().Single(c => c.name == "PlayerFollowCamera");
        Check(camera.Follow == jogadores[0].CinemachineCameraTarget.transform, "Camera acompanha o Endy da caverna");
    }
}
