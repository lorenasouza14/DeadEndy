using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ArmadilhasRampaSetup
{
    public const string NomePedra = "SM_Env_Rock_Ramp_02_fase_groilla";
    private const string PastaMateriais = "Assets/fase_gorila/materials";
    private const string PrefabMola = "Assets/PACKAGES/TRAPS AND OBSTACLES/Traps-Obstacles/Prefabs/Animated/Spring Trap.prefab";
    private const string PrefabBanana = "Assets/fase_gorila/SM_Fruits_Banana_Y1.prefab";

    [MenuItem("Tools/Gorila/Configurar armadilhas da rampa %#&t")]
    public static void Configurar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var pedra = GameObject.Find(NomePedra);
        if (pedra == null) throw new InvalidOperationException("Abra a cena do gorila com a pedra da fase.");
        var cena = pedra.scene;
        var moitas = pedra.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name.StartsWith("Moita armadilha ", StringComparison.Ordinal)).ToArray();
        var altar = pedra.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Altar_round_B");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabMola);
        var bananaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabBanana);
        if (moitas.Length == 0 || prefab == null || bananaPrefab == null)
            throw new InvalidOperationException("Faltam moitas ou prefabs para configurar a rampa.");

        Directory.CreateDirectory("Logs");
        if (!File.Exists("Logs/antes-das-spring-traps.unity"))
            EditorSceneManager.SaveScene(cena, "Logs/antes-das-spring-traps.unity", true);
        Undo.IncrementCurrentGroup();
        int grupo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Configurar armadilhas e banana da rampa");
        Physics.SyncTransforms();

        var destino = pedra.transform.parent.Find("Ponto_queda_traps");
        if (destino == null)
        {
            destino = new GameObject("Ponto_queda_traps").transform;
            Undo.RegisterCreatedObjectUndo(destino.gameObject, "Criar ponto de queda");
            destino.SetParent(pedra.transform.parent, true);
            var origem = new Vector3(-66.136f, 50f, 210.846f);
            var solo = Physics.RaycastAll(origem, Vector3.down, 100f, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => h.collider is TerrainCollider).OrderBy(h => h.distance).First();
            destino.position = solo.point;
        }

        foreach (var moita in moitas)
        {
            var existente = moita.Find("Spring Trap");
            var mola = existente != null ? existente.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(prefab, cena);
            if (existente == null)
            {
                Undo.RegisterCreatedObjectUndo(mola, "Instalar Spring Trap");
                mola.name = "Spring Trap";
                mola.transform.SetParent(moita, true);
                var rocha = pedra.GetComponent<MeshCollider>();
                if (!rocha.Raycast(new Ray(moita.position + Vector3.up * 30f, Vector3.down), out var hit, 60f))
                    throw new InvalidOperationException("Moita fora da superficie da rampa: " + moita.name);
                mola.transform.SetPositionAndRotation(hit.point - hit.normal * 0.08f,
                    Quaternion.FromToRotation(Vector3.up, hit.normal));
                mola.transform.localScale = Vector3.one * 1.5f;
            }

            // A madeira antiga continua como pista visual, com o chao da pedra sustentando o Endy.
            foreach (var col in moita.GetComponentsInChildren<Collider>(true))
            {
                if (col.transform.IsChildOf(mola.transform)) continue;
                Undo.RecordObject(col, "Ajustar colisao da decoracao");
                col.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(col);
            }
            var trigger = mola.GetComponent<BoxCollider>();
            if (trigger == null) trigger = Undo.AddComponent<BoxCollider>(mola);
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.2f, 0f);
            trigger.size = new Vector3(1.1f, 0.65f, 1.1f);
            var corpo = mola.GetComponent<Rigidbody>();
            if (corpo == null) corpo = Undo.AddComponent<Rigidbody>(mola);
            corpo.isKinematic = true;
            corpo.useGravity = false;
            var armadilha = mola.GetComponent<ArmadilhaMola>();
            if (armadilha == null) armadilha = Undo.AddComponent<ArmadilhaMola>(mola);
            armadilha.animador = mola.GetComponent<Animator>();
            armadilha.pontoDeQueda = destino;
            armadilha.alturaDoArco = 15f;
            AjustarMateriais(mola);
            PrefabUtility.RecordPrefabInstancePropertyModifications(mola.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(mola);
        }

        var banana = altar.Find("Banana do altar");
        if (banana == null)
        {
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(bananaPrefab, cena);
            Undo.RegisterCreatedObjectUndo(obj, "Colocar banana no altar");
            obj.name = "Banana do altar";
            banana = obj.transform;
            banana.SetParent(altar, true);
            banana.rotation = Quaternion.Euler(0f, 25f, 0f);
            banana.localScale = Vector3.one / altar.lossyScale.x;
            var bounds = obj.GetComponent<Renderer>().bounds;
            float fator = 0.85f / Mathf.Max(bounds.size.x, bounds.size.z);
            banana.localScale *= fator;
            bounds = obj.GetComponent<Renderer>().bounds;
            var mesa = altar.GetComponent<Renderer>().bounds;
            banana.position += new Vector3(mesa.center.x - bounds.center.x,
                mesa.max.y + 0.03f - bounds.min.y, mesa.center.z - bounds.center.z);
            AjustarMateriais(obj);
            PrefabUtility.RecordPrefabInstancePropertyModifications(banana);
            PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
        }
        var materialBanana = AssetDatabase.LoadAssetAtPath<Material>(PastaMateriais + "/golden_banana.mat");
        if (materialBanana != null)
        {
            var renderer = banana.GetComponent<Renderer>();
            Undo.RecordObject(renderer, "Aplicar amarelo da banana");
            renderer.sharedMaterial = materialBanana;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
        AplicarCorDiscreta("Rampa_Color1_URP", new Color(0.28f, 0.19f, 0.09f));
        AplicarCorDiscreta("Rampa_Color2_URP", new Color(0.16f, 0.19f, 0.12f));
        Undo.CollapseUndoOperations(grupo);
        EditorSceneManager.MarkSceneDirty(cena);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(cena);
        Selection.activeGameObject = banana.gameObject;
        SceneView.lastActiveSceneView?.LookAt(altar.position + Vector3.up, Quaternion.Euler(25f, 150f, 0f), 7f);
        File.WriteAllText("Logs/spring-setup.txt", $"PASS: {moitas.Length} armadilhas, banana no altar, destino {destino.position}.\n");
        Debug.Log($"Rampa salva com {moitas.Length} Spring Traps e banana sobre o altar.");
    }

    private static void AplicarCorDiscreta(string nome, Color cor)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(PastaMateriais + "/" + nome + ".mat");
        if (material == null) return;
        Undo.RecordObject(material, "Camuflar placa da armadilha");
        material.SetColor("_BaseColor", cor);
        EditorUtility.SetDirty(material);
    }

    private static void AjustarMateriais(GameObject obj)
    {
        foreach (var renderer in obj.GetComponentsInChildren<Renderer>(true))
        {
            Undo.RecordObject(renderer, "Ajustar materiais para URP");
            renderer.sharedMaterials = renderer.sharedMaterials.Select(MaterialURP).ToArray();
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
    }

    private static Material MaterialURP(Material original)
    {
        if (original == null || original.shader.name.StartsWith("Universal Render Pipeline/")) return original;
        string caminho = PastaMateriais + "/Rampa_" + original.name.Replace(" ", "_") + "_URP.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", original.HasProperty("_Color") ? original.GetColor("_Color") : Color.white);
        if (original.HasProperty("_MainTex")) material.SetTexture("_BaseMap", original.GetTexture("_MainTex"));
        material.SetFloat("_Smoothness", 0.15f);
        AssetDatabase.CreateAsset(material, caminho);
        return material;
    }
}
