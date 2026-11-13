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
public static class IndicadorItemSetup
{
    const string Pending = "IndicadorItemSetup.Validar";
    static IndicadorItemSetup() { EditorApplication.playModeStateChanged += Changed; }

    [MenuItem("Tools/Interacao/Configurar icones dos itens")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var inventory = Object.FindAnyObjectByType<InventarioEndy>();
        if (inventory == null) throw new Exception("Abra a cena do Endy.");
        string[] names = { "pocao", "PocaoAzul", "pirulito", "trompete", "Radio", "diario" };
        var sprites = names.Select(ImportIcon).ToArray();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/doctor_soos/Doctor Soos Bold 2.asset");
        if (font == null || sprites.Any(s => s == null)) throw new Exception("Ícones ou fonte ausentes.");
        Directory.CreateDirectory("Logs");
        EditorSceneManager.SaveScene(inventory.gameObject.scene, "Logs/item-hud-before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
        var hud = inventory.GetComponent<IndicadorItemEndy>();
        if (hud == null) hud = Undo.AddComponent<IndicadorItemEndy>(inventory.gameObject);
        Undo.RecordObject(hud, "Configurar ícones do inventário");
        hud.fonte = font;
        hud.pocaoVerde = sprites[0]; hud.pocaoAzul = sprites[1]; hud.pirulito = sprites[2];
        hud.trompete = sprites[3]; hud.radio = sprites[4]; hud.diario = sprites[5];
        Undo.RecordObject(inventory, "Substituir interface antiga dos itens");
        foreach (var old in new[] { inventory.iconeDiarioUI, inventory.iconePocaoUI })
            if (old != null) { Undo.RecordObject(old, "Ocultar ícone antigo"); old.SetActive(false); EditorUtility.SetDirty(old); }
        inventory.iconeDiarioUI = null;
        inventory.iconePocaoUI = null;
        inventory.mostrarControles = true;
        EditorUtility.SetDirty(inventory);
        EditorUtility.SetDirty(hud);
        PrefabUtility.RecordPrefabInstancePropertyModifications(inventory);
        PrefabUtility.RecordPrefabInstancePropertyModifications(hud);
        EditorSceneManager.MarkSceneDirty(inventory.gameObject.scene);
        EditorSceneManager.SaveScene(inventory.gameObject.scene);
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    static Sprite ImportIcon(string name)
    {
        string path = "Assets/Imagens/" + name + ".png";
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 512;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        new GameObject("Validação ícones e ações").AddComponent<IndicadorItemValidation>().Run();
    }
}

public class IndicadorItemValidation : MonoBehaviour
{
    string report = "";
    public void Run() { StartCoroutine(Guard()); }
    void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        report += "PASS: " + message + "\n";
        File.WriteAllText("Logs/item-hud-validation.txt", report);
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
            catch (Exception error)
            {
                File.WriteAllText("Logs/item-hud-validation.txt", report + "FAIL: " + error);
                Debug.LogException(error);
                EditorApplication.ExitPlaymode();
                yield break;
            }
            yield return next;
        }
    }

