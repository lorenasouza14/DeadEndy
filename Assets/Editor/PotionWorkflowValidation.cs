using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Execute com Unity -batchmode -executeMethod PotionWorkflowValidation.RunBatch.
// Usa uma cena temporária e nunca salva as cenas do jogo.
[InitializeOnLoad]
public static class PotionWorkflowValidation
{
    const string Pending = "PotionWorkflowValidation.Pending";
    static PotionWorkflowValidation()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Validação reservada ao modo batch.");
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/Cenario/PNB_Tropical_Jungle/Scenes/Demo_Day.unity");
            InventarioEndy inventory = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                inventory = root.GetComponentInChildren<InventarioEndy>(true);
                if (inventory != null) break;
            }
            Check(inventory != null && inventory.pocaoNaMao3D != null, "Cena tem o exemplo da mão ligado ao inventário");
            Check(inventory.pocaoNaMao3D.transform.parent.name == "handslot.r", "Exemplo está no encaixe da mão direita");
            Check(inventory.GetComponent<StarterAssets.ThirdPersonController>() != null, "Movimento e inventário estão no mesmo objeto");
            Check(inventory.pocaoNaMao3D.GetComponent<MeshFilter>().sharedMesh ==
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Baus/Prefabs/Potion_A.prefab").GetComponent<MeshFilter>().sharedMesh,
                "Poção A usa a mesma malha e o mesmo tamanho do exemplo");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception error) { Finish(error); }
    }

    [MenuItem("Tools/Poções/Validar coleta e efeitos")]
    public static void RunInEditor()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        try
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var manager = root.GetComponentInChildren<GerenciadorDeBaus>();
                if (manager != null)
                {
                    foreach (Transform chest in manager.baus)
                    {
                        Transform bottom = chest.Find("Chest_Animated/Chest_Bottom");
                        Check(bottom != null, "Fundo do baú encontrado: " + chest.name);
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Baus/Prefabs/Lollipop_01.prefab");
                        var candy = Object.Instantiate(prefab, bottom.position, bottom.rotation);
                        candy.transform.SetParent(bottom);
                        var adjustment = candy.GetComponent<AjusteDeItem>();
                        adjustment.Aplicar();
                        Bounds bounds = adjustment.CalcularLimites(bottom);
                        Bounds chestBounds = bottom.GetComponent<MeshFilter>().sharedMesh.bounds;
                        Debug.Log("LOLLIPOP BOUNDS " + chest.name + ": " + bounds + " CHEST " + chestBounds);
                        Check(Mathf.Abs(bounds.center.x) < 0.001f && Mathf.Abs(bounds.center.z) < 0.001f,
                            "Pirulito centralizado no baú: " + chest.name);
                        Check(bounds.min.x > chestBounds.min.x && bounds.max.x < chestBounds.max.x &&
                            bounds.min.z > chestBounds.min.z && bounds.max.z < chestBounds.max.z &&
                            bounds.min.y > chestBounds.min.y && bounds.max.y < chestBounds.max.y,
                            "Pirulito cabe dentro do fundo do baú: " + chest.name);
                        Object.Destroy(candy);
                    }
                }
            }
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var inventory = root.GetComponentInChildren<InventarioEndy>();
                if (inventory == null) continue;
                Check(inventory.pocaoNaMao3D != null, "Cena real tem o exemplo da mão configurado");
                foreach (string letter in new[] { "A", "C" })
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Baus/Prefabs/Potion_" + letter + ".prefab");
                    var potion = Object.Instantiate(prefab).GetComponent<PocaoMagica>();
                    Check(inventory.ColetarPocao(potion), letter + ": coleta no Endy da cena real");
                    Check(potion.gameObject.activeInHierarchy && potion.GetComponent<Renderer>().enabled &&
                        potion.transform.parent == inventory.pocaoNaMao3D.transform.parent &&
                        potion.transform.localPosition == inventory.pocaoNaMao3D.transform.localPosition,
                        letter + ": frasco visível no encaixe original do Endy");
                    inventory.BeberPocao();
                }
                break;
            }
        }
        catch (Exception error) { Finish(error); return; }
        // Alterações somente durante Play Mode; a cena aberta volta intacta ao sair.
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
        SceneManager.SetActiveScene(SceneManager.CreateScene("Potion validation"));
        new GameObject("Potion validation").AddComponent<PotionWorkflowValidationRunner>().StartChecks();
    }

    public static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        Debug.Log("POTION PASS: " + description);
    }

    public static void Finish(Exception error)
    {
        string result = error == null ? "PASS: potion workflow" : "FAIL: " + error;
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/potion-validation-result.txt", result);
        if (error != null) Debug.LogError(result); else Debug.Log(result);
        if (Application.isBatchMode) EditorApplication.Exit(error == null ? 0 : 1);
        else EditorApplication.ExitPlaymode();
    }
}

