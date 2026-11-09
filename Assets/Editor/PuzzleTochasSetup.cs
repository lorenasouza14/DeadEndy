using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PuzzleTochasSetup
{
    const string Pending = "PuzzleTochasSetup.Pending";
    static PuzzleTochasSetup() { EditorApplication.playModeStateChanged += Changed; }

    [MenuItem("Tools/Puzzle/Aplicar fogo e bilhete secreto")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var manager = Object.FindAnyObjectByType<GerenciadorPuzzleTriangulo>();
        if (manager == null || manager.bilheteSecreto == null || manager.fogoDasTochas.Length != 3)
            throw new Exception("Abra a cena com as três tochas e o bilhete secreto configurados.");
        var scene = manager.gameObject.scene;
        var original = Object.FindObjectsByType<InteracaoBilhete>(FindObjectsInactive.Include)
            .First(n => n.gameObject.scene == scene && n.gameObject != manager.bilheteSecreto);
        Directory.CreateDirectory("Logs");
        EditorSceneManager.SaveScene(scene, "Logs/puzzle-tochas-before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
        Undo.RegisterFullObjectHierarchyUndo(manager.bilheteSecreto, "Ajustar bilhete secreto");
        Undo.RegisterFullObjectHierarchyUndo(original.painelUI, "Uniformizar bilhetes");
        var flame = Material("Chama", 0);
        var ember = Material("Brasa", 1);
        foreach (var fire in manager.fogoDasTochas)
        {
            Undo.RegisterFullObjectHierarchyUndo(fire, "Melhorar fogo da tocha");
            ConfigureFire(fire, flame, ember);
        }
        ConfigureNote(original, manager.bilheteSecreto);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    static Material Material(string name, float shape)
    {
        const string folder = "Assets/Tochas";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "Tochas");
        string path = folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("DeadEndy/ChamaTocha"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetFloat("_Forma", shape);
        material.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        return material;
    }

    static ParticleSystem Child(GameObject parent, string name)
    {
        var child = parent.transform.Find(name);
        if (child != null) return child.GetComponent<ParticleSystem>();
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Camada de fogo");
        go.transform.SetParent(parent.transform, false);
        return go.AddComponent<ParticleSystem>();
    }

    static void ConfigureFire(GameObject fire, Material flame, Material ember)
    {
        fire.SetActive(true);
        fire.transform.rotation = Quaternion.identity;
        var mesh = fire.transform.parent.GetComponent<MeshFilter>();
        if (mesh != null && mesh.sharedMesh != null)
        {
            var bounds = mesh.sharedMesh.bounds;
            fire.transform.position = mesh.transform.TransformPoint(new Vector3(bounds.center.x, bounds.max.y - 0.04f, bounds.center.z));
        }
        var outer = fire.GetComponent<ParticleSystem>();
        ConfigureParticles(outer, flame, 26, 0.55f, 0.8f, 0.55f, 0.85f, 0.3f, 0.65f);
        var core = Child(fire, "Nucleo dourado");
        ConfigureParticles(core, flame, 22, 0.25f, 0.4f, 0.15f, 0.35f, 0.19f, 0.38f);
        var sparks = Child(fire, "Brasas");
        ConfigureParticles(sparks, ember, 5, 0.7f, 1.4f, 0.8f, 1.4f, 0.035f, 0.035f);
        var sparksMain = sparks.main;
        sparksMain.startColor = new Color(1.8f, 0.65f, 0.08f);
        var smoke = Child(fire, "Fumaca suave");
        smoke.transform.localPosition = Vector3.up * 0.35f;
        ConfigureParticles(smoke, ember, 3, 1.1f, 1.8f, 0.4f, 0.65f, 0.2f, 0.25f);
        var smokeMain = smoke.main;
        smokeMain.startColor = new Color(0.17f, 0.15f, 0.13f, 0.14f);
        var smokeSize = smoke.sizeOverLifetime;
        smokeSize.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0.6f, 1, 2.5f));

        var light = fire.GetComponentInChildren<Light>(true);
        if (light == null)
        {
            var go = new GameObject("Luz quente");
            Undo.RegisterCreatedObjectUndo(go, "Luz da tocha");
            go.transform.SetParent(fire.transform, false);
            light = go.AddComponent<Light>();
        }
        Undo.RecordObject(light, "Luz da tocha");
        light.transform.localPosition = Vector3.up * 0.25f;
        light.type = LightType.Point;
        light.color = new Color(1, 0.48f, 0.12f);
        light.intensity = 2.3f;
        light.range = 3.8f;
        light.shadows = LightShadows.None;
        light.enabled = true;
        if (light.GetComponent<LuzDaTocha>() == null) Undo.AddComponent<LuzDaTocha>(light.gameObject);
        fire.SetActive(false);
    }

    static void ConfigureParticles(ParticleSystem ps, Material material, float rate, float lifeMin, float lifeMax,
        float speedMin, float speedMax, float width, float height)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 2;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.maxParticles = 64;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(width * 0.7f, width * 1.2f);
        main.startSizeY = new ParticleSystem.MinMaxCurve(height * 0.8f, height * 1.2f);
        main.startSizeZ = 1;
        main.startColor = Color.white;
        main.gravityModifier = 0;
        main.startRotation3D = false;
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = rate;
        emission.rateOverDistance = 0;
        emission.SetBursts(new ParticleSystem.Burst[0]);
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 9;
        shape.radius = 0.09f;
        shape.rotation = new Vector3(-90, 0, 0);
        // Billboards têm pivô central: eleva a emissão para a base não atravessar o suporte.
        shape.position = Vector3.up * (height * 0.5f);
        var velocity = ps.velocityOverLifetime; velocity.enabled = false;
        var force = ps.forceOverLifetime; force.enabled = false;
        var collision = ps.collision; collision.enabled = false;
        var texture = ps.textureSheetAnimation; texture.enabled = false;
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.10f;
        noise.frequency = 2.2f;
        noise.scrollSpeed = 0.8f;
        noise.damping = true;
        noise.quality = ParticleSystemNoiseQuality.Low;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.9f, 0.12f), new GradientAlphaKey(0.65f, 0.55f), new GradientAlphaKey(0, 1) });
        var color = ps.colorOverLifetime; color.enabled = true; color.color = gradient;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.separateAxes = false;
        size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 0.65f), new Keyframe(0.2f, 1), new Keyframe(1, 0.1f)));
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.maxParticleSize = 1;
        EditorUtility.SetDirty(ps);
        EditorUtility.SetDirty(renderer);
    }

    static void ConfigureNote(InteracaoBilhete original, GameObject secret)
    {
        var note = secret.GetComponent<InteracaoBilhete>();
        if (note == null) note = Undo.AddComponent<InteracaoBilhete>(secret);
        Undo.RecordObject(note, "Ligar leitura do bilhete secreto");
        if (note.painelUI == null)
        {
            note.painelUI = Object.Instantiate(original.painelUI, original.painelUI.transform.parent, false);
            Undo.RegisterCreatedObjectUndo(note.painelUI, "Painel do bilhete secreto");
            note.painelUI.name = "PanelBilheteSecreto";
        }
        note.animEndy = original.animEndy;
        var sourceImage = original.painelUI.GetComponentsInChildren<Image>(true).First(i => i.gameObject != original.painelUI && i.sprite != null);
        var targetImage = note.painelUI.GetComponentsInChildren<Image>(true).First(i => i.gameObject != note.painelUI && i.sprite != null);
        var sprite = secret.GetComponent<SpriteRenderer>();
        targetImage.sprite = sprite.sprite;
        targetImage.name = "BilheteSecreto";
        Vector2 maxSize = sourceImage.GetComponent<TamanhoBilheteUI>() != null ?
            sourceImage.GetComponent<TamanhoBilheteUI>().tamanhoMaximo : sourceImage.rectTransform.sizeDelta;
        foreach (var image in new[] { sourceImage, targetImage })
        {
            Undo.RecordObject(image.rectTransform, "Tamanho dos bilhetes");
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            image.rectTransform.anchoredPosition = Vector2.zero;
            image.rectTransform.sizeDelta = maxSize;
            image.rectTransform.localScale = Vector3.one;
            var layout = image.GetComponent<TamanhoBilheteUI>();
            if (layout == null) layout = Undo.AddComponent<TamanhoBilheteUI>(image.gameObject);
            layout.tamanhoMaximo = maxSize;
            EditorUtility.SetDirty(layout);
        }
        var originalSprite = original.GetComponentInChildren<SpriteRenderer>(true);
        if (originalSprite != null)
        {
            Vector3 scale = secret.transform.localScale;
            scale.x = originalSprite.sprite.bounds.size.x * Mathf.Abs(originalSprite.transform.lossyScale.x) / sprite.sprite.bounds.size.x;
            scale.y = originalSprite.sprite.bounds.size.y * Mathf.Abs(originalSprite.transform.lossyScale.y) / sprite.sprite.bounds.size.y;
            secret.transform.localScale = scale;
        }
        var trigger = secret.GetComponent<SphereCollider>();
        if (trigger == null) trigger = Undo.AddComponent<SphereCollider>(secret);
        trigger.isTrigger = true;
        Vector3 worldScale = secret.transform.lossyScale;
        trigger.radius = 1.5f / Mathf.Max(Mathf.Abs(worldScale.x), Mathf.Abs(worldScale.y), Mathf.Abs(worldScale.z));
        trigger.center = secret.transform.InverseTransformPoint(secret.transform.position + Vector3.up * 0.5f);
        var hint = secret.GetComponent<DicaInteracao>();
        if (hint == null) hint = Undo.AddComponent<DicaInteracao>(secret);
        var originalHint = original.GetComponent<DicaInteracao>();
        if (originalHint != null) hint.modeloAviso = originalHint.modeloAviso;
        original.painelUI.SetActive(false);
        note.painelUI.SetActive(false);
        secret.SetActive(false);
        EditorUtility.SetDirty(note);
        EditorUtility.SetDirty(targetImage);
    }

    static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        new GameObject("Validação puzzle tochas").AddComponent<PuzzleTochasValidation>().Run();
    }
}

