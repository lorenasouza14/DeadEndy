using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;


public static class KrakenDesmoronamentoSetup
{
    const string Cena = "Assets/Scenes/Recuperadas/Level_Kraken_4.unity";
    const string Pasta = "Assets/KrakenCombate/Desmoronamento";
    const string Trabalho = "Logs/KrakenDesmoronamento/";
    [MenuItem("Tools/Kraken/Configurar desmoronamento e correntes")]
    public static void Configurar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Salve a cena aberta antes de configurar.");
        Directory.CreateDirectory(Trabalho); var cena = EditorSceneManager.OpenScene(Cena);
        var batalha = UnityEngine.Object.FindObjectsByType<BatalhaKraken>(FindObjectsSortMode.None).Single(b => b.gameObject.scene == cena);
        if (batalha.pilares.Length != 4 || batalha.pilares.Distinct().Count() != 4) throw new InvalidOperationException("Esperados quatro pilares distintos.");
        if (!AssetDatabase.IsValidFolder(Pasta)) AssetDatabase.CreateFolder("Assets/KrakenCombate", "Desmoronamento");
        var interior = AssetDatabase.LoadAssetAtPath<Material>(Pasta + "/InteriorPilar.mat");
        if (!interior)
        {
            interior = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            interior.name = "InteriorPilar";
            interior.SetColor("_BaseColor", new Color(.44f, .44f, .44f));
            interior.SetFloat("_Smoothness", .2f);
            AssetDatabase.CreateAsset(interior, Pasta + "/InteriorPilar.mat");
        }
        for (int i = 0; i < batalha.pilares.Length; i++)
        {
            var p = batalha.pilares[i];
            var filtro = p.pilar.GetComponent<MeshFilter>();
            if (!filtro || !filtro.sharedMesh || !p.corrente) throw new InvalidOperationException("Pilar/corrente incompletos.");
            string guid; long id;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(filtro.sharedMesh, out guid, out id);
            var meshes = RuinasFragmentos.Criar(filtro.sharedMesh, Pasta + "/Pilar_" + guid + "_" + id + ".asset");
            if (meshes.Length != 8 || meshes.Any(m => m.vertexCount == 0)) throw new InvalidOperationException("Recorte dos oito fragmentos falhou.");
            var efeito = p.GetComponent<FragmentosPilarKraken>();
            if (!efeito) efeito = Undo.AddComponent<FragmentosPilarKraken>(p.gameObject);
            Undo.RecordObject(efeito, "Configurar fragmentos do Kraken");
            efeito.origem = filtro; efeito.malhas = meshes; efeito.interior = interior;
            Undo.RecordObject(p, "Ligar desmoronamento");
            p.fragmentos = efeito;
            var corrente = p.corrente;
            var filtroCorrente = corrente.GetComponent<MeshFilter>();
            var malhaCorrente = AssetDatabase.LoadAssetAtPath<Mesh>(Pasta + "/CorrenteLegivel.asset");
            if (!malhaCorrente)
            {
                malhaCorrente = UnityEngine.Object.Instantiate(filtroCorrente.sharedMesh);
                malhaCorrente.name = "Corrente para balanco";
                AssetDatabase.CreateAsset(malhaCorrente, Pasta + "/CorrenteLegivel.asset");
            }
            if (!malhaCorrente.isReadable) throw new InvalidOperationException("A copia da corrente precisa permitir leitura.");
            Undo.RecordObject(filtroCorrente, "Preparar balanco local");
            filtroCorrente.sharedMesh = malhaCorrente;
            var balanco = corrente.GetComponent<BalancoCorrenteKraken>();
            if (!balanco) balanco = Undo.AddComponent<BalancoCorrenteKraken>(corrente.gameObject);
            Undo.RecordObject(balanco, "Configurar balanco da corrente");
            balanco.visual = filtroCorrente; balanco.corpo = corrente; balanco.fase = i * 1.37f;
            p.balancoCorrente = balanco;
            GameObjectUtility.SetStaticEditorFlags(corrente.gameObject, 0);
            foreach (var obj in new UnityEngine.Object[] { p, efeito, balanco, filtroCorrente })
            { EditorUtility.SetDirty(obj); PrefabUtility.RecordPrefabInstancePropertyModifications(obj); }
        }
        EditorSceneManager.MarkSceneDirty(cena);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(cena);
        Selection.activeGameObject = batalha.pilares[0].gameObject;
        SceneView.lastActiveSceneView?.FrameSelected();
        File.WriteAllText(Trabalho + "setup.txt", "PASS: quatro pilares, oito fragmentos por pilar, quatro correntes; prefabs compartilhados preservados.\n");
    }
}
