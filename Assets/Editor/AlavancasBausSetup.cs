using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class AlavancasBausSetup
{
    const string Pending = "AlavancasBausSetup.Validar";
    static AlavancasBausSetup() { EditorApplication.playModeStateChanged += Changed; }

    [MenuItem("Tools/Puzzle/Ajustar hastes e sliders")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = EditorSceneManager.GetActiveScene();
        var levers = Object.FindObjectsByType<AlavancaTriangulo>(FindObjectsInactive.Include)
            .Where(l => l.gameObject.scene == scene).ToArray();
        var chests = Object.FindObjectsByType<InteracaoBau>(FindObjectsInactive.Include)
            .Where(c => c.gameObject.scene == scene).ToArray();
        if (levers.Length != 3 || chests.Length != 5) throw new Exception("Abra a ilha com três alavancas e cinco baús.");
        // Verifica as referências antes de alterar a cena aberta, inclusive suas edições ainda não salvas.
        foreach (var lever in levers)
            if (!lever.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Switch_handle"))
                throw new Exception("Haste não encontrada: " + lever.name);
        foreach (var chest in chests)
            if (chest.barraPuzzle == null) throw new Exception("Slider não encontrado: " + chest.name);
        Directory.CreateDirectory("Logs");
        EditorSceneManager.SaveScene(scene, "Logs/alavancas-baus-before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
        foreach (var lever in levers)
        {
            Undo.RecordObject(lever, "Separar movimento da haste");
            lever.haste = lever.GetComponentsInChildren<Transform>(true).First(t => t.name == "Switch_handle");
            var hint = lever.GetComponent<DicaInteracao>();
            if (hint == null) hint = Undo.AddComponent<DicaInteracao>(lever.gameObject);
            Undo.RecordObject(hint, "Adicionar aviso de interação");
            if (lever.iconeTeclaE != null) hint.modeloAviso = lever.iconeTeclaE;
            // O campo antigo apontava para o asset do texto, sem uma instância visível na cena.
            lever.iconeTeclaE = null;
            EditorUtility.SetDirty(hint);
            PrefabUtility.RecordPrefabInstancePropertyModifications(hint);
            EditorUtility.SetDirty(lever);
            PrefabUtility.RecordPrefabInstancePropertyModifications(lever);
        }
        foreach (var chest in chests)
        {
            Undo.RecordObject(chest, "Ampliar zona verde");
            chest.zonaVerdeMin = 0.35f;
            chest.zonaVerdeMax = 0.65f;
            chest.larguraMinimaZona = 0.09f;
            EditorUtility.SetDirty(chest);
            PrefabUtility.RecordPrefabInstancePropertyModifications(chest);
            var zone = chest.barraPuzzle.GetComponentsInChildren<RectTransform>(true)
                .First(t => t.name == "Zona de acerto" || t.name == "MarcadorVerde");
            Undo.RecordObject(zone, "Ampliar faixa verde");
            zone.anchorMin = new Vector2(chest.zonaVerdeMin, zone.anchorMin.y);
            zone.anchorMax = new Vector2(chest.zonaVerdeMax, zone.anchorMax.y);
            EditorUtility.SetDirty(zone);
            PrefabUtility.RecordPrefabInstancePropertyModifications(zone);
            var rect = chest.barraPuzzle.GetComponent<RectTransform>();
            Undo.RecordObject(rect, "Aumentar slider do baú");
            // Escala uniforme preserva alinhamento do ponteiro e da zona de acerto.
            rect.localScale = new Vector3(1.25f, 1.25f, 1f);
            EditorUtility.SetDirty(rect);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        new GameObject("Validação alavancas e baús").AddComponent<AlavancasBausValidation>().Run();
    }
}

public class AlavancasBausValidation : MonoBehaviour
{
    string report = "";
    public void Run() { StartCoroutine(Guard()); }
    void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        report += "PASS: " + message + "\n";
        File.WriteAllText("Logs/alavancas-baus-validation.txt", report);
    }
    IEnumerator Guard()
    {
        var checks = Checks();
        while (true)
        {
            object next;
            try
            {
                if (!checks.MoveNext()) { EditorApplication.ExitPlaymode(); yield break; }
                next = checks.Current;
            }
            catch (Exception e)
            {
                File.WriteAllText("Logs/alavancas-baus-validation.txt", report + "FAIL: " + e);
                Debug.LogException(e);
                EditorApplication.ExitPlaymode();
                yield break;
            }
            yield return next;
        }
    }
    IEnumerator Checks()
    {
        yield return null;
        var player = Object.FindAnyObjectByType<InventarioEndy>();
        player.GetComponent<StarterAssets.ThirdPersonController>().enabled = false;
        foreach (var boss in Object.FindObjectsByType<CaranguejoChefe>(FindObjectsInactive.Include)) boss.enabled = false;
        var levers = Object.FindObjectsByType<AlavancaTriangulo>().OrderBy(l => l.indiceDaLuz).ToArray();
        var manager = levers[0].gerenciadorCentral;
        Check(!manager.bilheteSecreto.activeSelf, "Pista começa oculta");
        var cc = player.GetComponent<CharacterController>();
        var animator = player.animEndy;
        animator.Play("Base Layer.Idle Walk Run Blend", 0, 0f);
        yield return null;
        for (int i = 0; i < levers.Length; i++)
        {
            var lever = levers[i];
            var baseTransform = lever.GetComponentsInChildren<Transform>(true).First(t => t.name == "Switch_base");
            var rootMatrix = lever.transform.localToWorldMatrix;
            var baseMatrix = baseTransform.localToWorldMatrix;
            var start = lever.haste.localRotation;
            if (i == 0) Capture(lever.gameObject, "alavanca-antes", 1.8f);
            var hint = lever.GetComponent<DicaInteracao>();
            Check(hint != null && !hint.Visivel, "Aviso E oculto longe da alavanca " + i);
            lever.SendMessage("OnTriggerEnter", cc);
            yield return new WaitForEndOfFrame();
            Check(hint.Visivel && hint.GetComponentInChildren<TMPro.TMP_Text>().text == "[E] Puxar alavanca", "Aproximar mostra E na alavanca " + i);
            if (i == 0) Capture(lever.gameObject, "alavanca-aviso-e", 1.8f);
            lever.SendMessage("OnTriggerExit", cc);
            yield return new WaitForEndOfFrame();
            Check(!hint.Visivel, "Afastar oculta E na alavanca " + i);
            lever.SendMessage("OnTriggerEnter", cc);
            lever.SendMessage("PuxarAlavanca");
            yield return new WaitForSeconds(0.3f);
            Check(!hint.Visivel, "Puxar oculta E na alavanca " + i);
            Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Interact") || animator.GetNextAnimatorStateInfo(0).IsName("Interact"), "Endy executa Interact na alavanca " + i);
            yield return new WaitForSeconds(0.5f);
            Check(lever.transform.localToWorldMatrix == rootMatrix && baseTransform.localToWorldMatrix == baseMatrix, "Raiz e base ficam imóveis na alavanca " + i);
            Check(Mathf.Abs(Quaternion.Angle(start, lever.haste.localRotation) - 45f) < 0.1f, "Somente a haste inclina 45 graus na alavanca " + i);
            var end = lever.haste.localRotation;
            lever.SendMessage("PuxarAlavanca");
            yield return new WaitForSeconds(0.7f);
            Check(Quaternion.Angle(end, lever.haste.localRotation) < 0.01f, "Repetir interação não gira novamente a haste " + i);
            Check(manager.fogoDasTochas.Count(f => f.activeSelf) == i + 1 && manager.bilheteSecreto.activeSelf == (i == 2), "Puzzle libera pista somente após a terceira alavanca: " + i);
            if (i == 0) Capture(lever.gameObject, "alavanca-depois", 1.8f);
            lever.SendMessage("OnTriggerExit", cc);
        }
        var chests = Object.FindObjectsByType<InteracaoBau>().OrderBy(c => c.transform.root.name).ToArray();
        Check(chests.Length == 5, "Cinco baús encontrados");
        foreach (var chest in chests)
        {
            var slider = chest.barraPuzzle;
            Check(slider.transform.localScale == new Vector3(1.25f, 1.25f, 1f), "Slider 25% maior: " + chest.transform.root.name);
            var zone = slider.GetComponentsInChildren<RectTransform>(true).First(t => t.name == "Zona de acerto" || t.name == "MarcadorVerde");
            Check(Mathf.Abs(chest.zonaVerdeMax - chest.zonaVerdeMin - 0.3f) < 0.001f &&
                Mathf.Abs(zone.anchorMax.x - zone.anchorMin.x - 0.3f) < 0.001f,
                "Faixa verde e área de acerto ocupam 30% do slider: " + chest.transform.root.name);
            chest.SendMessage("OnTriggerEnter", cc);
            chest.SendMessage("IniciarPuzzle");
            yield return new WaitForSeconds(0.5f);
            Check(chest.FazendoPuzzle && slider.gameObject.activeInHierarchy && slider.value > 0f && slider.value <= 1f, "Puzzle e ponteiro funcionam: " + chest.transform.root.name);
            if (chest == chests[0]) Capture(chest.transform.root.gameObject, "bau-slider-maior", 2.5f);
            slider.value = 0.36f; // Dentro da faixa ampliada, mas fora da faixa antiga (0.4–0.6).
            chest.SendMessage("VerificarAcerto");
            Check(Mathf.Abs(chest.zonaVerdeMax - chest.zonaVerdeMin - 0.21f) < 0.001f,
                "Acerto na área ampliada avança a tranca: " + chest.transform.root.name);
            chest.SendMessage("CancelarPuzzle");
            chest.SendMessage("OnTriggerExit", cc);
        }
    }

    static void Capture(GameObject target, string filename, float size)
    {
        var renderers = target.GetComponentsInChildren<MeshRenderer>();
        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        var cameraObject = new GameObject("Prévia temporária");
        var camera = cameraObject.AddComponent<Camera>();
        var center = bounds.center + Vector3.up * 0.2f;
        camera.transform.position = center + target.transform.rotation * new Vector3(3f, 2f, 4f);
        camera.transform.LookAt(center);
        camera.orthographic = true;
        camera.orthographicSize = size;
        foreach (var canvas in target.GetComponentsInChildren<Canvas>()) canvas.transform.rotation = camera.transform.rotation;
        var rt = new RenderTexture(800, 800, 24);
        var texture = new Texture2D(800, 800, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 800, 800), 0, 0);
            texture.Apply();
            File.WriteAllBytes("Logs/" + filename + ".png", texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            rt.Release();
            Destroy(cameraObject); Destroy(rt); Destroy(texture);
        }
    }
}
