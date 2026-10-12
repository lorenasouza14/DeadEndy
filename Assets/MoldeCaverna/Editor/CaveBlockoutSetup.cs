using UnityEngine;
using UnityEditor;
using System.IO;

public static class CaveBlockoutSetup
{
    [MenuItem("Tools/Caverna/Criar molde na cena")]
    public static void Create()
    {
        string rootPath = null;
        foreach (var guid in AssetDatabase.FindAssets("CaveBlockoutSetup t:MonoScript"))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileName(p) == "CaveBlockoutSetup.cs")
                rootPath = Path.GetDirectoryName(Path.GetDirectoryName(p)).Replace('\\', '/');
        }
        if (rootPath == null) { Debug.LogError("Pasta do molde nao encontrada."); return; }
        var names = new[] { "Cave_Floor", "Cave_Walls", "Cave_Roof" };
        foreach (var name in names)
        {
            var path = rootPath + "/Meshes/" + name + ".obj";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) { Debug.LogError("Modelo ausente: " + path); return; }
            importer.globalScale = 1;
            importer.importNormals = ModelImporterNormals.Calculate;
            importer.normalSmoothingAngle = 65;
            importer.SaveAndReimport();
        }
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("HDRP/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) { Debug.LogError("Shader de material nao encontrado."); return; }
        var root = new GameObject("Molde Caverna");
        Undo.RegisterCreatedObjectUndo(root, "Criar molde da caverna");
        var colors = new[] { new Color(.43f,.47f,.50f), new Color(.29f,.33f,.37f), new Color(.24f,.27f,.30f) };
        for (int i=0; i<names.Length; i++)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(rootPath + "/Meshes/" + names[i] + ".obj");
            var part = (GameObject)PrefabUtility.InstantiatePrefab(model);
            part.transform.SetParent(root.transform, false);
            string materialPath = rootPath + "/" + names[i] + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (mat == null)
            {
                mat = new Material(shader);
                mat.color = colors[i];
                AssetDatabase.CreateAsset(mat, materialPath);
            }
            foreach (var renderer in part.GetComponentsInChildren<MeshRenderer>()) renderer.sharedMaterial = mat;
            foreach (var mesh in part.GetComponentsInChildren<MeshFilter>())
            {
                var collider = mesh.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh.sharedMesh;
            }
            if (i == 2) part.SetActive(false);
        }
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        Debug.Log("Molde criado: piso e paredes com colisao. Ative Cave_Roof se quiser o teto. Use Ctrl+Z para desfazer.");
    }
}
