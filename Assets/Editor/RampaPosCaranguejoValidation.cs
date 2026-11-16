using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RampaPosCaranguejoValidation
{
    [MenuItem("Tools/Gorila/Validar bloqueio da rampa antes do caranguejo")]
    public static void Validar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var relatorio = new StringBuilder();
        // A cena real e aberta como previa: nao altera cenas abertas nem o save do jogador.
        var cena = EditorSceneManager.OpenPreviewScene(ProgressoJogo.CenaIntegrada);
        try
        {
            var raizes = cena.GetRootGameObjects();
            var rampa = raizes.SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Single(t => t.name == ArmadilhasRampaSetup.NomePedra);
            var altar = rampa.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Altar_round_B");
            var banana = altar.GetComponentInChildren<BananaSopa>(true);
            var molas = rampa.GetComponentsInChildren<ArmadilhaMola>(true);
            var alavancas = raizes.SelectMany(r => r.GetComponentsInChildren<AlavancaTriangulo>(true)).ToArray();
            var ativas = alavancas.Select(a => a.gameObject.activeInHierarchy).ToArray();
            var chefe = raizes.SelectMany(r => r.GetComponentsInChildren<CaranguejoChefe>(true)).Single();
            Verificar(banana != null && molas.Length == 22, "Totem, banana e 22 armadilhas encontrados na rampa", relatorio);

            var objeto = new GameObject("Validacao da progressao da rampa");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(objeto, cena);
            var limites = objeto.AddComponent<LimitesPrimeiraFase>();
            typeof(LimitesPrimeiraFase).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(limites, null);
            Verificar(!altar.gameObject.activeInHierarchy && !banana.gameObject.activeInHierarchy,
                "Totem e banana ocultos antes da vitoria", relatorio);
            Verificar(molas.All(m => !m.gameObject.activeInHierarchy), "Armadilhas desativadas antes da vitoria", relatorio);
            Verificar(rampa.gameObject.activeInHierarchy && alavancas.Select((a, i) => a.gameObject.activeInHierarchy == ativas[i]).All(a => a),
                "Rampa e alavancas da primeira fase preservadas", relatorio);
            Verificar(!string.IsNullOrEmpty(banana.idSave), "Banana oculta conserva identificacao para o save", relatorio);

            chefe.estadoAtual = CaranguejoChefe.EstadoChefe.Derrotado;
            limites.AtualizarBloqueios();
            Verificar(!altar.gameObject.activeInHierarchy && molas.All(m => !m.gameObject.activeInHierarchy),
                "Rampa aguarda o termino do encolhimento do chefe", relatorio);
            chefe.RestaurarDerrota(chefe.transform.position);
            limites.AtualizarBloqueios();
            Verificar(altar.gameObject.activeInHierarchy && banana.gameObject.activeInHierarchy && molas.All(m => m.gameObject.activeInHierarchy),
                "Vitoria concluida libera totem, banana e todas as armadilhas", relatorio);

            banana.RestaurarDepositada();
            limites.AtualizarBloqueios();
            Verificar(!banana.gameObject.activeSelf, "Atualizacoes nao fazem reaparecer a banana entregue", relatorio);

            UnityEngine.Object.DestroyImmediate(objeto);
            // Ao continuar, o controlador nasce antes de o autosave restaurar a derrota e a sopa.
            typeof(CaranguejoChefe).GetProperty("EncolhimentoConcluido").GetSetMethod(true).Invoke(chefe, new object[] { false });
            objeto = new GameObject("Validacao ao continuar");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(objeto, cena);
            limites = objeto.AddComponent<LimitesPrimeiraFase>();
            typeof(LimitesPrimeiraFase).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(limites, null);
            chefe.RestaurarDerrota(chefe.transform.position);
            banana.RestaurarDepositada();
            limites.AtualizarBloqueios();
            Verificar(altar.gameObject.activeInHierarchy && molas.All(m => m.gameObject.activeInHierarchy) && !banana.gameObject.activeSelf,
                "Continuar apos o chefe libera a rampa sem duplicar banana ja entregue", relatorio);
        }
        catch (Exception erro)
        {
            relatorio.AppendLine("FAIL: " + erro);
            throw;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(cena);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/rampa-pos-caranguejo-validation.txt", relatorio.ToString());
        }
        Debug.Log(relatorio.ToString());
    }

    static void Verificar(bool condicao, string mensagem, StringBuilder relatorio)
    {
        if (!condicao) throw new InvalidOperationException(mensagem);
        relatorio.AppendLine("PASS: " + mensagem);
    }
}
