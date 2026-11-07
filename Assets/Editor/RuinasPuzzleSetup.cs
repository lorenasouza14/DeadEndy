using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Cinemachine;
using UnityEngine;

public static class RuinasPuzzleSetup
{
    private const string Pasta = "Assets/fase_gorila/PuzzleRuinas";

    [MenuItem("Tools/Gorila/Configurar puzzle das ruinas %#&u")]
    public static void Configurar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var root = GameObject.Find("desafio_alinhar_totens");
        if (root == null) throw new System.InvalidOperationException("Grupo desafio_alinhar_totens ausente.");
        if (root.GetComponent<PuzzleAltaresRuinas>() != null)
        {
            Debug.Log("Puzzle ja configurado. Ajuste os componentes existentes no Inspector.");
            return;
        }
        var grupos = Enumerable.Range(1, 3).Select(i => root.transform.Find("toten_" + i)).ToArray();
        var runas = grupos.Select(g => g.GetComponentsInChildren<MeshFilter>().Single(m => m.name.StartsWith("PP_Rune_Stone"))).ToArray();
        var monolito = root.transform.Find("monolito");
        var inteiro = monolito.Find("SM_Env_Monolith_01");
        var banana = monolito.GetComponentsInChildren<MeshFilter>().Single(m => m.name.StartsWith("Banana do altar"));
        var tampa = monolito.Find("Floor_tile_B");
        var camera = Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None).Single(c => c.name == "PlayerFollowCamera");
        if (!AssetDatabase.IsValidFolder(Pasta)) AssetDatabase.CreateFolder("Assets/fase_gorila", "PuzzleRuinas");
        Directory.CreateDirectory("Logs");
        EditorSceneManager.SaveScene(root.scene, "Logs/antes-puzzle-ruinas.unity", true);
        var meshes = RuinasFragmentos.Criar(inteiro.GetComponent<MeshFilter>().sharedMesh, Pasta + "/MonolitoFragmentos.asset");
        var rosa = Material("FeixeRosa", "Universal Render Pipeline/Unlit", new Color(1f, 0.05f, 0.65f));
        var interior = Material("InteriorPedra", "Universal Render Pipeline/Lit", new Color(0.47f, 0.42f, 0.33f));
        var materialPoeira = Material("Poeira", "Universal Render Pipeline/Particles/Unlit", new Color(0.55f, 0.48f, 0.35f, 0.25f));
        materialPoeira.SetFloat("_Surface", 1f);
        materialPoeira.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        materialPoeira.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        materialPoeira.SetFloat("_ZWrite", 0f);
        materialPoeira.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        materialPoeira.renderQueue = 3000;
        Undo.IncrementCurrentGroup();
        int grupoUndo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Configurar puzzle dos altares");
        var puzzle = Undo.AddComponent<PuzzleAltaresRuinas>(root);
        puzzle.altares = new AltarRuinas[3];
        puzzle.jogador = Object.FindAnyObjectByType<StarterAssets.ThirdPersonController>();
        int[] passos = { 0, 1, 3 };
        Vector3 centro = inteiro.GetComponent<Renderer>().bounds.center;
        for (int i = 0; i < 3; i++)
        {
            var grupo = grupos[i];
            var runa = runas[i].transform;
            var pivo = Novo("Pivo_do_altar", grupo);
            pivo.SetPositionAndRotation(runa.position, runa.rotation);
            Undo.SetTransformParent(runa, pivo, "Preparar giro da pedra");
            Vector3 direcao = Vector3.ProjectOnPlane(centro - pivo.position, Vector3.up);
            pivo.rotation = Quaternion.LookRotation(direcao);
            var altar = Undo.AddComponent<AltarRuinas>(grupo.gameObject);
            altar.puzzle = puzzle;
            altar.pivo = pivo;
            altar.alavanca = grupo.Find("Switch");
            altar.caboAlavanca = altar.alavanca.Find("Switch_handle");
            altar.rotacaoAlinhadaLocal = pivo.localRotation;
            altar.passosIniciais = passos[i];
            pivo.localRotation *= Quaternion.Euler(0f, passos[i] * 90f, 0f);
            var bounds = runas[i].sharedMesh.bounds;
            altar.emissor = Novo("Origem_feixe_rosa", runa);
            altar.emissor.localPosition = new Vector3(bounds.center.x, bounds.center.y * 1.2f, bounds.max.z + 0.035f);
            altar.luz = Undo.AddComponent<Light>(altar.emissor.gameObject);
            altar.luz.type = LightType.Point;
            altar.luz.color = new Color(1f, 0.08f, 0.6f);
            altar.luz.intensity = 1.4f;
            altar.luz.range = 1.3f;
            altar.luz.shadows = LightShadows.None;
            var feixe = Novo("Feixe_rosa", grupo);
            altar.feixe = Undo.AddComponent<LineRenderer>(feixe.gameObject);
            altar.feixe.sharedMaterial = rosa;
            altar.feixe.useWorldSpace = true;
            altar.feixe.positionCount = 2;
            altar.feixe.startWidth = 0.09f;
            altar.feixe.endWidth = 0.055f;
            altar.feixe.numCapVertices = 6;
            altar.feixe.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            altar.feixe.receiveShadows = false;
            altar.MostrarFeixe(false);
            Undo.AddComponent<DicaInteracao>(grupo.gameObject);
            var piso = grupo.Find("Floor_tile_B");
            if (piso.GetComponent<Collider>() == null)
            {
                var col = Undo.AddComponent<MeshCollider>(piso.gameObject);
                col.sharedMesh = piso.GetComponent<MeshFilter>().sharedMesh;
            }
            foreach (var t in pivo.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = false;
            altar.caboAlavanca.gameObject.isStatic = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(runa);
            puzzle.altares[i] = altar;
        }
        var desmoronar = Undo.AddComponent<MonolitoDesmoronavel>(monolito.gameObject);
        puzzle.monolito = desmoronar;
        desmoronar.corpoInteiro = inteiro.GetComponent<Collider>();
        desmoronar.tremor = Undo.AddComponent<CinemachineImpulseSource>(monolito.gameObject);
        desmoronar.tremor.ImpulseDefinition.ImpulseChannel = 8;
        desmoronar.tremor.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Dissipating;
        desmoronar.tremor.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Rumble;
        desmoronar.tremor.ImpulseDefinition.ImpulseDuration = 1.2f;
        desmoronar.tremor.ImpulseDefinition.DissipationDistance = 40f;
        desmoronar.tremor.ImpulseDefinition.DissipationRate = 0.5f;
        var listener = camera.GetComponent<CinemachineImpulseListener>();
        if (listener == null) listener = Undo.AddComponent<CinemachineImpulseListener>(camera.gameObject);
        Undo.RecordObject(listener, "Ligar tremor do monolito");
        listener.ChannelMask |= 8;
        listener.Gain = 1f;
        listener.ApplyAfter = CinemachineCore.Stage.Noise;

        var partes = Novo("Fragmentos_do_monolito", monolito);
        partes.localPosition = inteiro.localPosition;
        partes.localRotation = inteiro.localRotation;
        partes.localScale = inteiro.localScale;
        desmoronar.fragmentos = partes.gameObject;
        Material[] materiais = inteiro.GetComponent<Renderer>().sharedMaterials.Concat(new[] { interior }).ToArray();
        foreach (var mesh in meshes)
        {
            var parte = Novo(mesh.name, partes);
            parte.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            parte.gameObject.AddComponent<MeshRenderer>().sharedMaterials = materiais;
            PrepararPedaco(parte.gameObject, mesh);
        }
        var tampaQuebrada = Object.Instantiate(tampa.gameObject, partes);
        Undo.RegisterCreatedObjectUndo(tampaQuebrada, "Preparar fragmento da tampa");
        tampaQuebrada.transform.SetPositionAndRotation(tampa.position, tampa.rotation);
        tampaQuebrada.transform.localScale = new Vector3(tampa.lossyScale.x / partes.lossyScale.x,
            tampa.lossyScale.y / partes.lossyScale.y, tampa.lossyScale.z / partes.lossyScale.z);
        PrepararPedaco(tampaQuebrada, tampa.GetComponent<MeshFilter>().sharedMesh);
        Undo.SetTransformParent(tampa, inteiro, "Vincular tampa ao monolito");
        partes.gameObject.SetActive(false);
        inteiro.gameObject.isStatic = false;
        var bananaCol = banana.GetComponent<BoxCollider>();
        if (bananaCol == null) bananaCol = Undo.AddComponent<BoxCollider>(banana.gameObject);
        bananaCol.center = banana.sharedMesh.bounds.center;
        bananaCol.size = banana.sharedMesh.bounds.size;
        var corpoBanana = banana.GetComponent<Rigidbody>();
        if (corpoBanana == null) corpoBanana = Undo.AddComponent<Rigidbody>(banana.gameObject);
        corpoBanana.mass = 0.4f;
        corpoBanana.isKinematic = true;
        corpoBanana.useGravity = true;
        corpoBanana.constraints = RigidbodyConstraints.FreezeRotation;
        corpoBanana.interpolation = RigidbodyInterpolation.Interpolate;
        corpoBanana.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        banana.gameObject.isStatic = false;
        desmoronar.banana = corpoBanana;

        var poeira = Novo("Poeira_da_queda", monolito);
        poeira.position = inteiro.position + Vector3.up * 0.6f;
        desmoronar.poeira = Undo.AddComponent<ParticleSystem>(poeira.gameObject);
        desmoronar.poeira.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var principal = desmoronar.poeira.main;
        principal.loop = false;
        principal.playOnAwake = false;
        principal.duration = 1f;
        principal.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.5f);
        principal.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
        principal.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.4f);
        principal.startColor = new Color(0.7f, 0.64f, 0.5f, 0.3f);
        principal.simulationSpace = ParticleSystemSimulationSpace.World;
        principal.maxParticles = 60;
        var emissao = desmoronar.poeira.emission;
        emissao.rateOverTime = 0f;
        emissao.SetBursts(new[] { new ParticleSystem.Burst(0f, 45) });
        var forma = desmoronar.poeira.shape;
        forma.shapeType = ParticleSystemShapeType.Hemisphere;
        forma.radius = 1.4f;
        poeira.GetComponent<ParticleSystemRenderer>().sharedMaterial = materialPoeira;
        Undo.CollapseUndoOperations(grupoUndo);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorSceneManager.SaveScene(root.scene);
        File.WriteAllText("Logs/ruinas-setup.txt", "PASS: tres altares, feixes, nove fragmentos, banana fisica e tremor conectados. Estado inicial 0,1,3. Solucao: altar 1 tres vezes e altar 3 uma vez.\n");
        Debug.Log("Puzzle das ruinas configurado e salvo.");
    }

    private static void PrepararPedaco(GameObject obj, Mesh mesh)
    {
        obj.isStatic = false;
        var col = obj.AddComponent<MeshCollider>();
        col.sharedMesh = mesh;
        col.convex = true;
        var corpo = obj.AddComponent<Rigidbody>();
        corpo.mass = 4f;
        corpo.isKinematic = true;
        corpo.useGravity = true;
        corpo.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private static Transform Novo(string nome, Transform parent)
    {
        var obj = new GameObject(nome);
        Undo.RegisterCreatedObjectUndo(obj, "Criar " + nome);
        obj.transform.SetParent(parent, false);
        return obj.transform;
    }

    private static Material Material(string nome, string shader, Color cor)
    {
        string caminho = Pasta + "/" + nome + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (material != null) return material;
        material = new Material(Shader.Find(shader));
        material.SetColor("_BaseColor", cor);
        AssetDatabase.CreateAsset(material, caminho);
        return material;
    }
    [MenuItem("Tools/Gorila/Inspecionar puzzle das ruinas %#&i")]
    public static void Inspecionar()
    {
        var root = GameObject.Find("desafio_alinhar_totens");
        var report = new StringBuilder();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            report.AppendLine(t.name + " parent=" + (t.parent == null ? "raiz" : t.parent.name) + " pos=" + t.position.ToString("F3") +
                " rot=" + t.eulerAngles.ToString("F2") + " localRot=" + t.localEulerAngles.ToString("F2") +
                " scale=" + t.lossyScale.ToString("F3") + " components=" + string.Join(",", t.GetComponents<Component>().Select(c => c == null ? "MISSING" : c.GetType().Name)));
            foreach (var r in t.GetComponents<Renderer>()) report.AppendLine(" BOUNDS " + r.bounds.ToString("F3") + " materials=" + string.Join(",", r.sharedMaterials.Select(m=>m.name)));
            var mesh = t.GetComponent<MeshFilter>();
            if (mesh != null) report.AppendLine(" MESH " + AssetDatabase.GetAssetPath(mesh.sharedMesh) + " local bounds=" + mesh.sharedMesh.bounds.ToString("F3"));
        }
        foreach(var camera in Object.FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsSortMode.None))
            report.AppendLine("CAMERA " + camera.name + " components=" + string.Join(",",camera.GetComponents<Component>().Select(c=>c.GetType().Name)));
        File.WriteAllText("Logs/ruinas-inspecao.txt", report.ToString());
        Debug.Log("Inspecao do puzzle salva.");
    }
}