public class PotionWorkflowValidationRunner : MonoBehaviour
{
    public void StartChecks() { StartCoroutine(GuardedChecks()); }
    IEnumerator GuardedChecks()
    {
        IEnumerator checks = Checks();
        while (true)
        {
            object wait;
            try
            {
                if (!checks.MoveNext()) { PotionWorkflowValidation.Finish(null); yield break; }
                wait = checks.Current;
            }
            catch (Exception error) { PotionWorkflowValidation.Finish(error); yield break; }
            yield return wait;
        }
    }

    static void Check(bool condition, string description) { PotionWorkflowValidation.Check(condition, description); }
    static PocaoMagica Potion(string letter)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Baus/Prefabs/Potion_" + letter + ".prefab");
        GameObject instance = Object.Instantiate(prefab, Vector3.up * 10, Quaternion.identity);
        Check(instance.GetComponent<EfeitoPocaoAzul>() == null && instance.GetComponent<EfeitoPocaoVerde>() == null,
            letter + ": sem os scripts antigos de uso direto no baú");
        return instance.GetComponent<PocaoMagica>();
    }

    IEnumerator Checks()
    {
        GameObject player = new GameObject("Endy validation");
        player.SetActive(false);
        player.transform.position = Vector3.up * 10;
        var movement = player.AddComponent<StarterAssets.ThirdPersonController>();
        movement.enabled = false;
        var body = player.GetComponent<CharacterController>();
        var life = player.AddComponent<VidaEndy>();
        var anchor = new GameObject("handslot.r").transform;
        anchor.SetParent(player.transform, false);
        var example = new GameObject("Pocao exemplo");
        example.transform.SetParent(anchor, false);
        example.transform.localPosition = new Vector3(0.005f, 0.07f, 0.053f);
        var inventory = player.AddComponent<InventarioEndy>();
        inventory.pocaoNaMao3D = example;
        inventory.tempoSincroniaArremesso = 0.02f;
        inventory.mostrarControles = false;
        player.SetActive(true);
        yield return null;

        PocaoMagica green = Potion("A");
        green.tempoDeEfeito = 0.1f;
        Check(green.GetComponent<ItemColetavel>().TentarColetar(inventory), "Coleta A pelo componente do prefab");
        Check(green.transform.parent == anchor && green.transform.localPosition == example.transform.localPosition,
            "Frasco ocupa o encaixe e a posição do exemplo");
        Check(green.GetComponent<Renderer>().enabled && green.gameObject.activeInHierarchy && !example.activeSelf,
            "Frasco visível sem duplicar o exemplo");
        Check(!green.GetComponent<Collider>().enabled && green.GetComponent<Rigidbody>().isKinematic,
            "Frasco na mão não colide nem cai");
        PocaoMagica blue = Potion("C");
        Check(!blue.GetComponent<ItemColetavel>().TentarColetar(inventory) && blue.DisponivelParaColeta,
            "Mão ocupada preserva a outra poção");
        inventory.BeberPocao();
        Check(player.transform.localScale == Vector3.one * 0.5f && !inventory.temPocao, "Beber A encolhe Endy e libera a mão");
        yield return new WaitForSeconds(0.2f);
        Check(player.transform.localScale == Vector3.one, "Tamanho restaurado após o efeito");

        float baseSpeed = movement.MoveSpeed;
        blue.tempoDeEfeito = 0.1f;
        Check(blue.GetComponent<ItemColetavel>().TentarColetar(inventory), "Coleta C depois de usar A");
        inventory.BeberPocao();
        Check(movement.MultiplicadorPocao == 2f && movement.MoveSpeed == baseSpeed, "C acelera sem sobrescrever a velocidade base");
        // Simula a alteração de velocidade base aplicada pela areia durante o bônus.
        movement.MoveSpeed = 1.5f;
        yield return new WaitForSeconds(0.2f);
        Check(movement.MultiplicadorPocao == 1f && movement.MoveSpeed == 1.5f, "Efeito expira sem desfazer a lentidão da areia");
        movement.MoveSpeed = baseSpeed;

        blue = Potion("C");
        blue.tempoDeEfeito = 0.4f;
        blue.prefabEfeitoSplash = null;
        Check(inventory.ColetarPocao(blue), "Coleta C para arremesso");
        inventory.JogarPocao();
        inventory.BeberPocao();
        Check(inventory.temPocao && movement.MultiplicadorPocao == 1, "Não bebe durante a animação de arremesso");
        yield return new WaitForSeconds(0.08f);
        Check(!inventory.temPocao && blue.transform.parent == null, "Arremesso solta o frasco e esvazia a mão");
        Check(!blue.GetComponent<Collider>().isTrigger && !blue.GetComponent<Rigidbody>().isKinematic,
            "Arremesso ativa colisão sólida e física");
        Check(Physics.GetIgnoreCollision(blue.GetComponent<Collider>(), body), "Arremesso ignora o corpo do Endy");
        Check(!inventory.ColetarPocao(blue), "Projétil não pode ser coletado de novo");

        var bossObject = new GameObject("Boss validation");
        bossObject.transform.position = new Vector3(20, 10, 0);
        var bossBody = bossObject.AddComponent<Rigidbody>();
        bossBody.useGravity = false;
        bossBody.constraints = RigidbodyConstraints.FreezeAll;
        var bossCollider = bossObject.AddComponent<BoxCollider>();
        bossCollider.size = Vector3.one * 2;
        var boss = bossObject.AddComponent<CaranguejoChefe>();
        float bossSpeed = boss.velPerseguicao;
        blue.transform.position = bossObject.transform.position + Vector3.back * 2;
        blue.GetComponent<Rigidbody>().linearVelocity = Vector3.forward * 12;
        Physics.SyncTransforms();
        yield return new WaitForSeconds(0.15f);
        Check(blue == null && boss.velPerseguicao == bossSpeed * 2, "Colisão real de C acelera o chefe");
        boss.InvestidaPunitiva(false);
        yield return new WaitForSeconds(0.4f);
        Check(boss.velPerseguicao == bossSpeed, "Bônus do chefe expira mesmo após interromper corrotinas");
        boss.StopAllCoroutines();
        boss.estadoAtual = CaranguejoChefe.EstadoChefe.Escondido;
        var candyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Baus/Prefabs/Lollipop_01.prefab");
        var candy = Object.Instantiate(candyPrefab).GetComponent<EfeitoPirulito>();
        candy.tempoDePaz = 0.25f;
        Check(candy.GetComponent<ItemColetavel>().TentarColetar(inventory), "E coleta o pirulito pelo componente do prefab");
        Check(candy.transform.parent == anchor && candy.gameObject.activeInHierarchy &&
            !candy.GetComponent<Collider>().enabled, "Pirulito fica na mão sem colisão");
        inventory.BeberPocao();
        Check(inventory.TemPirulito, "R não consome o pirulito");
        var extraPotion = Potion("A");
        Check(!inventory.ColetarPocao(extraPotion), "Pirulito e poção respeitam o mesmo espaço na mão");
        Object.Destroy(extraPotion.gameObject);
        inventory.JogarPocao();
        yield return new WaitForSeconds(0.08f);
        Check(!inventory.TemPirulito && candy.transform.parent == null &&
            Physics.GetIgnoreCollision(candy.GetComponent<Collider>(), body), "F solta o pirulito e ignora Endy");
        candy.transform.position = bossObject.transform.position + Vector3.back * 2;
        candy.GetComponent<Rigidbody>().linearVelocity = Vector3.forward * 12;
        Physics.SyncTransforms();
        yield return new WaitForSeconds(0.15f);
        Check(candy == null && boss.EstaCalmo, "Colisão do pirulito acalma o chefe");
        boss.InvestidaPunitiva(true);
        boss.AcordarChefe();
        Check(boss.EstaCalmo && boss.estadoAtual == CaranguejoChefe.EstadoChefe.Escondido,
            "Baús não interrompem os segundos de paz");
        boss.alvoEndy = player.transform;
        yield return new WaitForSeconds(0.3f);
        Check(!boss.EstaCalmo && boss.estadoAtual == CaranguejoChefe.EstadoChefe.Perseguindo,
            "Chefe volta a perseguir ao acabar o efeito do pirulito");
        boss.estadoAtual = CaranguejoChefe.EstadoChefe.Escondido;
        green = Potion("A");
        green.prefabEfeitoSplash = null;
        green.transform.position = bossObject.transform.position + Vector3.back * 2;
        green.ConfigurarArremesso(life, body, Vector3.forward * 12);
        Physics.SyncTransforms();
        yield return new WaitForSeconds(0.2f);
        Check(green == null && boss.estadoAtual == CaranguejoChefe.EstadoChefe.Derrotado,
            "Colisão real de A derrota e encolhe o chefe");
        yield return new WaitForSeconds(2.1f);
        Check(Vector3.Distance(boss.transform.localScale, Vector3.one * 0.15f) < 0.001f, "Chefe termina com o tamanho encolhido");
    }
}