    IEnumerator Checks()
    {
        yield return null;
        var inventory = Object.FindAnyObjectByType<InventarioEndy>();
        var hud = inventory.GetComponent<IndicadorItemEndy>();
        inventory.GetComponent<StarterAssets.ThirdPersonController>().enabled = false;
        inventory.GetComponent<CharacterController>().enabled = false;
        foreach (var boss in Object.FindObjectsByType<CaranguejoChefe>(FindObjectsInactive.Include)) boss.gameObject.SetActive(false);
        yield return new WaitForSeconds(1);
        Check(!hud.Visivel && !hud.DiarioVisivel, "Inventário vazio não ocupa a tela");
        inventory.ColetarItem(false, true);
        yield return null;
        yield return new WaitForEndOfFrame();
        Check(hud.DiarioVisivel && !hud.Visivel, "Diário guardado tem ícone próprio e atalho de leitura");
        string[] names = { "Potion_A", "Potion_C", "Lollipop_01", "Trumpet_01", "Radio" };
        Sprite[] icons = { hud.pocaoVerde, hud.pocaoAzul, hud.pirulito, hud.trompete, hud.radio };
        for (int i = 0; i < names.Length; i++)
        {
            var instance = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Baus/Prefabs/" + names[i] + ".prefab"));
            var potion = instance.GetComponent<PocaoMagica>();
            if (potion != null) { potion.tempoDeEfeito = 0.1f; potion.perderAoErrar = false; }
            Check(instance.GetComponent<ItemColetavel>().TentarColetar(inventory), "Coleta real: " + names[i]);
            yield return new WaitForSeconds(0.3f);
            Check(hud.Visivel && hud.IconeAtual == icons[i], "Ícone correto ao coletar: " + names[i]);
            var canvas = GameObject.Find("Itens do Endy");
            var actions = canvas.GetComponentsInChildren<TMP_Text>().Select(t => t.text).ToArray();
            string[] expected = i < 2 ? new[] { "R", "Beber", "F", "Arremessar" } :
                new[] { "F", i == 2 ? "Arremessar" : i == 3 ? "Tocar" : "Colocar" };
            Check(actions.SequenceEqual(expected.Concat(new[] { "[J] Ler diário" })), "Somente teclas e ações disponíveis: " + names[i]);
            Check(canvas.GetComponentsInChildren<TMP_Text>().All(t => t.font == hud.fonte), "Usa Doctor Soos: " + names[i]);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("Logs/item-hud-" + names[i] + ".png");
            if (i == 0)
            {
                yield return new WaitForSeconds(10);
                Check(hud.Visivel && hud.IconeAtual == icons[i] && hud.DiarioVisivel, "Item guardado mantém ícone e ações após dez segundos");
                inventory.enabled = false;
                yield return new WaitForEndOfFrame();
                Check(!hud.Visivel, "Desativar jogador oculta interface");
                inventory.enabled = true;
                yield return new WaitForEndOfFrame();
                Check(hud.Visivel && hud.IconeAtual == icons[i], "Reativar restaura o item ainda guardado");
                inventory.BeberPocao();
                yield return new WaitForSeconds(0.2f);
                Check(hud.Visivel && inventory.ExecutandoAcao && canvas.transform.Find("Área segura/Item na mão/Ações").GetComponent<CanvasGroup>().alpha < 0.5f,
                    "Beber mantém ícone e apaga comandos durante a animação");
                yield return new WaitForSeconds(1.8f);
            }
            else if (i == 4)
            {
                var oldPosition = inventory.transform.position;
                var oldRotation = inventory.transform.rotation;
                inventory.transform.SetPositionAndRotation(new Vector3(0, 1000.1f, 0), Quaternion.identity);
                Physics.SyncTransforms();
                Check(!inventory.ColocarRadio(), "Não coloca rádio sem chão");
                yield return new WaitForEndOfFrame();
                Check(hud.Visivel && hud.IconeAtual == hud.radio, "Falha ao colocar preserva rádio no indicador");
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.transform.position = new Vector3(0, 999.5f, 0);
                floor.transform.localScale = new Vector3(12, 1, 12);
                Physics.SyncTransforms();
                Check(inventory.ColocarRadio(), "Rádio pode ser colocado depois");
                yield return new WaitForEndOfFrame();
                Check(!hud.Visivel, "Rádio colocado sai do indicador");
                Check(inventory.ColetarRadio(instance.GetComponent<EfeitoTocaDiscos>()), "Rádio pode ser recolhido");
                yield return new WaitForEndOfFrame();
                Check(hud.Visivel && hud.IconeAtual == hud.radio, "Recolher rádio restaura o mesmo ícone");
                Destroy(instance);
                Destroy(floor);
                inventory.transform.SetPositionAndRotation(oldPosition, oldRotation);
            }
            else
            {
                inventory.JogarPocao();
                yield return new WaitForSeconds(i == 3 ? 2.5f : 0.6f);
                if (instance != null) Destroy(instance);
            }
            yield return new WaitForEndOfFrame();
            Check(!hud.Visivel && hud.DiarioVisivel, "Usar remove apenas o item consumido: " + names[i]);
            Check(inventory.PodeColetarPocao, "Pode coletar próximo item: " + names[i]);
        }
    }
}
