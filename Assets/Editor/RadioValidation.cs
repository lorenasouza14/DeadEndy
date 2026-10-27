using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Executa apenas em Play Mode e não salva alterações nas cenas do jogo.
[InitializeOnLoad]
public static class RadioValidation
{
    const string ScenePath = "Assets/Cenario/PNB_Tropical_Jungle/Scenes/Demo_Day.unity";
    static RadioValidation() { EditorApplication.playModeStateChanged += Changed; }
    public static void RunBatch() { Run(); }
    [MenuItem("Tools/Radio/Validar coleta musica e chefe")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!SceneManager.GetSceneByPath(ScenePath).isLoaded)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            SessionState.SetBool("RadioValidation.AddedScene", true);
        }
        SessionState.SetBool("RadioValidation.Pending", true);
        EditorApplication.EnterPlaymode();
    }
    static void Changed(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("RadioValidation.Pending", false))
        {
            SessionState.SetBool("RadioValidation.Pending", false);
            new GameObject("Radio validation").AddComponent<RadioValidationRunner>().Run();
        }
        if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool("RadioValidation.AddedScene", false))
        {
            SessionState.SetBool("RadioValidation.AddedScene", false);
            EditorApplication.delayCall += () => EditorSceneManager.CloseScene(SceneManager.GetSceneByPath(ScenePath), true);
        }
    }
    public static void Finish(Exception error)
    {
        Directory.CreateDirectory("Logs");
        string result = error == null ? "PASS: radio pickup, placement, music, dance, rage and replay" : "FAIL: " + error;
        File.WriteAllText("Logs/radio-validation.txt", result);
        Debug.Log(result);
        if (Application.isBatchMode) EditorApplication.Exit(error == null ? 0 : 1);
        else EditorApplication.ExitPlaymode();
    }
}

