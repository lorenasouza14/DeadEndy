using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class AguaValidation
{
    static AguaValidation() { EditorApplication.playModeStateChanged += Changed; }
    [MenuItem("Tools/Agua/Validar margem e investidas")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool("AguaValidation.Pending", true);
        EditorApplication.EnterPlaymode();
    }
    static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("AguaValidation.Pending", false)) return;
        SessionState.SetBool("AguaValidation.Pending", false);
        new GameObject("Water validation").AddComponent<AguaValidationRunner>().Run();
    }
    public static void Finish(Exception error)
    {
        Directory.CreateDirectory("Logs");
        string result = error == null ? "PASS: shoreline walls, player movement, jump, shrink and boss charges" : "FAIL: " + error;
        File.WriteAllText("Logs/water-validation.txt", result);
        Debug.Log(result);
        EditorApplication.ExitPlaymode();
    }
}

public class AguaValidationRunner : MonoBehaviour
{
    public void Run() { StartCoroutine(Guard()); }
    IEnumerator Guard()
    {
        IEnumerator checks = Checks();
        while (true)
        {
            object wait;
            try
            {
                if (!checks.MoveNext()) { AguaValidation.Finish(null); yield break; }
                wait = checks.Current;
            }
            catch (Exception error) { AguaValidation.Finish(error); yield break; }
            yield return wait;
        }
    }
    static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        Debug.Log("WATER PASS: " + message);
    }

    IEnumerator Checks()
    {
        yield return null;
        var inventory = Object.FindAnyObjectByType<InventarioEndy>();
        var bosses = Object.FindObjectsByType<CaranguejoChefe>(FindObjectsInactive.Include);
        Check(inventory != null && bosses.Length > 0, "Cena contém Endy e chefe");
        var wall = BarreiraDaAgua.Garantir(inventory.gameObject.scene);
        Check(wall != null && wall.Trechos.Count > 100, "Margem gerada: " + (wall != null ? wall.Trechos.Count : 0) + " trechos");
        Check(BarreiraDaAgua.Garantir(inventory.gameObject.scene) == wall, "Não duplica barreiras ao inicializar novamente");
        Check(wall.GetComponentsInChildren<Renderer>().Length == 0, "Barreira é invisível");
        var boss = bosses[0];
        Vector3 arena = boss.transform.position;
        var candidates = new List<BarreiraDaAgua.Trecho>(wall.Trechos);
        candidates.Sort((a, b) => (a.inicio - arena).sqrMagnitude.CompareTo((b.inicio - arena).sqrMagnitude));
        var samples = new List<BarreiraDaAgua.Trecho>();
        foreach (var t in candidates)
        {
            Vector3 mid = (t.inicio + t.fim) * 0.5f;
            if (wall.EstaSobreAgua(mid + t.direcaoTerra * 5) || !wall.EstaSobreAgua(mid - t.direcaoTerra * 3)) continue;
            bool close = false;
            foreach (var prior in samples) if (Vector3.Distance(prior.inicio, t.inicio) < 12) close = true;
            if (close) continue;
            samples.Add(t);
            if (samples.Count == 4) break;
        }
        Check(samples.Count == 4, "Quatro trechos distintos próximos da arena selecionados");
        var meshColliders = wall.GetComponentsInChildren<MeshCollider>();
        int verified = 0;
        for (int i = 0; i < wall.Trechos.Count; i += Math.Max(1, wall.Trechos.Count / 400))
        {
            var t = wall.Trechos[i];
            Vector3 mid = (t.inicio + t.fim) * 0.5f;
            mid.y = wall.AlturaTopo - 3;
            bool hit = false;
            foreach (var col in meshColliders)
                if (col.Raycast(new Ray(mid + t.direcaoTerra, -t.direcaoTerra), out _, 2)) hit = true;
            Check(hit, "Colisão cobre trecho " + i);
            verified++;
        }
        Debug.Log("WATER coverage: " + verified);
        var player = inventory.transform;
        var cc = inventory.GetComponent<CharacterController>();
        inventory.GetComponent<StarterAssets.ThirdPersonController>().enabled = false;
        inventory.GetComponent<VidaEndy>().enabled = false;
        Vector3 scale = player.localScale;
        foreach (var sample in samples)
        {
            Vector3 mid = (sample.inicio + sample.fim) * 0.5f;
            foreach (float factor in new[] { 1f, 0.45f })
            {
                cc.enabled = false;
                player.localScale = scale * factor;
                player.position = mid + sample.direcaoTerra * 4 + Vector3.up * 2;
                cc.enabled = true;
                Physics.SyncTransforms();
                for (int step = 0; step < 20; step++) cc.Move(-sample.direcaoTerra * 0.8f);
                Check(!wall.EstaSobreAgua(player.position), "Endy não atravessa correndo/pulando; escala " + factor);
            }
        }
        cc.enabled = false;
        player.localScale = scale;
        player.position = arena + Vector3.up * 100;
        boss.gameObject.SetActive(true);
        boss.StopAllCoroutines();
        boss.prefabAreia = null;
        var rb = boss.GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        var target = new GameObject("Water charge target");
        boss.alvoEndy = target.transform;
        foreach (var sample in samples)
        {
            boss.StopAllCoroutines();
            Vector3 mid = (sample.inicio + sample.fim) * 0.5f;
            float radius = 1;
            foreach (var col in boss.GetComponentsInChildren<Collider>())
                radius = Mathf.Max(radius, col.bounds.extents.x, col.bounds.extents.z);
            float height = Mathf.Min(wall.AlturaTopo - 8, mid.y + 5);
            Vector3 start = mid + sample.direcaoTerra * (radius + 5);
            start.y = height;
            rb.position = start;
            rb.linearVelocity = Vector3.zero;
            target.transform.position = mid - sample.direcaoTerra * 20;
            target.transform.position = new Vector3(target.transform.position.x, height, target.transform.position.z);
            Physics.SyncTransforms();
            int stunned = boss.NumeroAtordoamento;
            boss.velInvestida = 100;
            boss.InvestidaPunitiva(false);
            for (int step = 0; step < 20; step++) yield return new WaitForFixedUpdate();
            Check(!wall.EstaSobreAgua(boss.transform.position), "Chefe não atravessa a margem em investida a 100 m/s");
            Check(boss.estadoAtual != CaranguejoChefe.EstadoChefe.Investindo && boss.NumeroAtordoamento == stunned,
                "Margem interrompe investida sem conceder atordoamento");
        }
        boss.TomarPocaoEncolhedora();
        Check(wall.gameObject.activeInHierarchy && meshColliders[0].enabled, "Margem continua ativa após derrotar chefe");
        SaveMap(wall, inventory.gameObject.scene);
    }

    static void SaveMap(BarreiraDaAgua wall, UnityEngine.SceneManagement.Scene scene)
    {
        Terrain terrain = null;
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponentInChildren<Terrain>() != null) { terrain = root.GetComponentInChildren<Terrain>(); break; }
        if (terrain == null) return;
        const int n = 768;
        var texture = new Texture2D(n, n, TextureFormat.RGB24, false);
        var colors = new Color[n * n];
        var origin = terrain.transform.position;
        var size = terrain.terrainData.size;
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                var p = origin + new Vector3(size.x * x / (n - 1), 0, size.z * z / (n - 1));
                colors[z * n + x] = wall.EstaSobreAgua(p) ? new Color(0.1f, 0.42f, 0.65f) : new Color(0.65f, 0.7f, 0.4f);
            }
        foreach (var t in wall.Trechos)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt((t.inicio.x - origin.x) / size.x * (n - 1)), 0, n - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt((t.inicio.z - origin.z) / size.z * (n - 1)), 0, n - 1);
            colors[z * n + x] = Color.red;
        }
        texture.SetPixels(colors);
        texture.Apply();
        Directory.CreateDirectory("Logs");
        File.WriteAllBytes("Logs/water-boundary-map.png", texture.EncodeToPNG());
        Object.Destroy(texture);
    }
}