public class PuzzleTochasValidation : MonoBehaviour
{
    string report = "";
    public void Run() { StartCoroutine(Guard()); }
    void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        report += "PASS: " + message + "\n";
        File.WriteAllText("Logs/puzzle-tochas-validation.txt", report);
    }
    IEnumerator Guard()
    {
        var checks = Checks();
        while (true)
        {
            object wait;
            try
            {
                if (!checks.MoveNext()) { EditorApplication.ExitPlaymode(); yield break; }
                wait = checks.Current;
            }
            catch (Exception error)
            {
                File.WriteAllText("Logs/puzzle-tochas-validation.txt", report + "FAIL: " + error);
                Debug.LogException(error);
                EditorApplication.ExitPlaymode();
                yield break;
            }
            yield return wait;
        }
    }
    IEnumerator Checks()
    {
        yield return null;
        var manager = Object.FindAnyObjectByType<GerenciadorPuzzleTriangulo>();
        var player = Object.FindAnyObjectByType<InventarioEndy>();
        player.GetComponent<StarterAssets.ThirdPersonController>().enabled = false;
        foreach (var boss in Object.FindObjectsByType<CaranguejoChefe>(FindObjectsInactive.Include)) boss.enabled = false;
        Check(manager.fogoDasTochas.All(f => !f.activeSelf) && !manager.bilheteSecreto.activeSelf, "Puzzle começa apagado e sem revelar a pista");
        var levers = Object.FindObjectsByType<AlavancaTriangulo>().Where(l => l.gerenciadorCentral == manager).OrderBy(l => l.indiceDaLuz).ToArray();
        Check(levers.Length == 3 && levers.Select(l => l.indiceDaLuz).Distinct().Count() == 3, "Três alavancas ligadas a tochas diferentes");
        levers[0].SendMessage("PuxarAlavanca");
        manager.AcenderParteDoTriangulo(levers[0].indiceDaLuz);
        manager.AcenderParteDoTriangulo(-1);
        Check(manager.fogoDasTochas.Count(f => f.activeSelf) == 1 && !manager.bilheteSecreto.activeSelf, "Acender uma tocha repetidamente não libera a pista");
        levers[1].SendMessage("PuxarAlavanca");
        Check(!manager.bilheteSecreto.activeSelf, "Duas tochas ainda não revelam o bilhete");
        levers[2].SendMessage("PuxarAlavanca");
        yield return new WaitForSeconds(1);
        Check(manager.bilheteSecreto.activeInHierarchy && manager.fogoDasTochas.All(f => f.activeSelf), "As três alavancas revelam o bilhete");
        foreach (var fire in manager.fogoDasTochas)
        {
            Check(fire.GetComponentsInChildren<ParticleSystem>().All(ps => ps.isPlaying && ps.particleCount > 0), "Chama, núcleo, brasas e fumaça ativos: " + fire.name);
            Check(fire.GetComponentsInChildren<ParticleSystemRenderer>().All(r => r.sharedMaterial.shader.isSupported), "Materiais compatíveis com URP: " + fire.name);
        }
        CaptureFire(manager.fogoDasTochas[0]);
        var note = manager.bilheteSecreto.GetComponent<InteracaoBilhete>();
        var original = Object.FindObjectsByType<InteracaoBilhete>().First(n => n != note);
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.transform.position = note.transform.position + Vector3.forward * 0.5f;
        cc.enabled = true;
        Physics.SyncTransforms();
        cc.Move(Vector3.forward * 0.01f);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Check(note.MostrarDica, "Proximidade real oferece leitura com E");
        note.SendMessage("MostrarInterface");
        yield return null;
        Check(note.painelUI.activeSelf, "Bilhete secreto abre o próprio painel");
        original.painelUI.SetActive(true);
        yield return null;
        var a = original.painelUI.GetComponentInChildren<TamanhoBilheteUI>().GetComponent<RectTransform>();
        var b = note.painelUI.GetComponentInChildren<TamanhoBilheteUI>().GetComponent<RectTransform>();
        Check(Vector2.Distance(a.rect.size, b.rect.size) < 0.01f, "Os dois bilhetes abrem no mesmo tamanho");
        original.painelUI.SetActive(false);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot("Logs/bilhete-secreto-preview.png");
        note.SendMessage("MostrarInterface");
        Check(!note.painelUI.activeSelf, "E novamente fecha o bilhete");
        note.SendMessage("MostrarInterface");
        cc.Move(Vector3.forward * 5);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Check(!note.painelUI.activeSelf, "Afastar-se fecha o bilhete");
        yield return new WaitForSeconds(0.5f);
    }
    void CaptureFire(GameObject fire)
    {
        var go = new GameObject("Preview da tocha");
        var camera = go.AddComponent<Camera>();
        Vector3 center = fire.transform.position + Vector3.down * 0.15f;
        camera.transform.position = center + new Vector3(2.4f, 0.5f, 3.4f);
        camera.transform.LookAt(center);
        camera.orthographic = true;
        camera.orthographicSize = 1.3f;
        var rt = new RenderTexture(800, 800, 24);
        var image = new Texture2D(800, 800, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            image.ReadPixels(new Rect(0, 0, 800, 800), 0, 0);
            image.Apply();
            File.WriteAllBytes("Logs/tocha-preview.png", image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            rt.Release();
            Destroy(go); Destroy(rt); Destroy(image);
        }
    }
}
