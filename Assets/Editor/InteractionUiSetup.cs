using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

public static class InteractionUiSetup
{
    const string ChestPath = "Assets/Baus/Prefabs/Chest_Orange.prefab";
    const string HintPath = "Assets/Baus/Prefabs/AvisoTeclaE.prefab";
    [MenuItem("Tools/Interacao/Corrigir Slider e avisos")]
    public static void Repair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var chests = Object.FindObjectsByType<InteracaoBau>(FindObjectsInactive.Include).Where(b => b.gameObject.scene == scene).ToArray();
        if (chests.Length == 0) throw new Exception("Nenhum baú na cena aberta.");
        Directory.CreateDirectory("Logs");
        string backup = "Logs/interaction-before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        if (!EditorSceneManager.SaveScene(scene, backup + ".unity", true)) throw new IOException("Backup da cena falhou.");
        File.Copy(ChestPath, backup + ".prefab");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(ChestPath);
        var hint = AssetDatabase.LoadAssetAtPath<GameObject>(HintPath);
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Corrigir interface dos baús");
        foreach (var chest in chests)
        {
            Undo.RegisterFullObjectHierarchyUndo(chest.transform.parent.gameObject, "Interface do baú");
            Configure(chest, chest.transform.parent, source, hint);
        }
        foreach (var note in Object.FindObjectsByType<InteracaoBilhete>(FindObjectsInactive.Include).Where(b => b.gameObject.scene == scene))
        {
            var indicator = note.GetComponent<DicaInteracao>();
            if (indicator == null) indicator = Undo.AddComponent<DicaInteracao>(note.gameObject);
            Undo.RecordObject(indicator, "Aviso do bilhete");
            indicator.modeloAviso = hint;
            EditorUtility.SetDirty(indicator);
        }
        foreach (var item in Object.FindObjectsByType<ItemColetavel>(FindObjectsInactive.Include).Where(b => b.gameObject.scene == scene))
        {
            var indicator = item.GetComponent<DicaInteracao>();
            if (indicator == null) indicator = Undo.AddComponent<DicaInteracao>(item.gameObject);
            Undo.RecordObject(indicator, "Aviso do item");
            indicator.modeloAviso = hint;
            EditorUtility.SetDirty(indicator);
        }
        Undo.CollapseUndoOperations(group);
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Baus/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (path != ChestPath && asset.GetComponent<ItemColetavel>() == null) continue;
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (path == ChestPath)
                {
                    var model = contents.transform.Find("Chest_Animated");
                    var chest = model.GetComponent<InteracaoBau>();
                    if (chest == null) chest = model.gameObject.AddComponent<InteracaoBau>();
                    var trigger = model.GetComponent<BoxCollider>();
                    if (trigger == null) trigger = model.gameObject.AddComponent<BoxCollider>();
                    trigger.isTrigger = true;
                    trigger.size = new Vector3(3, 3, 3);
                    var body = model.GetComponent<Rigidbody>();
                    if (body == null) body = model.gameObject.AddComponent<Rigidbody>();
                    body.isKinematic = true;
                    body.useGravity = false;
                    Configure(chest, contents.transform, source, hint);
                }
                else
                {
                    var indicator = contents.GetComponent<DicaInteracao>();
                    if (indicator == null) indicator = contents.AddComponent<DicaInteracao>();
                    indicator.modeloAviso = hint;
                }
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Falha ao salvar cena.");
        Inspect();
        File.WriteAllText("Logs/interaction-ui-setup.txt", "PASS: " + chests.Length + " baús com Slider, ponteiro, faixa verde, Canvas ativo, aviso local e Animator. Prefabs dos itens e bilhete com avisos flutuantes.");
        Selection.activeGameObject = chests[0].gameObject;
        Debug.Log(File.ReadAllText("Logs/interaction-ui-setup.txt"));
    }

    static void Configure(InteracaoBau chest, Transform root, GameObject source, GameObject hint)
    {
        chest.enabled = true;
        if (chest.barraPuzzle == null) chest.barraPuzzle = root.GetComponentInChildren<Slider>(true);
        if (chest.barraPuzzle == null) throw new Exception("Slider ausente: " + root.name);
        var slider = chest.barraPuzzle;
        var canvas = slider.GetComponentInParent<Canvas>(true);
        if (canvas == null) throw new Exception("Canvas ausente: " + root.name);
        chest.objetoSlider = slider.gameObject;
        chest.animBau = chest.GetComponent<Animator>();
        if (chest.animBau == null) chest.animBau = chest.GetComponentInChildren<Animator>();
        if (chest.iconeTeclaE == null || EditorUtility.IsPersistent(chest.iconeTeclaE) || !chest.iconeTeclaE.transform.IsChildOf(canvas.transform))
            chest.iconeTeclaE = (GameObject)PrefabUtility.InstantiatePrefab(hint, canvas.transform);
        var text = chest.iconeTeclaE.GetComponent<TMP_Text>();
        text.text = "[E] Abrir baú";
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.fontSize = 28;
        var hintRect = chest.iconeTeclaE.GetComponent<RectTransform>();
        hintRect.anchoredPosition = Vector2.zero;
        hintRect.sizeDelta = new Vector2(260, 55);
        canvas.gameObject.SetActive(true);
        canvas.renderMode = RenderMode.WorldSpace;
        if (canvas.GetComponent<VirarParaCamera>() == null) canvas.gameObject.AddComponent<VirarParaCamera>();
        Bounds bounds = new Bounds(chest.transform.position, Vector3.zero);
        foreach (var renderer in chest.GetComponentsInChildren<MeshRenderer>(true)) bounds.Encapsulate(renderer.bounds);
        canvas.transform.position = new Vector3(bounds.center.x, bounds.max.y + 0.7f, bounds.center.z);
        var parentScale = canvas.transform.parent.lossyScale;
        canvas.transform.localScale = new Vector3(0.01f / parentScale.x, 0.01f / parentScale.y, 0.01f / parentScale.z);
        slider.minValue = 0;
        slider.maxValue = 1;
        slider.wholeNumbers = false;
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        if (slider.handleRect == null)
        {
            var template = source.GetComponentInChildren<Slider>(true).handleRect.parent;
            var handles = Object.Instantiate(template.gameObject, slider.transform, false);
            handles.name = "Handle Slide Area";
            slider.handleRect = handles.transform.Find("Handle") as RectTransform;
            slider.targetGraphic = slider.handleRect.GetComponent<Image>();
        }
        if (slider.fillRect != null) slider.fillRect.gameObject.SetActive(false);
        var oldMarker = slider.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == "MarcadorVerde");
        var generatedZone = slider.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == "Zona de acerto");
        if (oldMarker != null && generatedZone != null) Object.DestroyImmediate(generatedZone.gameObject);
        var zone = oldMarker != null ? oldMarker : generatedZone;
        if (zone == null)
        {
            var go = new GameObject("Zona de acerto", typeof(RectTransform), typeof(Image));
            zone = (RectTransform)go.transform;
        }
        zone.SetParent(slider.handleRect.parent, false);
        zone.localScale = Vector3.one;
        zone.localRotation = Quaternion.identity;
        zone.gameObject.SetActive(true);
        zone.anchorMin = new Vector2(chest.zonaVerdeMin, 0.25f);
        zone.anchorMax = new Vector2(chest.zonaVerdeMax, 0.75f);
        zone.offsetMin = zone.offsetMax = Vector2.zero;
        zone.GetComponent<Image>().color = new Color(0.15f, 0.85f, 0.25f, 0.95f);
        zone.GetComponent<Image>().raycastTarget = false;
        zone.SetAsFirstSibling();
        slider.handleRect.parent.SetAsLastSibling();
        slider.gameObject.SetActive(false);
        chest.iconeTeclaE.SetActive(false);
        EditorUtility.SetDirty(chest);
        foreach (var component in root.GetComponentsInChildren<Component>(true))
            if (component != null && PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
    }

    [MenuItem("Tools/Interacao/Inspecionar interface")]
    public static void Inspect()
    {
        Directory.CreateDirectory("Logs");
        var lines = Object.FindObjectsByType<InteracaoBau>(FindObjectsInactive.Include).Select(b =>
            $"{b.transform.parent?.name}/{b.name}: slider={b.barraPuzzle}, objeto={b.objetoSlider}, dica={b.iconeTeclaE}, anim={b.animBau}\n" +
            string.Join("\n", b.transform.parent.GetComponentsInChildren<Transform>(true).Select(t =>
            $"  {t.name} parent={t.parent?.name} active={t.gameObject.activeSelf} local={t.localPosition} scale={t.localScale}"))).ToArray();
        File.WriteAllLines("Logs/interaction-ui-inspection.txt", lines);
        Debug.Log("Interface inspecionada em Logs/interaction-ui-inspection.txt");
    }
}
