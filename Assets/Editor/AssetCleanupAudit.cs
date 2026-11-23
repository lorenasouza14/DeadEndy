using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public static class AssetCleanupAudit
{
    static AssetCleanupAudit() { EditorApplication.update += Pending; }
    static void Pending()
    {
        const string request = "Logs/asset-cleanup-request.txt";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string command = File.ReadAllText(request).Trim();
        bool packages = command == "packages";
        File.Delete(request);
        if (command == "folders") { AuditFolders(); return; }
        AnalyzeInternal(packages);
    }
    static void AuditFolders()
    {
        var all = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") && File.Exists(p)).ToArray();
        var roots = new HashSet<string>(EditorBuildSettings.scenes.Select(s => s.path));
        foreach (var p in all)
            if (p.EndsWith(".cs") || p.EndsWith(".asmdef") || p.EndsWith(".asmref") || p.EndsWith(".dll") ||
                p.Contains("/Resources/") || p.StartsWith("Assets/StreamingAssets/") || p.StartsWith("Assets/Settings/")) roots.Add(p);
        foreach (var settings in Directory.GetFiles("ProjectSettings"))
        foreach (Match m in Regex.Matches(File.ReadAllText(settings), "[a-fA-F0-9]{32}"))
        {
            string p = AssetDatabase.GUIDToAssetPath(m.Value);
            if (File.Exists(p)) roots.Add(p);
        }
        var used = new HashSet<string>(AssetDatabase.GetDependencies(roots.ToArray(), true));
        // Qualquer caminho literal de codigo protege a pasta inteira, inclusive carregamento GLTF por URL.
        foreach (var script in all.Where(p => p.EndsWith(".cs") && !p.EndsWith("/AssetCleanupAudit.cs")))
        foreach (Match m in Regex.Matches(File.ReadAllText(script), "Assets/[^\"\\r\\n]+"))
        {
            string path = m.Value.Replace('\\', '/');
            while (path.StartsWith("Assets/") && !Directory.Exists(path) && !File.Exists(path))
                path = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "";
            if (File.Exists(path)) foreach (var dependency in AssetDatabase.GetDependencies(path, true)) used.Add(dependency);
            else if (path.TrimEnd('/') != "Assets" && Directory.Exists(path))
                foreach (var dependency in AssetDatabase.GetDependencies(all.Where(p => p.StartsWith(path.TrimEnd('/') + "/")).ToArray(), true)) used.Add(dependency);
        }
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            foreach (var p in AssetDatabase.GetDependencies(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).path, true)) used.Add(p);
        File.WriteAllLines("Logs/folders-used.txt", used.OrderBy(p => p));
        var unused = Directory.GetDirectories("Assets").Select(p => p.Replace('\\', '/'))
            .Where(dir => !used.Any(p => p.StartsWith(dir + "/"))).ToArray();
        File.WriteAllLines("Logs/folders-unused.txt", unused);
    }
    [MenuItem("Tools/Assets/Analisar assets sem uso")]
    public static void Analyze()
    {
        AnalyzeInternal(false);
    }
    static void AnalyzeInternal(bool packages)
    {
        var all = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") && File.Exists(p)).ToArray();
        var candidates = new HashSet<string>(new[] { ".png", ".jpg", ".jpeg", ".tga", ".psd", ".tif", ".tiff", ".exr", ".hdr", ".fbx", ".obj", ".blend", ".mat", ".prefab", ".anim", ".controller", ".overridecontroller", ".wav", ".mp3", ".ogg", ".mp4" });
        var roots = new HashSet<string>(all.Where(p => !candidates.Contains(Path.GetExtension(p).ToLowerInvariant()) || p.Contains("/Resources/") || p.StartsWith("Assets/StreamingAssets/") || !string.IsNullOrEmpty(AssetImporter.GetAtPath(p)?.assetBundleName)));
        if (packages)
        {
            var infrastructure = new HashSet<string>(new[] { ".cs", ".asmdef", ".asmref", ".dll", ".shader", ".shadergraph", ".shadersubgraph", ".hlsl", ".cginc", ".compute", ".rsp", ".uxml", ".uss" });
            roots = new HashSet<string>(all.Where(p => infrastructure.Contains(Path.GetExtension(p).ToLowerInvariant()) || p.Contains("/Resources/") || p.Contains("/Editor/") || p.StartsWith("Assets/Settings/") || p.StartsWith("Assets/StreamingAssets/") || !string.IsNullOrEmpty(AssetImporter.GetAtPath(p)?.assetBundleName)));
            foreach (var scene in EditorBuildSettings.scenes) roots.Add(scene.path);
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                roots.Add(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).path);
            // Importadores podem resolver arquivos auxiliares por nome, sem registrar GUIDs.
            // Nao remover modelos, materiais, texturas ou arquivos-fonte nesta limpeza.
            var removable = new HashSet<string>(new[] { ".unity", ".prefab", ".anim", ".controller", ".overridecontroller" });
            foreach (var path in all)
                if (!removable.Contains(Path.GetExtension(path).ToLowerInvariant()) ||
                    path.StartsWith("Assets/Baus/") || path.StartsWith("Assets/caldeirao-de-bruxa-3d-blender/") ||
                    path.StartsWith("Assets/fase_gorila/") || path.StartsWith("Assets/nappin/")) roots.Add(path);
        }
        // Preserve complete directories referenced by editor/runtime code, including dynamically built paths.
        foreach (var script in all.Where(p => p.EndsWith(".cs") && !p.EndsWith("/AssetCleanupAudit.cs")))
        foreach (Match m in Regex.Matches(File.ReadAllText(script), "Assets/[^\"\\r\\n]+"))
        {
            string path = m.Value.Replace('\\', '/');
            while (path.StartsWith("Assets/") && !Directory.Exists(path) && !File.Exists(path))
                path = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "";
            if (File.Exists(path)) roots.Add(path);
            else if (path.TrimEnd('/') != "Assets" && Directory.Exists(path)) foreach (var p in all.Where(p => p.StartsWith(path.TrimEnd('/') + "/"))) roots.Add(p);
        }
        foreach (var settings in Directory.GetFiles("ProjectSettings"))
        foreach (Match m in Regex.Matches(File.ReadAllText(settings), "[a-fA-F0-9]{32}"))
        {
            string path = AssetDatabase.GUIDToAssetPath(m.Value);
            if (File.Exists(path)) roots.Add(path);
        }
        var used = new HashSet<string>(AssetDatabase.GetDependencies(roots.ToArray(), true));
        var unused = all.Where(p => !roots.Contains(p) && !used.Contains(p)).OrderBy(p => p).ToArray();
        string prefix = packages ? "Logs/safe-cleanup" : "Logs/assets";
        File.WriteAllLines(prefix + "-unused.txt", unused);
        File.WriteAllLines(prefix + "-preserved.txt", used.OrderBy(p => p));
        if (packages)
        {
            var report = new List<string>();
            var targets = new[] { "Assets/Baus/Prefabs/Radio.prefab", "Assets/Baus/Prefabs/Trumpet_01.prefab", "Assets/Baus/Prefabs/Lollipop_01.prefab" }
                .Concat(all.Where(p => p.StartsWith("Assets/caldeirao-de-bruxa-3d-blender/") && new[] { ".fbx", ".blend", ".obj", ".prefab" }.Contains(Path.GetExtension(p).ToLowerInvariant())));
            foreach (var path in targets)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!model) { report.Add("UNRESOLVED " + path); continue; }
                var renderers = model.GetComponentsInChildren<Renderer>(true);
                report.Add(path + " renderers=" + renderers.Length);
                foreach (var r in renderers)
                foreach (var material in r.sharedMaterials)
                {
                    if (!material) { report.Add("MISSING MATERIAL " + r.name); continue; }
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material, out string guid, out long id);
                    report.Add("material " + guid + ":" + id + " shader=" + (material.shader ? material.shader.name : "MISSING"));
                }
                foreach (var mesh in model.GetComponentsInChildren<MeshFilter>(true))
                    report.Add("mesh " + mesh.name + " vertices=" + (mesh.sharedMesh ? mesh.sharedMesh.vertexCount : -1));
            }
            File.WriteAllLines("Logs/safe-cleanup-items.txt", report);
        }
        Debug.Log("CLEANUP_AUDIT: " + unused.Length + " unused assets; " + unused.Sum(p => new FileInfo(p).Length) + " bytes");
    }
}