public class RadioValidationRunner : MonoBehaviour
{
    public void Run() { StartCoroutine(Guard()); }
    IEnumerator Guard()
    {
        var checks = Checks();
        while (true)
        {
            object wait;
            try
            {
                if (!checks.MoveNext()) { RadioValidation.Finish(null); yield break; }
                wait = checks.Current;
            }
            catch (Exception error) { RadioValidation.Finish(error); yield break; }
            yield return wait;
        }
    }
    static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        Debug.Log("RADIO PASS: " + message);
    }
    IEnumerator Checks()
    {
        Directory.CreateDirectory("Logs");
        var inventory = Object.FindAnyObjectByType<InventarioEndy>();
        var bosses = Object.FindObjectsByType<CaranguejoChefe>(FindObjectsInactive.Include);
        Check(inventory != null && bosses.Length > 0, "Endy e chefe da cena encontrados");
        var boss = bosses[0];
        inventory.GetComponent<StarterAssets.ThirdPersonController>().enabled = false;
        inventory.GetComponent<VidaEndy>().enabled = false;
        var controller = inventory.GetComponent<CharacterController>();
        controller.enabled = false;
        inventory.transform.SetPositionAndRotation(new Vector3(0, 1000.55f, 0), Quaternion.identity);
        controller.enabled = true;
        inventory.animEndy.SetFloat("Speed", 0);
        inventory.animEndy.Play("Idle Walk Run Blend", 0, 0);
        var radioPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Baus/Prefabs/Radio.prefab");
        var radio = Object.Instantiate(radioPrefab).GetComponent<EfeitoTocaDiscos>();
        var pickup = radio.GetComponent<ItemColetavel>();
        Check(pickup != null && pickup.ehRadio && radio.musicaRelaxante != null, "Prefab tem coleta e música configuradas");
        var manager = Object.FindAnyObjectByType<GerenciadorDeBaus>();
        if (manager != null)
            foreach (var chest in manager.baus)
            {
                var bottom = chest.Find("Chest_Animated/Chest_Bottom");
                radio.transform.SetParent(bottom, false);
                radio.GetComponent<AjusteDeItem>().Aplicar();
                Bounds bounds = radio.GetComponent<AjusteDeItem>().CalcularLimites(bottom);
                Check(Mathf.Abs(bounds.center.x) < 0.01f && Mathf.Abs(bounds.center.z) < 0.01f && Mathf.Abs(bounds.min.y - 0.2f) < 0.01f,
                    "Rádio centralizado no baú " + chest.name);
            }
        Check(pickup.TentarColetar(inventory), "E pega o rádio");
        Check(inventory.TemRadio && radio.NaMao && !radio.Ligado && !pickup.enabled, "Rádio fica na mão sem ligar");
        Check(radio.GetComponentInChildren<Renderer>().bounds.size.magnitude < 2f, "Escala do rádio preservada");
        Check(!radio.AlternarSom() && !inventory.PodeColetarPocao, "Não liga na mão e ocupa o inventário");
        Check(!inventory.ColocarRadio() && inventory.TemRadio, "Sem chão mantém rádio na mão");
        yield return null;
        if (!Application.isBatchMode) { yield return new WaitForEndOfFrame(); Capture(inventory.transform, "Logs/radio-held.png"); }
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Radio test floor";
        floor.transform.position = new Vector3(0, 1000, 0);
        floor.transform.localScale = new Vector3(50, 1, 50);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = inventory.transform.position + Vector3.forward * 0.7f + Vector3.up * 0.5f;
        Physics.SyncTransforms();
        Check(!inventory.ColocarRadio() && inventory.TemRadio, "Não coloca através de obstáculo");
        wall.SetActive(false);
        Physics.SyncTransforms();
        Check(inventory.ColocarRadio(), "F coloca no chão: " + radio.Aviso);
        Check(radio.Colocado && !radio.Ligado && !inventory.TemRadio && inventory.PodeColetarPocao, "Colocação libera mão sem ligar automaticamente");
        Check(Mathf.Abs(radio.GetComponentInChildren<Renderer>().bounds.min.y - 1000.525f) < 0.04f, "Base do rádio encosta no chão");
        boss.gameObject.SetActive(true);
        boss.StopAllCoroutines();
        boss.transform.SetPositionAndRotation(new Vector3(0, 1000.6f, 12), Quaternion.Euler(0, 180, 0));
        boss.alvoEndy = inventory.transform;
        boss.prefabAreia = null;
        boss.tempoEntreAtaques = 100;
        boss.distanciaSoco = 1;
        boss.GetComponent<Rigidbody>().useGravity = false;
        boss.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
        boss.estadoAtual = CaranguejoChefe.EstadoChefe.Investindo;
        wall.tag = "Pedra";
        boss.SendMessage("ProcessarColisao", wall);
        int stunned = boss.NumeroAtordoamento;
        yield return new WaitForSeconds(0.2f);
        Debug.Log("BOSS STUN: logic=" + boss.estadoAtual + " enabled=" + boss.enabled +
            " animator=" + boss.animChefe.isActiveAndEnabled + " speed=" + boss.animChefe.speed +
            " hasDie=" + boss.animChefe.HasState(0, Animator.StringToHash("Base Layer.Die")) +
            " current=" + boss.animChefe.GetCurrentAnimatorStateInfo(0).fullPathHash +
            " next=" + boss.animChefe.GetNextAnimatorStateInfo(0).fullPathHash +
            " clips=" + string.Join(",", System.Array.ConvertAll(boss.animChefe.GetCurrentAnimatorClipInfo(0), c => c.clip.name)));
        Check(boss.estadoAtual == CaranguejoChefe.EstadoChefe.Atordoado &&
            boss.animChefe.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Die"), "Bater na parede toca Die sem derrotar chefe");
        var die = boss.animChefe.GetCurrentAnimatorClipInfo(0)[0].clip;
        Check(!die.isLooping, "Die toca uma vez e segura a pose");
        Debug.Log("BOSS CLIP: " + die.name + " length=" + die.length);
        boss.InvestidaPunitiva(true);
        boss.SendMessage("ProcessarColisao", wall);
        Check(boss.estadoAtual == CaranguejoChefe.EstadoChefe.Atordoado && boss.NumeroAtordoamento == stunned,
            "Ataque externo e contato repetido nao interrompem atordoamento");
        yield return new WaitForSeconds(2.7f);
        Check(boss.animChefe.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Die"), "Pose de Die permanece durante atordoamento");
        if (!Application.isBatchMode) { yield return new WaitForEndOfFrame(); Capture(boss.transform, "Logs/boss-stunned-die.png"); }
        yield return new WaitForSeconds(1.4f);
        Check(boss.estadoAtual == CaranguejoChefe.EstadoChefe.Perseguindo &&
            boss.animChefe.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Run Forward In Place"), "Apos quatro segundos chefe volta a perseguir");
        float originalSpeed = boss.velPerseguicao;
        radio.tempoDePaz = 1.2f;
        yield return null;
        Check(pickup.TentarColetar(inventory), "E no rádio colocado liga música");
        Check(radio.Ligado && radio.GetComponent<AudioSource>().isPlaying && radio.GetComponent<AudioSource>().loop, "Música toca em loop");
        Check(boss.EstaDancando, "Música distrai o chefe");
        boss.InvestidaPunitiva(true);
        Check(boss.EstaDancando, "Chefe não ataca durante a dança");
        yield return new WaitForSeconds(0.3f);
        if (!Application.isBatchMode) yield return new WaitForEndOfFrame();
        var dance = boss.GetComponent<DancaCaranguejo>();
        Check(dance.Ativa && dance.OssosEncontrados == 5, "Dança usa corpo, cabeça e garras do modelo real");
        Transform claw = null;
        foreach (var child in boss.GetComponentsInChildren<Transform>()) if (child.name == "RigLArm1") claw = child;
        Quaternion firstPose = claw.localRotation;
        if (!Application.isBatchMode) Capture(boss.transform, "Logs/radio-dance-1.png");
        yield return new WaitForSeconds(0.16f);
        if (!Application.isBatchMode) yield return new WaitForEndOfFrame();
        Check(Quaternion.Angle(firstPose, claw.localRotation) > 1f, "Garras mudam de pose no ritmo da dança");
        if (!Application.isBatchMode) Capture(boss.transform, "Logs/radio-dance-2.png");
        yield return new WaitForSeconds(0.9f);
        Check(!boss.EstaDancando && !dance.Ativa && boss.NivelRaivaRadio == 1 && boss.velPerseguicao > originalSpeed,
            "Chefe percebe distração e fica mais rápido");
        Check(radio != null && radio.Ligado && radio.GetComponent<AudioSource>().isPlaying, "Música continua após dança sem destruir rádio");
        float angrySpeed = boss.velPerseguicao;
        Check(boss.EstaReagindoComRaiva, "Fim da dança inicia gesto de raiva");
        Check(boss.animChefe.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Summon Attack"), "Raiva usa Summon Attack");
        var summon = boss.animChefe.GetCurrentAnimatorClipInfo(0)[0].clip;
        Check(boss.DuracaoReacaoRaiva >= summon.length, "Raiva deixa o clipe terminar antes de investir");
        Debug.Log("BOSS CLIP: " + summon.name + " length=" + summon.length);
        yield return new WaitForSeconds(boss.DuracaoReacaoRaiva + 0.1f);
        Check(boss.estadoAtual == CaranguejoChefe.EstadoChefe.Investindo, "Gesto termina e chefe avança");
        Check(pickup.TentarColetar(inventory) && !radio.Ligado && !radio.GetComponent<AudioSource>().isPlaying, "E desliga a música");
        Check(boss.NivelRaivaRadio == 2 && boss.velPerseguicao > angrySpeed, "Desligar aumenta a raiva");
        var rage = boss.GetComponent<RaivaCaranguejo>();
        Check(boss.EstaReagindoComRaiva && (rage == null || !rage.Ativa), "Desligar inicia clipe sem sobrepor gesto procedural");
        Vector3 ragePosition = boss.transform.position;
        boss.InvestidaPunitiva(true);
        Check(boss.EstaReagindoComRaiva, "Investida externa não interrompe gesto");
        yield return new WaitForSeconds(0.35f);
        Check(boss.animChefe.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Summon Attack"), "Parar a musica toca Summon Attack");
        yield return new WaitForEndOfFrame();
        Quaternion ragePose = claw.localRotation;
        Capture(boss.transform, "Logs/boss-rage-1.png");
        yield return new WaitForSeconds(0.22f);
        yield return new WaitForEndOfFrame();
        Check(Quaternion.Angle(ragePose, claw.localRotation) > 1f, "Garras se movimentam durante a raiva");
        Check(Vector3.Distance(ragePosition, boss.transform.position) < 0.02f && boss.GetComponent<Rigidbody>().linearVelocity.sqrMagnitude < 0.01f,
            "Gesto não desloca corpo nem inicia ataque");
        Capture(boss.transform, "Logs/boss-rage-2.png");
        boss.ReagirComRaiva();
        yield return new WaitForSeconds(boss.DuracaoReacaoRaiva - 0.5f);
        Check(boss.estadoAtual == CaranguejoChefe.EstadoChefe.Investindo, "Repetir não prolonga gesto e ataque retoma");
        var trumpet = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Baus/Prefabs/Trumpet_01.prefab")).GetComponent<EfeitoTrompete>();
        trumpet.tempoDeFuria = 0.1f;
        trumpet.PrepararNaMao();
        trumpet.AplicarEfeito();
        Check(boss.EstaReagindoComRaiva && (rage == null || !rage.Ativa), "Tocar trompete tambem dispara clipe de raiva");
        yield return new WaitForSeconds(0.15f);
        Check(boss.animChefe.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Summon Attack"), "Trompete toca Summon Attack");
        Object.Destroy(trumpet.gameObject);
        yield return new WaitForSeconds(boss.DuracaoReacaoRaiva + 0.15f);
        Check(boss.estadoAtual == CaranguejoChefe.EstadoChefe.Investindo, "Trompete termina gesto e avança");
        float veryAngrySpeed = boss.velPerseguicao;
        Check(pickup.TentarColetar(inventory), "Pode ligar de novo");
        yield return new WaitForSeconds(0.1f);
        Check(!boss.EstaDancando && boss.NivelRaivaRadio == 2, "Religar não reinicia distração nem apaga raiva");
        boss.FicarRapido(0.1f);
        yield return new WaitForSeconds(0.2f);
        Check(Mathf.Approximately(boss.velPerseguicao, veryAngrySpeed), "Expirar poção preserva raiva do rádio");
        boss.ReagirComRaiva();
        boss.FicarCalmo(0.2f);
        Check((rage == null || !rage.Ativa) && !boss.EstaReagindoComRaiva, "Pirulito cancela pose de raiva");
        pickup.TentarColetar(inventory);
        Check(boss.EstaCalmo, "Desligar não cancela efeito do pirulito");
        yield return new WaitForSeconds(0.3f);
        Check(!boss.EstaCalmo && boss.NivelRaivaRadio == 2, "Pirulito expira mantendo raiva");
        Check(inventory.ColetarRadio(radio) && inventory.TemRadio, "Q pode recolher rádio desligado");
        Check(inventory.ColocarRadio(), "Pode colocar o mesmo rádio novamente");
        yield return null;
        pickup.TentarColetar(inventory);
        boss.ReagirComRaiva();
        boss.TomarPocaoEncolhedora();
        Check((rage == null || !rage.Ativa) && !boss.EstaReagindoComRaiva, "Derrota cancela pose de raiva");
        pickup.TentarColetar(inventory);
        pickup.TentarColetar(inventory);
        Check(boss.estadoAtual == CaranguejoChefe.EstadoChefe.Derrotado, "Rádio não revive chefe derrotado");
        pickup.TentarColetar(inventory);
        boss.gameObject.SetActive(false);
        var earlyObject = new GameObject("Early stop boss");
        earlyObject.transform.position = inventory.transform.position + Vector3.forward * 10;
        var rigidbody = earlyObject.AddComponent<Rigidbody>();
        rigidbody.useGravity = false;
        rigidbody.constraints = RigidbodyConstraints.FreezeAll;
        var early = earlyObject.AddComponent<CaranguejoChefe>();
        early.alvoEndy = inventory.transform;
        early.estadoAtual = CaranguejoChefe.EstadoChefe.Perseguindo;
        pickup.TentarColetar(inventory);
        Check(early.EstaDancando, "Novo chefe ouve rádio");
        pickup.TentarColetar(inventory);
        Check(!early.EstaDancando && early.NivelRaivaRadio == 2, "Desligar durante dança interrompe e enfurece imediatamente");
    }

    static void Capture(Transform model, string path)
    {
        Bounds bounds = new Bounds(model.position, Vector3.zero);
        bool found = false;
        foreach (var renderer in model.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if (!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds);
        }
        var go = new GameObject("Radio preview camera");
        var camera = go.AddComponent<Camera>();
        Vector3 direction = (model.forward * 2 + model.right + Vector3.up * 0.7f).normalized;
        camera.transform.position = bounds.center + direction * Mathf.Max(2f, bounds.extents.magnitude * 2.7f);
        camera.transform.LookAt(bounds.center);
        camera.fieldOfView = 45;
        var target = new RenderTexture(900, 900, 24);
        var texture = new Texture2D(900, 900, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            target.Release();
            Object.Destroy(target);
            Object.Destroy(texture);
            Object.Destroy(go);
        }
    }
}
