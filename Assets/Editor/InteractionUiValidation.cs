using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class InteractionUiValidation
{
    static InteractionUiValidation() { EditorApplication.playModeStateChanged += Changed; }
    [MenuItem("Tools/Interacao/Validar puzzle e avisos")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool("InteractionUiValidation.Pending", true);
        EditorApplication.EnterPlaymode();
    }
    static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("InteractionUiValidation.Pending", false)) return;
        SessionState.SetBool("InteractionUiValidation.Pending", false);
        new GameObject("Validar interface").AddComponent<InteractionUiValidationRunner>().Run();
    }
    public static void Finish(Exception error)
    {
        string result = error == null ? "PASS: cinco baús, proximidade real, hints individuais, ponteiro móvel, cancelamento, abertura e dicas de itens." : "FAIL: " + error;
        File.WriteAllText("Logs/interaction-ui-validation.txt", result);
        Debug.Log(result);
        EditorApplication.ExitPlaymode();
    }
}

public class InteractionUiValidationRunner : MonoBehaviour
{
    public void Run() { StartCoroutine(Guard()); }
    IEnumerator Guard()
    {
        var checks = Checks();
        while (true)
        {
            object wait;
            try { if (!checks.MoveNext()) { InteractionUiValidation.Finish(null); yield break; } wait = checks.Current; }
            catch (Exception error) { InteractionUiValidation.Finish(error); yield break; }
            yield return wait;
        }
    }
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Debug.Log("UI PASS: " + message);
    }
    IEnumerator Checks()
    {
        var chests = Object.FindObjectsByType<InteracaoBau>().OrderBy(b => b.transform.parent.name).ToArray();
        var inventory = Object.FindAnyObjectByType<InventarioEndy>();
        Check(chests.Length == 5 && inventory != null, "Cinco baús e Endy reais encontrados");
        inventory.GetComponent<StarterAssets.ThirdPersonController>().enabled = false;
        inventory.GetComponent<VidaEndy>().enabled = false;
        var controller = inventory.GetComponent<CharacterController>();
        foreach (var boss in Object.FindObjectsByType<CaranguejoChefe>(FindObjectsInactive.Include)) boss.enabled = false;
        Check(chests.Select(b => b.iconeTeclaE).Distinct().Count() == 5, "Cada baú tem sua própria dica");
        foreach (var chest in chests)
        {
            Check(chest.isActiveAndEnabled, "Script de interação está ativo: " + chest.transform.parent.name);
            Check(chest.barraPuzzle != null && chest.barraPuzzle.handleRect != null && chest.objetoSlider == chest.barraPuzzle.gameObject,
                "Referências de Slider/ponteiro: " + chest.transform.parent.name);
            var box = chest.GetComponent<BoxCollider>();
            controller.enabled = false;
            Vector3 stand = box.bounds.center - chest.transform.forward * 1.2f;
            var terrain = Terrain.activeTerrain;
            stand.y = terrain != null ? terrain.SampleHeight(stand) + terrain.transform.position.y + 0.1f : stand.y - controller.center.y;
            inventory.transform.position = stand;
            controller.enabled = true;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return null;
            Check(chest.iconeTeclaE.activeInHierarchy, "Aproximação acende dica: " + chest.transform.parent.name);
            if (chest == chests[0])
            {
                yield return new WaitForEndOfFrame();
                Capture(chest, "Logs/chest-hint-preview.png");
            }
            chest.SendMessage("IniciarPuzzle");
            yield return new WaitForSeconds(0.25f);
            Check(chest.objetoSlider.activeInHierarchy && !chest.iconeTeclaE.activeSelf && InteracaoBau.PuzzleAtivo, "E troca dica pelo puzzle");
            Check(inventory.GetComponent<StarterAssets.ThirdPersonController>().BloquearPulo, "Espaço reservado para acertar o puzzle");
            float value = chest.barraPuzzle.value;
            yield return new WaitForSeconds(0.1f);
            Check(Mathf.Abs(chest.barraPuzzle.value - value) > 0.01f, "Ponteiro se move");
            if (chest == chests[0])
            {
                yield return new WaitForEndOfFrame();
                Capture(chest, "Logs/chest-slider-preview.png");
            }
            var graphics = chest.objetoSlider.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
            var colors = graphics.Select(g => g.color).ToArray();
            chest.barraPuzzle.value = 0.05f;
            chest.SendMessage("VerificarAcerto");
            Check(!chest.FazendoPuzzle && chest.objetoSlider.activeSelf && chest.iconeTeclaE.activeInHierarchy && !InteracaoBau.PuzzleAtivo,
                "Erro libera o jogador e mantem slider visivel para piscar");
            Check(graphics.Any(g => g.color.r > 0.9f && g.color.g < 0.1f), "Erro pinta slider de vermelho");
            if (chest == chests[0])
            {
                yield return new WaitForEndOfFrame();
                Capture(chest, "Logs/chest-error-preview.png");
            }
            yield return new WaitForSeconds(chest.duracaoPiscarErro + 0.1f);
            Check(!chest.objetoSlider.activeSelf && graphics.Select((g, i) => g.color == colors[i]).All(same => same),
                "Fim das piscadas esconde slider e restaura cores");
            chest.SendMessage("IniciarPuzzle");
            chest.barraPuzzle.value = 0.05f;
            chest.SendMessage("VerificarAcerto");
            chest.SendMessage("IniciarPuzzle");
            Check(chest.objetoSlider.activeSelf && graphics.Select((g, i) => g.color == colors[i]).All(same => same),
                "Nova tentativa interrompe piscada sem deixar cores vermelhas");
            chest.SendMessage("CancelarPuzzle");
            chest.SendMessage("IniciarPuzzle");
            for (int i = 0; i < chest.trancasParaAbrir; i++)
            {
                yield return new WaitForSeconds(0.25f);
                chest.barraPuzzle.value = (chest.zonaVerdeMin + chest.zonaVerdeMax) * 0.5f;
                chest.SendMessage("VerificarAcerto");
            }
            yield return new WaitForSeconds(0.75f);
            Check(chest.BauAberto && !chest.objetoSlider.activeSelf && !chest.iconeTeclaE.activeSelf && chest.animBau.speed == 0,
                "Acertos abrem baú e mantêm tampa aberta");
            var item = chest.GetComponentInChildren<ItemColetavel>();
            Check(item != null && item.GetComponent<DicaInteracao>() != null, "Item nasce no fundo com aviso configurado");
            item.SendMessage("OnTriggerEnter", controller);
            yield return null;
            yield return new WaitForEndOfFrame();
            Check(item.GetComponent<DicaInteracao>().Visivel, "Aviso flutua sobre o item coletável");
        }
    }
    static void Capture(InteracaoBau chest, string path)
    {
        var go = new GameObject("Preview da interface");
        var camera = go.AddComponent<Camera>();
        var canvas = chest.barraPuzzle.GetComponentInParent<Canvas>();
        Vector3 target = Vector3.Lerp(chest.transform.position, canvas.transform.position, 0.65f);
        camera.transform.position = target + (Camera.main != null ? -Camera.main.transform.forward : Vector3.forward) * 5f + chest.transform.right * 2f + Vector3.up * 0.4f;
        camera.transform.LookAt(target);
        canvas.transform.rotation = camera.transform.rotation;
        camera.fieldOfView = 48;
        var rt = new RenderTexture(900, 900, 24);
        var texture = new Texture2D(900, 900, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            rt.Release();
            Object.Destroy(rt); Object.Destroy(texture); Object.Destroy(go);
        }
    }
}
