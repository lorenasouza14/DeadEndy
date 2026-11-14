using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EntradaCavernaSetup
{
    [MenuItem("Tools/Gorila/Sopa/Corrigir passagem e preservar fundo %#&m")]
    public static void Corrigir()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var cena = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var g = Object.FindObjectsByType<GorilaSopa>(FindObjectsInactive.Include).Single(x => x.gameObject.scene == cena);
        var entrada = cena.GetRootGameObjects().Single(x => x.name == "fase_gorila").transform.Find("entrada_caverna");
        var fundo = entrada.Find("fundo_entrada_caverna");
        var vulcao = cena.GetRootGameObjects().Single(x => x.name == "SM_Env_Volcano_01");
        var colisao = vulcao.GetComponent<MeshCollider>();
        var origem = vulcao.GetComponent<MeshFilter>();
        var importador = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(origem.sharedMesh)) as ModelImporter;
        if (importador != null && !importador.isReadable)
        {
            importador.isReadable = true;
            importador.SaveAndReimport();
        }
        // O recorte acompanha o painel preto, inclusive nas laterais. A direcao
        // do gorila estreitava/desalinhava a abertura quando Endy entrava de lado.
        Vector3 frente = Vector3.ProjectOnPlane(fundo.forward, Vector3.up).normalized;
        if (Vector3.Dot(frente, fundo.position - g.transform.position) < 0f) frente = -frente;
        Quaternion rotacao = Quaternion.LookRotation(frente);
        float pisoY = g.transform.position.y - 0.05f;
        Vector3 portal = new Vector3(fundo.position.x, pisoY, fundo.position.z);
        float aproximacao = Mathf.Max(1f, Vector3.Dot(portal - g.transform.position, frente) + 1f);
        Vector3 inicio = portal - frente * aproximacao;
        Vector3 fim = portal + frente * 6f;
        float comprimento = aproximacao + 6f;
        float largura = Mathf.Abs(fundo.lossyScale.x) - 0.4f;
        float tetoY = fundo.GetComponent<Renderer>().bounds.max.y;
        Vector3 centro = (inicio + fim) * 0.5f;
        centro.y = (pisoY - 0.1f + tetoY) * 0.5f;
        Vector3 metade = new Vector3(largura * 0.5f, (tetoY - pisoY + 0.1f) * 0.5f, comprimento * 0.5f);
        var volume = entrada.Find("Volume livre da caverna");
        if (volume == null)
        {
            var go = new GameObject("Volume livre da caverna");
            Undo.RegisterCreatedObjectUndo(go, "Criar abertura editavel");
            volume = go.transform;
            volume.SetParent(entrada, false);
            volume.SetPositionAndRotation(centro, rotacao);
            var caixa = Undo.AddComponent<BoxCollider>(go);
            caixa.size = metade * 2f;
        }
        var abertura = volume.GetComponent<BoxCollider>();
        Undo.RecordObject(abertura, "Definir volume livre");
        abertura.isTrigger = true;
        abertura.enabled = false;
        // Preserva os ajustes manuais da caixa em chamadas seguintes do menu.
        centro = volume.TransformPoint(abertura.center);
        rotacao = volume.rotation;
        largura = abertura.size.x * Mathf.Abs(volume.lossyScale.x);
        comprimento = abertura.size.z * Mathf.Abs(volume.lossyScale.z);
        pisoY = volume.TransformPoint(abertura.center - Vector3.up * abertura.size.y * 0.5f).y + 0.1f;
        var recorte = vulcao.GetComponent<RecorteColisaoCaverna>();
        if (recorte == null) recorte = Undo.AddComponent<RecorteColisaoCaverna>(vulcao);
        Undo.RecordObject(recorte, "Atualizar abertura da montanha");
        recorte.volumeLivre = abertura;
        EditorUtility.SetDirty(recorte);
        var malha = RecorteColisaoCaverna.Gerar(origem, abertura);
        const string caminho = "Assets/fase_gorila/EntradaVulcaoColisao.asset";
        var existente = AssetDatabase.LoadAssetAtPath<Mesh>(caminho);
        if (existente == null) AssetDatabase.CreateAsset(malha, caminho);
        else { EditorUtility.CopySerialized(malha, existente); Object.DestroyImmediate(malha); malha = existente; EditorUtility.SetDirty(malha); }
        Undo.RecordObject(colisao, "Abrir colisao da caverna"); colisao.convex = false; colisao.sharedMesh = malha;
        PrefabUtility.RecordPrefabInstancePropertyModifications(colisao); EditorUtility.SetDirty(colisao);
        var piso = entrada.Find("Piso da passagem liberada");
        if (piso == null)
        {
            var go = new GameObject("Piso da passagem liberada");
            Undo.RegisterCreatedObjectUndo(go, "Criar apoio da passagem");
            piso = go.transform; piso.SetParent(entrada, false);
        }
        Undo.RecordObject(piso, "Ajustar apoio da passagem");
        piso.SetPositionAndRotation(new Vector3(centro.x, pisoY - 0.15f, centro.z), rotacao);
        piso.localScale = Vector3.one;
        var apoio = piso.GetComponent<BoxCollider>();
        if (apoio == null) apoio = Undo.AddComponent<BoxCollider>(piso.gameObject);
        Undo.RecordObject(apoio, "Ajustar chao"); apoio.center = Vector3.zero; apoio.size = new Vector3(largura, 0.3f, comprimento); apoio.isTrigger = false;
        Undo.RecordObject(g, "Preservar fundo ao liberar sopa");
        g.visuaisDosBloqueios = new Renderer[0];
        g.bloqueiosDaCaverna = fundo.GetComponents<Collider>();
        g.pisoDaPassagem = apoio;
        foreach (var r in fundo.GetComponents<Renderer>())
        { Undo.RecordObject(r, "Manter fundo visivel"); r.enabled = true; EditorUtility.SetDirty(r); }
        PrefabUtility.RecordPrefabInstancePropertyModifications(g); EditorUtility.SetDirty(g);
        EditorSceneManager.MarkSceneDirty(cena); EditorSceneManager.SaveScene(cena); AssetDatabase.SaveAssets();
        Selection.activeGameObject = volume.gameObject;
        SceneView.lastActiveSceneView?.FrameSelected();
        Debug.Log("Abertura editavel aplicada e salva. Ajuste Volume livre da caverna > Box Collider > Edit Collider e reaplique este menu. Ao iniciar o jogo, a colisao e recalculada da malha visual atual. Nenhum teste executado.");
    }

    [MenuItem("Tools/Gorila/Sopa/Inspecionar bloqueios da entrada %#&p")]
    public static void Inspecionar()
    {
        var cena = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var g = Object.FindObjectsByType<GorilaSopa>(FindObjectsInactive.Include).Single(x => x.gameObject.scene == cena);
        var area = new Bounds(g.transform.position, new Vector3(24, 14, 24));
        var texto = new StringBuilder();
        texto.AppendLine("GORILA " + g.transform.position.ToString("F3"));
        var fundo = GameObject.Find("fase_gorila/entrada_caverna/fundo_entrada_caverna");
        Vector3 frente = Vector3.ProjectOnPlane(fundo.transform.position - g.transform.position, Vector3.up).normalized;
        foreach (float lado in new[] { -0.6f, 0f, 0.6f })
        {
            var origem = g.transform.position + Vector3.up + Vector3.Cross(Vector3.up, frente) * lado;
            foreach (var hit in Physics.RaycastAll(origem, frente, 12f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                texto.AppendLine("ENTRADA " + lado + " " + SopaCenaSetup.Caminho(hit.transform) + " ponto=" + hit.point.ToString("F3") + " normal=" + hit.normal.ToString("F3"));
        }
        foreach (var col in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include))
        {
            if (col.gameObject.scene != cena || !col.enabled || !col.bounds.Intersects(area)) continue;
            texto.AppendLine(SopaCenaSetup.Caminho(col.transform) + " | " + col.GetType().Name + " | " + col.bounds.ToString("F3") + " | trigger=" + col.isTrigger);
            texto.AppendLine(EditorJsonUtility.ToJson(col));
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/entrada-caverna-colliders.txt", texto.ToString());
        Debug.Log("Colliders da entrada registrados, sem executar Play Mode.");
    }
}
