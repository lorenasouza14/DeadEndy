using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class BananaMaterialPreviewTemporary
{
    static BananaMaterialPreviewTemporary() { EditorApplication.update += Run; }
    static void Run()
    {
        const string request = "Logs/juncao/banana-preview.txt";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
        File.Delete(request);
        try
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/fase_gorila/materials/golden_banana.mat");
            var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(r => r.sharedMaterials.Contains(material)).ToArray();
            if (renderers.Length == 0) throw new Exception("Nenhuma banana com este material na cena aberta.");
            var target = renderers.OrderBy(r => Vector3.Distance(r.bounds.center, new Vector3(28,9,102))).First();
            Render(target.bounds, "banana-antes.png");
            File.Copy("Assets/fase_gorila/materials/golden_banana.mat", "Logs/juncao/golden_banana-antes.mat", true);
            Undo.RecordObject(material, "Dourado claro da banana");
            var gold = new Color(1f, 226f/255f, 138f/255f, 1f);
            material.SetColor("_BaseColor", gold);
            material.SetColor("_Color", gold);
            material.SetFloat("_Metallic", .75f);
            material.SetFloat("_Smoothness", .94f);
            material.SetColor("_EmissionColor", new Color(.16f,.10f,.02f,1f));
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            Render(target.bounds, "banana-depois.png");
            File.WriteAllText("Logs/juncao/banana-resultado.txt", "Material atualizado. Bananas usando o material: " + renderers.Length + "\nAlvo: " + target.name + " bounds=" + target.bounds);
        }
        catch(Exception e) { File.WriteAllText("Logs/juncao/banana-resultado.txt", e.ToString()); }
    }
    static void Render(Bounds bounds, string name)
    {
        var go = new GameObject("Preview temporario da banana") { hideFlags = HideFlags.HideAndDontSave };
        var camera = go.AddComponent<Camera>();
        float radius = Mathf.Max(bounds.extents.magnitude,.2f);
        go.transform.position = bounds.center + new Vector3(1,.6f,-1).normalized * radius * 3.5f;
        go.transform.LookAt(bounds.center);
        camera.fieldOfView = 42;
        camera.nearClipPlane = .03f;
        camera.farClipPlane = 1000;
        camera.allowHDR = true;
        var rt = new RenderTexture(1000,1000,24);
        var previous = RenderTexture.active;
        Texture2D texture = null;
        try
        {
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            texture = new Texture2D(1000,1000,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,1000,1000),0,0);
            texture.Apply();
            File.WriteAllBytes("Logs/juncao/"+name,texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            if(texture != null) Object.DestroyImmediate(texture);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
        }
    }
}
