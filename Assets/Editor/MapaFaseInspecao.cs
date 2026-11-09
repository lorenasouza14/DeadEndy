using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class MapaFaseInspecao
{
    static MapaFaseInspecao() { EditorApplication.update += InspecionarPendente; }
    static void InspecionarPendente() {
        if (!Application.isBatchMode && File.Exists("Logs/mapa-inspecao-pendente.txt") && !EditorApplication.isPlayingOrWillChangePlaymode)
        { File.Delete("Logs/mapa-inspecao-pendente.txt"); Executar(); }
    }
    [MenuItem("Tools/Mapa/Inspecionar fase")]
    public static void Executar()
    {
        if (Application.isBatchMode) EditorSceneManager.OpenScene(ProgressoJogo.CenaJogo);
        var sb = new StringBuilder();
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t.GetComponent<InventarioEndy>() != null || t.GetComponent<AlavancaTriangulo>() != null ||
                t.GetComponent<InteracaoBau>() != null || t.GetComponent<BauDiario>() != null ||
                t.GetComponent<GatilhoArena>() != null || t.GetComponent<GerenciadorPuzzleTriangulo>() != null ||
                t.GetComponent<CaranguejoChefe>() != null || t.name.ToLowerInvariant().Contains("parede") ||
                t.name.ToLowerInvariant().Contains("cave") || t.name.ToLowerInvariant().Contains("terrain"))
            {
                sb.AppendLine(t.name + " @ " + t.position.ToString("F2"));
                foreach (var c in t.GetComponents<Collider>()) sb.AppendLine("  collider " + c.bounds + " trigger=" + c.isTrigger);
            }
        }
        foreach (var terrain in Object.FindObjectsByType<Terrain>()) sb.AppendLine("Terrain size " + terrain.terrainData.size);
        File.WriteAllText("Logs/mapa-fase.txt", sb.ToString());
        var player = Object.FindAnyObjectByType<InventarioEndy>();
        var go = new GameObject("Map camera");
        var cam = go.AddComponent<Camera>();
        cam.transform.position = new Vector3(-20, 250, 70);
        cam.transform.rotation = Quaternion.Euler(90, 0, 0);
        cam.orthographic = true; cam.orthographicSize = 200; cam.farClipPlane = 1000;
        var rt = new RenderTexture(1600, 1600, 24); cam.targetTexture = rt;
        cam.Render(); RenderTexture.active = rt;
        var image = new Texture2D(1600,1600,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,1600,1600),0,0); image.Apply();
        File.WriteAllBytes("Logs/mapa-fase.png", image.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null;
        Object.DestroyImmediate(go); Object.DestroyImmediate(rt); Object.DestroyImmediate(image);
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}

