using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class GorilaSonoCena
{
    private const string Chave = "GorilaSonoCena.Teste";
    private const string Relatorio = "Logs/gorila-sono-cena.txt";
    private static GorilaGuardiao gorila;
    private static bool dormiu;
    private static bool acordando;
    private static float inicio;
    static GorilaSonoCena() { EditorApplication.update += Observar; }

    [MenuItem("Tools/Gorila/Ativar sono no gorila da caverna %#&d")]
    public static void Configurar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var candidatos = UnityEngine.Object.FindObjectsByType<GorilaGuardiao>(FindObjectsInactive.Include)
            .Where(g => g.gameObject.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene()).ToArray();
        if (candidatos.Length != 1) throw new InvalidOperationException("Esperado um GorilaGuardiao na entrada_caverna.");
        var g = candidatos[0];
        Undo.RecordObject(g, "Ativar sono do gorila");
        g.usarCicloSono = true;
        g.causarDano = true;
        g.guardandoCaverna = true;
        g.estadoDormindo = "Base Layer.Animal_Gorilla@Die";
        g.valorDormindo = 8;
        g.manterPoseFinalSono = true;
        g.intervaloAcordado = new Vector2(20, 35);
        g.intervaloDormindo = new Vector2(8, 12);
        g.tempoAdormecer = 0.8f;
        g.tempoAcordar = 1.5f;
        PrefabUtility.RecordPrefabInstancePropertyModifications(g);
        EditorUtility.SetDirty(g);
        EditorSceneManager.MarkSceneDirty(g.gameObject.scene);
        EditorSceneManager.SaveScene(g.gameObject.scene);
        Selection.activeGameObject = g.gameObject;
        Directory.CreateDirectory("Logs");
        File.WriteAllText(Relatorio, "Configuracao salva: sono ativo, dano de 1/3 ativo, Die=8, acordado 20-35s, dormindo 8-12s.\n");
        Debug.Log("Sono ativado no gorila da caverna e cena salva.", g);
    }

    [MenuItem("Tools/Gorila/Testar ciclo de sono na cena %#&f")]
    public static void Testar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        gorila = null;
        dormiu = acordando = false;
        SessionState.SetBool(Chave, true);
        EditorApplication.isPlaying = true;
    }

    private static void Observar()
    {
        if (!SessionState.GetBool(Chave, false) || !EditorApplication.isPlaying || EditorApplication.isPaused || Time.time < 1f) return;
        if (gorila == null)
        {
            gorila = UnityEngine.Object.FindObjectsByType<GorilaGuardiao>().Single(g => g.gameObject.scene.name == "Demo_day_fase_gorilla");
            inicio = Time.time;
            dormiu = acordando = false;
            File.AppendAllText(Relatorio, $"Play: sono={gorila.usarCicloSono}, dano={gorila.causarDano}, alvo={gorila.alvoEndy?.name}\n");
        }
        if (gorila.EstaDormindo && !dormiu)
        {
            if (!gorila.animGorila.GetCurrentAnimatorStateInfo(0).IsName(gorila.estadoDormindo)) return;
            dormiu = true;
            File.AppendAllText(Relatorio, $"PASS: dormiu naturalmente aos {Time.time:F1}s; Animator em {gorila.estadoDormindo}.\n");
        }
        if (dormiu && gorila.EstadoSono == GorilaGuardiao.EstadoSonoGorila.Acordando) acordando = true;
        if (acordando && gorila.EstadoSono == GorilaGuardiao.EstadoSonoGorila.Acordado)
        {
            File.AppendAllText(Relatorio, $"PASS: acordou com transicao aos {Time.time:F1}s. Ciclo completo confirmado na fase.\n");
            SessionState.SetBool(Chave, false);
            Debug.Log("Ciclo completo de sono confirmado na fase.");
            EditorApplication.isPlaying = false;
        }
        else if (Time.time - inicio > 60f)
        {
            File.AppendAllText(Relatorio, $"FAIL: tempo excedido, estado={gorila.EstadoSono}, ativo={gorila.enabled}.\n");
            SessionState.SetBool(Chave, false);
            EditorApplication.isPlaying = false;
        }
    }
}
