using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class FantasmasPisoValidation
{
    const string Pedido = "Logs/validar-fantasmas-pendente.txt";
    static FantasmasPisoValidation() { EditorApplication.update += Pendente; }
    static void Pendente()
    {
        if (!File.Exists(Pedido) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Pedido);
        Inspecionar();
    }

    [MenuItem("Tools/Caverna/Inspecionar piso dos fantasmas")]
    public static void Inspecionar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var anterior = SceneManager.GetActiveScene();
        var cena = SceneManager.GetSceneByPath(ProgressoJogo.CenaCaverna);
        bool abriu = !cena.IsValid() || !cena.isLoaded;
        var report = new StringBuilder();
        RotasChaoFantasmas rotas = null;
        Collider[] externos = Array.Empty<Collider>();
        try
        {
            if (abriu) cena = EditorSceneManager.OpenScene(ProgressoJogo.CenaCaverna, OpenSceneMode.Additive);
            externos = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include)
                .Where(c => c.gameObject.scene != cena && c.enabled).ToArray();
            foreach (var col in externos) col.enabled = false;
            Physics.SyncTransforms();
            rotas = RotasChaoFantasmas.Obter(cena);
            var tetos = cena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshFilter>())
                .Where(m => m.sharedMesh && (m.name + m.sharedMesh.name).ToLowerInvariant().Contains("ground") && m.transform.up.y < -.5f).ToArray();
            report.AppendLine("PECAS DE CHAO INVERTIDAS USADAS COMO TETO: " + tetos.Length);
            var manager = cena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GerenciadorFantasmasCaverna>(true)).Single();
            var prefab = (GameObject)typeof(GerenciadorFantasmasCaverna).GetField("prefabDoFantasma", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(manager);
            foreach (var ponto in cena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name.StartsWith("SpawnFantasma")).OrderBy(t => t.name))
            {
                report.AppendLine(ponto.name + " pos=" + ponto.position.ToString("F3"));
                Verificar(rotas.Amostrar(ponto.position, 20f, out var piso), ponto.name + ": encontrou piso para nascer", report);
                report.AppendLine("ROTA pos=" + piso.ToString("F3"));
                var obj = UnityEngine.Object.Instantiate(prefab, ponto.position, ponto.rotation);
                SceneManager.MoveGameObjectToScene(obj, cena);
                try
                {
                    var ghost = obj.GetComponent<FantasmaCaverna>() ?? obj.AddComponent<FantasmaCaverna>();
                    typeof(FantasmaCaverna).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ghost, null);
                    typeof(FantasmaCaverna).GetField("rotas", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ghost, rotas);
                    float offset = (float)typeof(FantasmaCaverna).GetProperty("AlturaDeFlutuacao", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ghost);
                    obj.transform.position = piso + Vector3.up * offset;
                    ghost.Configurar(null, null, 1, 0, .35f);
                    float alturaInicial = obj.transform.position.y;
                    Verificar(Mathf.Abs(alturaInicial - offset - piso.y) <= .6f, ponto.name + ": apoio proximo do piso, sem salto para cobertura", report);
                    report.AppendLine("GHOST offset=" + offset + " pos=" + obj.transform.position.ToString("F3") + " scale=" + obj.transform.lossyScale);
                    for (int i = 0; i < 30; i++) typeof(FantasmaCaverna).GetMethod("MoverPeloChao", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(ghost, new object[] { Vector3.zero, 1f / 60f });
                    report.AppendLine("GHOST apos 30 frames parado=" + obj.transform.position.ToString("F3"));
                    Verificar(Mathf.Abs(obj.transform.position.y - alturaInicial) < .01f, ponto.name + ": altura estavel em 30 atualizacoes", report);
                    foreach (var hit in Physics.RaycastAll(piso + Vector3.up * 20, Vector3.down, 60,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                        if (hit.collider.gameObject.scene == cena)
                            report.AppendLine("PROJECTED HIT " + hit.collider.name + " y=" + hit.point.y.ToString("F3") + " normal=" + hit.normal.ToString("F3"));
                }
                finally { UnityEngine.Object.DestroyImmediate(obj); }
                foreach (var hit in Physics.RaycastAll(ponto.position + Vector3.up * 10, Vector3.down, 30,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                {
                    if (hit.collider.gameObject.scene != cena) continue;
                    var mesh = hit.collider.GetComponent<MeshFilter>();
                    report.AppendLine("HIT " + hit.collider.name + " y=" + hit.point.y.ToString("F3") + " normal=" + hit.normal.ToString("F3")
                        + " up=" + hit.transform.up.ToString("F3") + " mesh=" + (mesh && mesh.sharedMesh ? mesh.sharedMesh.name : "-")
                        + " pai=" + (hit.transform.parent ? hit.transform.parent.name : "-"));
                }
            }
            TestarTetoBaixo(report);
        }
        catch (Exception erro) { report.AppendLine("FAIL: " + erro); Debug.LogException(erro); }
        finally
        {
            if (rotas) UnityEngine.Object.DestroyImmediate(rotas.gameObject);
            foreach (var col in externos) if (col) col.enabled = true;
            if (abriu && cena.IsValid() && cena.isLoaded) EditorSceneManager.CloseScene(cena, true);
            if (anterior.IsValid() && anterior.isLoaded) SceneManager.SetActiveScene(anterior);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/fantasmas-piso-validation.txt", report.ToString());
        }
    }

    static void TestarTetoBaixo(StringBuilder report)
    {
        var teste = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var raiz = new GameObject("Teste isolado de apoio");
        SceneManager.MoveGameObjectToScene(raiz, teste);
        raiz.transform.position = new Vector3(10000, 0, 10000);
        RotasChaoFantasmas rotasTeste = null;
        try
        {
            var piso = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piso.name = "PP_Stone_Ground_piso";
            piso.transform.SetParent(raiz.transform, false);
            piso.transform.localPosition = new Vector3(0, -.25f, 0);
            piso.transform.localScale = new Vector3(10, .5f, 10);
            var teto = GameObject.CreatePrimitive(PrimitiveType.Cube);
            teto.name = "PP_Stone_Ground_cobertura";
            teto.transform.SetParent(raiz.transform, false);
            teto.transform.localPosition = new Vector3(0, 2.25f, 0);
            teto.transform.localScale = new Vector3(10, .5f, 10);
            teto.transform.localRotation = Quaternion.Euler(180, 0, 0);
            var go = new GameObject("Fantasma embaixo da cobertura");
            go.transform.SetParent(raiz.transform, false);
            go.transform.localPosition = Vector3.up * .7f;
            var fantasma = go.AddComponent<FantasmaCaverna>();
            Physics.SyncTransforms();
            Verificar(Apoio(fantasma, out float y) && Mathf.Abs(y) < .01f,
                "Teto baixo mantem apoio no chao y=0 (antes da correcao retornava y=2,5)", report);
            Verificar(RotasChaoFantasmas.EhCobertura(teto.transform) && !RotasChaoFantasmas.EhCobertura(piso.transform),
                "Distingue a mesma peca usada como piso e como teto", report);
            rotasTeste = RotasChaoFantasmas.Obter(teste);
            Verificar(rotasTeste.Amostrar(raiz.transform.position + Vector3.up * 2.3f, 1, out var apoio) && Mathf.Abs(apoio.y) < .01f,
                "Rota ignora teto invertido mesmo quando o marcador esta mais perto dele", report);
            Verificar(!rotasTeste.Amostrar(raiz.transform.position + Vector3.up * 30, 1, out _),
                "Projecao horizontal sem altura de piso valida nao e aceita como apoio", report);

            teto.transform.localRotation = Quaternion.identity;
            teto.name = "Rocha sobre o caminho";
            UnityEngine.Object.DestroyImmediate(fantasma);
            fantasma = go.AddComponent<FantasmaCaverna>();
            Physics.SyncTransforms();
            Verificar(Apoio(fantasma, out y) && Mathf.Abs(y) < .01f,
                "Nao sobe 2,5 metros para uma rocha acima do corpo", report);
            piso.transform.localPosition += Vector3.up * .4f;
            Physics.SyncTransforms();
            Verificar(Apoio(fantasma, out y) && Mathf.Abs(y - .4f) < .01f,
                "Continua subindo degraus de 40 centimetros", report);
        }
        finally
        {
            if (rotasTeste) UnityEngine.Object.DestroyImmediate(rotasTeste.gameObject);
            EditorSceneManager.CloseScene(teste, true);
        }
    }

    static bool Apoio(FantasmaCaverna fantasma, out float altura)
    {
        object[] args = { fantasma.transform.position, 0f };
        bool encontrou = (bool)typeof(FantasmaCaverna).GetMethod("TentarEncontrarPiso", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fantasma, args);
        altura = (float)args[1];
        return encontrou;
    }

    static void Verificar(bool ok, string mensagem, StringBuilder report)
    {
        if (!ok) throw new InvalidOperationException(mensagem);
        report.AppendLine("PASS: " + mensagem);
    }
}
