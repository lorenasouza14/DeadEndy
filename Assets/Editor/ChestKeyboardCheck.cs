using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

[InitializeOnLoad]
public static class ChestKeyboardCheck
{
    [MenuItem("Tools/Interacao/Ativar interacao dos baus")]
    public static void RepairEnabled()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var chests = Object.FindObjectsByType<InteracaoBau>(FindObjectsInactive.Include).Where(c => c.gameObject.scene == scene).ToArray();
        string report = string.Join("\n", chests.Select(c => c.transform.parent.name + ": antes=" + c.enabled));
        EditorSceneManager.SaveScene(scene, "Logs/chest-keyboard-before.unity", true);
        foreach (var chest in chests)
        {
            Undo.RecordObject(chest, "Ativar interação do baú");
            chest.enabled = true;
            EditorUtility.SetDirty(chest);
            if (PrefabUtility.IsPartOfPrefabInstance(chest)) PrefabUtility.RecordPrefabInstancePropertyModifications(chest);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Logs/chest-keyboard-repair.txt", report + "\nPASS: todos ativados e cena salva.");
        Debug.Log("Interação dos baús ativada e salva.");
    }
    static ChestKeyboardCheck() { EditorApplication.playModeStateChanged += Changed; }
    [MenuItem("Tools/Interacao/Preparar teste da tecla E")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool("ChestKeyboardCheck.Pending", true);
        EditorApplication.EnterPlaymode();
    }
    static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("ChestKeyboardCheck.Pending", false)) return;
        SessionState.SetBool("ChestKeyboardCheck.Pending", false);
        new GameObject("Teste real da tecla E").AddComponent<ChestKeyboardCheckRunner>();
    }
}

public class ChestKeyboardCheckRunner : MonoBehaviour
{
    InteracaoBau chest;
    InventarioEndy player;
    string events = "";
    float next;
    void Start()
    {
        player = FindAnyObjectByType<InventarioEndy>();
        chest = FindObjectsByType<InteracaoBau>().OrderBy(b => Vector3.Distance(b.transform.position, player.transform.position)).First();
        foreach (var boss in FindObjectsByType<CaranguejoChefe>(FindObjectsInactive.Include)) boss.enabled = false;
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        Vector3 stand = chest.transform.position - chest.transform.forward * 1.7f;
        var terrain = Terrain.activeTerrain;
        stand.y = terrain.SampleHeight(stand) + terrain.transform.position.y + 0.1f;
        player.transform.position = stand;
        cc.enabled = true;
        Physics.SyncTransforms();
        next = 0;
    }
    void Update()
    {
        if (chest == null || player == null) return;
        bool modern = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        bool legacy = Input.GetKeyDown(KeyCode.E);
        if (modern || legacy) events += $"\nE em {Time.time:F2}: novo={modern}, antigo={legacy}";
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + 0.5f;
        var cc = player.GetComponent<CharacterController>();
        var box = chest.GetComponent<BoxCollider>();
        var nearby = typeof(InteracaoBau).GetField("jogadorPerto", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(chest);
        File.WriteAllText("Logs/chest-keyboard-check.txt", $"{chest.transform.parent.name}: time={Time.time} focus={Application.isFocused} scale={Time.timeScale} input={InputSystem.settings.updateMode}\n" +
            $"player={player.transform.position} bounds={cc.bounds}\nchest={chest.transform.position} bounds={box.bounds} enabled={chest.enabled} trigger={box.isTrigger}\n" +
            $"nearby={nearby} puzzle={chest.FazendoPuzzle} global={InteracaoBau.PuzzleAtivo} slider={chest.barraPuzzle} visible={chest.objetoSlider.activeInHierarchy} hint={chest.iconeTeclaE.activeInHierarchy}\n" + events);
    }
}
