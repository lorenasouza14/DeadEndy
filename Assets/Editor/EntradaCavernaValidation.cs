using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EntradaCavernaValidation
{
    const string Cena = "Assets/Cenario/PNB_Tropical_Jungle/Scenes/juncao_fase1-2.unity";

    public static void ExecutarBatch()
    {
        EditorSceneManager.OpenScene(Cena);
        EditorApplication.Exit(Verificar());
    }

    [MenuItem("Tools/Gorila/Sopa/Diagnosticar passagem atual %#&F8")]
    public static void Diagnosticar() { Verificar(); }

    private static int Verificar()
    {
        var texto = new StringBuilder();
        int codigo = 0;
        var gorila = UnityEngine.Object.FindAnyObjectByType<GorilaSopa>();
        var endy = UnityEngine.Object.FindAnyObjectByType<InventarioEndy>().GetComponent<CharacterController>();
        var posGorila = gorila.transform.position;
        var posEndy = endy.transform.position;
        bool endyAtivo = endy.enabled;
        var bloqueios = gorila.bloqueiosDaCaverna.Select(c => c.enabled).ToArray();
        var liberado = typeof(GorilaSopa).GetField("bloqueiosLiberados", BindingFlags.Instance | BindingFlags.NonPublic);
        bool estadoLiberado = (bool)liberado.GetValue(gorila);
        var atualizar = typeof(GorilaSopa).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
        try
        {
            var fundo = GameObject.Find("fase_gorila/entrada_caverna/fundo_entrada_caverna").transform;
            var origem = gorila.transform.position;
            var piso = fundo.parent.Find("Piso da passagem liberada").GetComponent<BoxCollider>();
            var frente = piso.transform.forward;
            var lateral = Vector3.Cross(Vector3.up, frente);
            float distancia = Vector3.Dot(fundo.position - origem, frente);
            texto.AppendLine("Cena=" + gorila.gameObject.scene.path + " piso=" + piso.size + " associado=" + (gorila.pisoDaPassagem != null));
            foreach (var mc in UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include))
                if (mc.name.Contains("Volcano")) texto.AppendLine("Vulcao=" + mc.sharedMesh.name + " vertices=" + mc.sharedMesh.vertexCount);
            texto.AppendLine($"Origem={origem:F3}, fundo={fundo.position:F3}, Endy: altura={endy.height}, raio={endy.radius}, escala={endy.transform.lossyScale}");
            atualizar.Invoke(gorila, null);
            if (gorila.bloqueiosDaCaverna.Any(c => !c.enabled)) throw new Exception("A entrada abriu enquanto o gorila ainda estava na frente.");
            texto.AppendLine("PASS: entrada bloqueada com gorila na frente.");
            gorila.transform.position = gorila.percurso.Last().position;
            Physics.SyncTransforms();
            // Com o gorila fora, identifica a barreira que ainda ocupa a entrada
            // antes de simular sua liberacao pela sopa.
            foreach (var hit in Physics.SphereCastAll(origem + Vector3.up, 0.28f, frente, distancia + 2f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                texto.AppendLine($"ANTES DA LIBERACAO: {SopaCenaSetup.Caminho(hit.transform)}, ponto={hit.point:F3}");
            atualizar.Invoke(gorila, null);
            texto.AppendLine("Liberado pelo proprio Update: " + gorila.bloqueiosDaCaverna.All(c => !c.enabled));
            Physics.SyncTransforms();
            foreach (float lado in new[] { -0.6f, 0f, 0.6f, 1.5f, 2.6f })
            {
                endy.enabled = false;
                endy.transform.position = origem - frente * 0.4f + lateral * lado + Vector3.up * 0.1f;
                endy.enabled = true;
                Physics.SyncTransforms();
                for (int i = 0; i < 240; i++)
                {
                    endy.Move((frente * 2f + Vector3.down * 2f) / 60f);
                    Physics.SyncTransforms();
                    if (Vector3.Dot(endy.transform.position - origem, frente) >= distancia + 1.3f) break;
                }
                float avancou = Vector3.Dot(endy.transform.position - origem, frente);
                bool passou = avancou >= distancia + 1.2f && endy.transform.position.y > origem.y - 0.3f;
                texto.AppendLine($"{(passou ? "PASS" : "FAIL")} lado={lado}: Endy={endy.transform.position:F3}, avancou={avancou:F3}, minimo={distancia + 1.2f:F3}");
                if (!passou) codigo = 1;
                foreach (var hit in Physics.SphereCastAll(endy.bounds.center, endy.radius * 0.8f, frente, 2f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                    texto.AppendLine($"  Colisao: {SopaCenaSetup.Caminho(hit.transform)}, ponto={hit.point:F3}");
                foreach (var col in Physics.OverlapCapsule(endy.bounds.center + Vector3.up * 0.5f, endy.bounds.center - Vector3.up * 0.5f, 0.6f, ~0, QueryTriggerInteraction.Ignore))
                    texto.AppendLine("  Contato: " + SopaCenaSetup.Caminho(col.transform));
            }
        }
        catch (Exception e) { codigo = 1; texto.AppendLine(e.ToString()); }
        finally
        {
            endy.enabled = false;
            endy.transform.position = posEndy;
            endy.enabled = endyAtivo;
            gorila.transform.position = posGorila;
            for (int i = 0; i < bloqueios.Length; i++) gorila.bloqueiosDaCaverna[i].enabled = bloqueios[i];
            liberado.SetValue(gorila, estadoLiberado);
            Physics.SyncTransforms();
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/entrada-caverna-validation.txt", texto.ToString());
        Debug.Log(texto.ToString());
        return codigo;
    }
}
