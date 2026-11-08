using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SopaCenaSetup
{
    private static T Componente<T>(GameObject go) where T : Component
    { var existente = go.GetComponent<T>(); return existente != null ? existente : Undo.AddComponent<T>(go); }
    private static Transform Ponto(Transform pai, string nome, Vector3 posicao)
    {
        var t = pai.Find(nome);
        if (t == null) { var go = new GameObject(nome); Undo.RegisterCreatedObjectUndo(go, "Configurar sopa"); t = go.transform; t.SetParent(pai, false); }
        Undo.RecordObject(t, "Posicionar ponto da sopa"); t.position = posicao; return t;
    }

    // Comeca abaixo do teto da caverna. Seleciona somente superficies caminhaveis.
    public static Vector3 Solo(Vector3 posicao, Transform ignorar = null)
    {
        var hits = Physics.RaycastAll(posicao + Vector3.up * 2f, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore)
            .Where(h => h.normal.y > 0.55f && (ignorar == null || !h.transform.IsChildOf(ignorar)) &&
                h.transform.GetComponentInParent<GorilaGuardiao>() == null && h.transform.GetComponentInParent<BananaSopa>() == null)
            .OrderBy(h => h.distance).ToArray();
        if (hits.Length == 0) throw new System.InvalidOperationException("Sem chao em " + posicao);
        return hits[0].point;
    }

    [MenuItem("Tools/Gorila/Sopa/Configurar fase atual %#&k")]
    public static void Configurar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var cena = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!cena.path.EndsWith("Demo_day_fase_gorilla.unity")) throw new System.InvalidOperationException("Abra a fase do gorila.");
        // Copia inclui alteracoes ainda nao salvas; nao reabre nem substitui a cena atual.
        Directory.CreateDirectory("Logs");
        EditorSceneManager.SaveScene(cena, "Logs/SopaAntesDaConfiguracao.unity", true);
        Undo.IncrementCurrentGroup(); int grupo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Configurar sopa na fase");
        var guarda = Object.FindAnyObjectByType<GorilaGuardiao>();
        var inv = Object.FindAnyObjectByType<InventarioEndy>();
        var mono = Object.FindAnyObjectByType<MonolitoDesmoronavel>();
        var frutas = Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include).Where(m => m.name.StartsWith("Banana do altar")).Select(m => m.gameObject).ToArray();
        if (frutas.Length != 3) throw new System.InvalidOperationException("Esperadas exatamente tres frutas da fase.");
        foreach (var fruta in frutas)
        {
            Selection.activeGameObject = fruta; SopaGorilaSetup.PrepararBanana();
            var b = fruta.GetComponent<BananaSopa>(); Undo.RecordObject(b, "Associar desafio da banana");
            b.gorilaQueProtege = Caminho(fruta.transform).Contains("Santuario na caverna") ? guarda : null;
            b.monolito = fruta.GetComponent<Rigidbody>() == mono.banana ? mono : null;
            b.tamanhoNaMao = 0.38f; b.posicaoNaMao = new Vector3(0, 0.015f, 0.04f); b.rotacaoNaMao = new Vector3(0, 90, 0);
            EditorUtility.SetDirty(b);
        }
        Undo.RecordObject(inv, "Associar mao do Endy");
        inv.encaixeMao = inv.GetComponentsInChildren<Transform>(true).Single(t => t.name == "handslot.r");
        EditorUtility.SetDirty(inv);
        var raiz = GameObject.Find("fase_gorila/caldeirao").transform;
        var panela = raiz.GetComponentsInChildren<Transform>().Single(t => t.name.Trim() == "Caldeirão completo");
        var sopa = Componente<CaldeiraoSopa>(raiz.gameObject); Componente<DicaInteracao>(raiz.gameObject);
        Undo.RecordObject(sopa, "Associar receita e deposito");
        sopa.bananasDaReceita = frutas.Select(f => f.GetComponent<BananaSopa>()).ToArray(); sopa.jogador = inv;
        sopa.pontoInteracao = Ponto(raiz, "Ponto de deposito", panela.position + new Vector3(0, 0.35f, -1.15f));
        sopa.alcanceDeposito = 2.5f;
        var colPanela = Componente<CapsuleCollider>(panela.gameObject);
        Undo.RecordObject(colPanela, "Colisao da panela"); colPanela.center = new Vector3(0, -0.15f, 0); colPanela.radius = 0.96f; colPanela.height = 1.45f;
        var gorila = Componente<GorilaSopa>(guarda.gameObject); Undo.RecordObject(gorila, "Associar percurso"); sopa.gorila = gorila;
        gorila.velocidade = 1.5f; gorila.velocidadeGiro = 135f;
        gorila.olharParaSopa = Ponto(raiz, "Centro da sopa", panela.position + Vector3.up * 0.5f);
        var percurso = Ponto(raiz.parent, "Percurso do gorila ate a sopa", Vector3.zero);
        var marcos = new[] { guarda.transform.position, new Vector3(16.8f,8,131), new Vector3(18,7,128), new Vector3(18,7,124), new Vector3(18,7,120), new Vector3(18,7,118.1f) };
        var pontos = new System.Collections.Generic.List<Transform>();
        for (int i = 1; i < marcos.Length; i++)
        {
            int passos = Mathf.CeilToInt(Vector3.Distance(marcos[i-1], marcos[i]) / 0.65f);
            for (int j = 1; j <= passos; j++)
            {
                var pos = Solo(Vector3.Lerp(marcos[i-1], marcos[i], (float)j / passos)) + Vector3.up * 0.05f;
                pontos.Add(Ponto(percurso, "Ponto " + (pontos.Count + 1).ToString("00"), pos));
            }
        }
        gorila.percurso = pontos.ToArray();
        var barreira = GameObject.Find("fase_gorila/entrada_caverna/fundo_entrada_caverna");
        gorila.bloqueiosDaCaverna = barreira.GetComponents<Collider>();
        gorila.visuaisDosBloqueios = barreira.GetComponents<Renderer>();
        // O corpo acompanha o modelo em movimento; a animacao nao desloca a raiz.
        guarda.animGorila.applyRootMotion = false;
        var vaporT = Ponto(raiz, "Vapor da sopa pronta", panela.position + Vector3.up * 0.6f);
        var vapor = Componente<ParticleSystem>(vaporT.gameObject); vapor.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var principal = vapor.main; principal.playOnAwake = false; principal.loop = true; principal.duration = 3; principal.startLifetime = new ParticleSystem.MinMaxCurve(1.7f, 2.5f);
        principal.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.6f); principal.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
        principal.startColor = new Color(0.94f, 0.92f, 0.83f, 0.25f); principal.maxParticles = 35; principal.simulationSpace = ParticleSystemSimulationSpace.World;
        var emissao = vapor.emission; emissao.rateOverTime = 7;
        var forma = vapor.shape; forma.shapeType = ParticleSystemShapeType.Cone; forma.angle = 12; forma.radius = 0.4f; forma.rotation = new Vector3(-90,0,0);
        var cor = vapor.colorOverLifetime; cor.enabled = true; var gradiente = new Gradient(); gradiente.SetKeys(new[]{new GradientColorKey(Color.white,0), new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,0.2f),new GradientAlphaKey(0,1)}); cor.color = gradiente;
        var tamanho = vapor.sizeOverLifetime; tamanho.enabled = true; tamanho.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0,0.65f,1,1.8f));
        const string materialPath = "Assets/fase_gorila/materials/VaporSopa.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(material, materialPath); }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/PurePoly/Mining_Pack/Textures/FX Textures/PP_FX_Smoke.png"));
        material.SetColor("_BaseColor", Color.white); material.SetFloat("_Surface",1); material.SetFloat("_Blend",0); material.SetFloat("_SrcBlend",5); material.SetFloat("_DstBlend",10); material.SetFloat("_ZWrite",0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000; material.SetOverrideTag("RenderType","Transparent");
        vapor.GetComponent<ParticleSystemRenderer>().sharedMaterial = material; sopa.vaporSopa = vapor;
        EditorUtility.SetDirty(material); EditorUtility.SetDirty(gorila); EditorUtility.SetDirty(sopa);
        Undo.CollapseUndoOperations(grupo); EditorSceneManager.MarkSceneDirty(cena); EditorSceneManager.SaveScene(cena); AssetDatabase.SaveAssets();
        Selection.activeGameObject = raiz.gameObject;
        SceneView.lastActiveSceneView.LookAt(new Vector3(17,8,124), Quaternion.Euler(30,0,0), 20);
        Debug.Log("SOPA: tres bananas, mao, receita, vapor, percurso e bloqueio configurados e salvos.");
    }
    public static string Caminho(Transform t) => t.parent == null ? t.name : Caminho(t.parent) + "/" + t.name;
    [MenuItem("Tools/Gorila/Sopa/Inspecionar cena %#&j")]
    public static void Inspecionar()
    {
        var b = new StringBuilder();
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (!t.gameObject.scene.IsValid()) continue;
            string p = Caminho(t);
            if (!(p.Contains("fase_gorila") || p.Contains("desafio_alinhar_totens") || t.name.StartsWith("Banana do altar") || t.GetComponent<InventarioEndy>() != null || t.name == "handslot.r")) continue;
            if (t.name.Contains("Banana") || t.GetComponent<GorilaGuardiao>() || t.name.ToLower().Contains("calde") ||
                p.ToLower().Contains("calde") || t.GetComponent<InventarioEndy>() || t.name == "handslot.r" ||
                p.Contains("entrada_caverna") || p.Contains("GorillaA") || t.name.Contains("monolito"))
            {
                b.AppendLine($"OBJ {p} active={t.gameObject.activeSelf} pos={t.position:F3} rot={t.eulerAngles:F2} scale={t.lossyScale:F3}");
                foreach (var c in t.GetComponents<Component>())
                {
                    b.AppendLine(" COMP " + c.GetType().Name);
                    if (c is Renderer r) b.AppendLine($" BOUNDS center={r.bounds.center:F3} size={r.bounds.size:F3}");
                    if (c is Collider col) b.AppendLine($" COL enabled={col.enabled} trigger={col.isTrigger} center={col.bounds.center:F3} size={col.bounds.size:F3}");
                    if (c is InventarioEndy inv) b.AppendLine($" HAND {inv.encaixeMao} example={inv.pocaoNaMao3D}");
                }
            }
        }
        Directory.CreateDirectory("Logs");
        for (float z = 114; z <= 144; z += 2)
        for (float x = 13; x <= 21; x += 2)
        {
            var hits = Physics.RaycastAll(new Vector3(x, 18, z), Vector3.down, 20, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => !Caminho(h.transform).Contains("GorillaA") && !Caminho(h.transform).Contains("caldeirao")).OrderBy(h => h.distance);
            b.AppendLine($"GROUND {x},{z}: " + string.Join(" | ", hits.Take(3).Select(h => $"{Caminho(h.transform)} y={h.point.y:F3} n={h.normal:F2}")));
        }
        File.WriteAllText("Logs/sopa-cena-inspecao.txt", b.ToString());
        Debug.Log("Inspecao da sopa gravada.");
    }
}
