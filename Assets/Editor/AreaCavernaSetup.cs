using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AreaCavernaSetup
{
    [MenuItem("Tools/Gorila/Sopa/Aplicar script na area caverna")]
    public static void Aplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var cena = SceneManager.GetActiveScene();
        var objetos = cena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var area = objetos.Single(t => t.name == "area caverna").gameObject;
        var montanha = objetos.Single(t => t.name == "SM_Env_Volcano_01").GetComponent<MeshCollider>();
        var jogador = objetos.Select(t => t.GetComponent<StarterAssets.ThirdPersonController>())
            .Single(c => c != null).GetComponent<CharacterController>();
        if (montanha == null || jogador == null) throw new InvalidOperationException("Montanha ou jogador sem collider.");

        var caixa = area.GetComponent<BoxCollider>();
        if (caixa == null) caixa = Undo.AddComponent<BoxCollider>(area);
        Undo.RecordObject(caixa, "Configurar area caverna");
        caixa.enabled = true;
        caixa.isTrigger = true;
        var script = area.GetComponent<AreaCaverna>();
        if (script == null) script = Undo.AddComponent<AreaCaverna>(area);
        Undo.RecordObject(script, "Vincular montanha e jogador");
        script.colliderMontanha = montanha;
        script.jogador = jogador;
        EditorUtility.SetDirty(script);
        EditorUtility.SetDirty(caixa);
        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        Selection.activeGameObject = area;
        Debug.Log("Area caverna configurada e salva: desativa a colisao da montanha dentro da area e restaura ao sair.", area);
        Validar();
    }

    // Verifica o comportamento em objetos temporarios, sem iniciar o jogo ou tocar no save.
    [MenuItem("Tools/Gorila/Sopa/Validar area caverna %#&F7")]
    public static void Validar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var raiz = new GameObject("Validacao temporaria AreaCaverna");
        raiz.SetActive(false);
        try
        {
            var caixa = raiz.AddComponent<BoxCollider>();
            caixa.size = new Vector3(8, 6, 8);
            caixa.isTrigger = true;
            var alvo = new GameObject("Montanha de teste");
            alvo.transform.SetParent(raiz.transform);
            var montanha = alvo.AddComponent<BoxCollider>();
            var pessoa = new GameObject("Jogador de teste");
            pessoa.transform.SetParent(raiz.transform);
            var jogador = pessoa.AddComponent<CharacterController>();
            jogador.height = 2;
            jogador.radius = 0.3f;
            var script = raiz.AddComponent<AreaCaverna>();
            script.colliderMontanha = montanha;
            script.jogador = jogador;
            pessoa.transform.position = Vector3.right * 10;
            raiz.SetActive(true);
            var atualizarMetodo = typeof(AreaCaverna).GetMethod("AtualizarColisao", BindingFlags.Instance | BindingFlags.NonPublic);
            Action atualizar = () => atualizarMetodo.Invoke(script, null);
            Action<bool, string> verificar = (ok, caso) => { if (!ok) throw new Exception("AreaCaverna FAIL: " + caso); };
            atualizar(); verificar(montanha.enabled, "fora");
            pessoa.transform.position = Vector3.zero;
            atualizar(); verificar(!montanha.enabled, "entrada / nascimento dentro");
            atualizar(); verificar(!montanha.enabled, "permanencia");
            pessoa.transform.position = Vector3.right * 10;
            atualizar(); verificar(montanha.enabled, "saida / teleporte");
            pessoa.transform.position = Vector3.zero;
            atualizar(); pessoa.SetActive(false);
            atualizar(); verificar(montanha.enabled, "jogador desativado");
            pessoa.SetActive(true); atualizar();
            typeof(AreaCaverna).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(script, null);
            verificar(montanha.enabled, "area desativada");
            montanha.enabled = false; atualizar();
            pessoa.transform.position = Vector3.right * 10;
            atualizar(); verificar(!montanha.enabled, "preservar collider originalmente desligado");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/area-caverna-validacao.txt", "PASS: fora, entrada, permanencia, saida/teleporte, jogador desativado, area desativada, estado anterior.\n");
            Debug.Log("AreaCaverna: 7 verificacoes passaram (sem Play Mode).");
        }
        finally { UnityEngine.Object.DestroyImmediate(raiz); }
    }
}
