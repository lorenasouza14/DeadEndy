using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

public static class FontePortuguesSetup
{
    const string AssetPath = "Assets/Fonts/doctor_soos/Doctor Soos Bold 2.asset";
    const string SourcePath = "Assets/Fonts/doctor_soos/Doctor Soos Bold 2.1.ttf";
    const string Accents = "áàâãäéèêëíìîïóòôõöúùûüçÁÀÂÃÄÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇ";

    [MenuItem("Tools/Interacao/Corrigir acentos da fonte")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
        var source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
        if (font == null || source == null) throw new Exception("Fonte Doctor Soos não encontrada.");
        FontEngine.InitializeFontEngine();
        if (FontEngine.LoadFontFace(source, (int)font.faceInfo.pointSize) != FontEngineError.Success)
            throw new Exception("Não foi possível ler a fonte original.");
        string absentInSource = new string(Accents.Where(c => !SourceHasCharacter(c)).ToArray());
        if (absentInSource.Length > 0) throw new Exception("Acentos ausentes no arquivo original: " + absentInSource);
        Directory.CreateDirectory("Logs");
        File.Copy(AssetPath, "Logs/doctor-soos-before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".asset");
        string previous = new string(font.characterTable.Where(c => c.unicode <= char.MaxValue).Select(c => (char)c.unicode).ToArray());
        string missingBefore = new string(Accents.Where(c => !font.HasCharacter(c, false, false)).ToArray());
        // Preserva o asset, seus materiais e os glifos existentes; amplia o atlas conforme necessário.
        Undo.RecordObjects(AssetDatabase.LoadAllAssetsAtPath(AssetPath), "Habilitar acentos Doctor Soos");
        font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        font.isMultiAtlasTexturesEnabled = true;
        var serialized = new SerializedObject(font);
        serialized.FindProperty("m_SourceFontFile").objectReferenceValue = source;
        serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
        serialized.ApplyModifiedProperties();
        // Mesma operação usada pelo Inspector do TMP ao trocar Static por Dynamic.
        var textureUtilities = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("UnityEditor.TextCore.LowLevel.FontEngineEditorUtilities"))
            .First(t => t != null);
        var makeReadable = textureUtilities.GetMethod("SetAtlasTextureIsReadable", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var texture in font.atlasTextures)
            if (texture != null && !texture.isReadable) makeReadable.Invoke(null, new object[] { texture, true });
        // Pré-carrega o latim e a pontuação disponíveis no TTF, inclusive os acentos do português.
        string supported = new string(Enumerable.Range(32, 352).Concat(Enumerable.Range(0x2000, 112))
            .Where(c => SourceHasCharacter((uint)c)).Select(c => (char)c).ToArray());
        if (!font.TryAddCharacters(supported, out string missing))
            throw new Exception("Falha ao incluir caracteres no atlas: " + missing);
        foreach (char c in previous + Accents)
            if (!font.HasCharacter(c, false, false)) throw new Exception("Caractere não preservado: U+" + ((int)c).ToString("X4"));
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetPath)) EditorUtility.SetDirty(asset);
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssetIfDirty(font);
        TMPro_EventManager.ON_FONT_PROPERTY_CHANGED(true, font);
        foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
            if (text.font == font) { text.SetAllDirty(); text.ForceMeshUpdate(true); }
        Preview(font);
        File.WriteAllText("Logs/fonte-portugues-validation.txt",
            "PASS: TTF original contém todos os acentos testados.\n" +
            "Acentos ausentes antes: " + missingBefore + "\n" +
            "PASS: Todos os caracteres anteriores foram preservados.\n" +
            "PASS: Acentos disponíveis na própria Doctor Soos, sem outra fonte.\n" +
            "PASS: Texto de exemplo renderizado sem substituir caracteres.\n" +
            "PASS: Fonte original vinculada; atlas dinâmico com expansão automática.\n" +
            "Caracteres no atlas: " + font.characterTable.Count + "\n");
        Debug.Log("Acentos da Doctor Soos corrigidos. Prévia: Logs/fonte-portugues-preview.png");
    }

    static bool SourceHasCharacter(uint unicode)
    {
        return FontEngine.TryGetGlyphWithUnicodeValue(unicode, GlyphLoadFlags.LOAD_DEFAULT, out var glyph) && glyph.index != 0;
    }

    static void Preview(TMP_FontAsset font)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var rt = new RenderTexture(1400, 650, 24);
        var image = new Texture2D(1400, 650, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            var cameraObject = new GameObject("Prévia da fonte", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = scene;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            camera.orthographicSize = 2.9f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.075f, 0.10f);
            camera.targetTexture = rt;
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            canvasObject.GetComponent<RectTransform>().sizeDelta = new Vector2(1200, 520);
            canvasObject.transform.localScale = Vector3.one * 0.01f;
            var textObject = new GameObject("Texto de conferência", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(canvasObject.transform, false);
            textObject.GetComponent<RectTransform>().sizeDelta = new Vector2(1200, 520);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = 46;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.text = "Poção - Interação - Missão\nVocê encontrou a segunda pista!\nBaú, coração, água, início, bênção\nÁ À Â Ã É Ê Í Ó Ô Õ Ú Ü Ç\ná à â ã é ê í ó ô õ ú ü ç";
            Canvas.ForceUpdateCanvases();
            text.ForceMeshUpdate(true);
            for (int i = 0; i < text.textInfo.characterCount; i++)
            {
                var c = text.textInfo.characterInfo[i];
                if (char.IsWhiteSpace(c.character)) continue;
                if (c.fontAsset != font || c.textElement.unicode != c.character)
                    throw new Exception("Caractere substituído na prévia: " + c.character);
            }
            camera.Render();
            RenderTexture.active = rt;
            image.ReadPixels(new Rect(0, 0, 1400, 650), 0, 0);
            image.Apply();
            File.WriteAllBytes("Logs/fonte-portugues-preview.png", image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            EditorSceneManager.ClosePreviewScene(scene);
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(image);
        }
    }
}
