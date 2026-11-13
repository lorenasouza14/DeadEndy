using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class BeberPocaoValidation
{
    const string Pending = "BeberPocaoValidation.Pending";
    static BeberPocaoValidation() { EditorApplication.playModeStateChanged += Changed; }

    [MenuItem("Tools/Player/Configurar e validar beber pocao")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/StarterAssetsThirdPerson.controller");
        var clip = AssetDatabase.LoadAllAssetsAtPath("Assets/Endy/Animations/fbx/Rig_Medium/Rig_Medium_General.fbx")
            .OfType<AnimationClip>().Single(c => c.name == "Use_Item");
        var machine = controller.layers.Single(l => l.name == "Arremesso").stateMachine;
        var empty = machine.states.Single(s => s.state.name == "Vazio").state;
        var drink = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "BeberPocao");
        if (drink == null) drink = machine.AddState("BeberPocao", new Vector3(390, 150));
        drink.motion = clip;
        drink.speed = 1;
        drink.writeDefaultValues = true;
        var transition = drink.transitions.FirstOrDefault(t => t.destinationState == empty) ?? drink.AddTransition(empty);
        transition.hasExitTime = true;
        transition.exitTime = 1;
        transition.hasFixedDuration = true;
        transition.duration = 0.1f;
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssetIfDirty(controller);
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        new GameObject("Validação beber poção").AddComponent<BeberPocaoValidationRunner>().Run();
    }
}

public class BeberPocaoValidationRunner : MonoBehaviour
{
    string report = "";
    public void Run() { StartCoroutine(Guard()); }
    void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        report += "PASS: " + message + "\n";
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/beber-pocao-validation.txt", report);
    }
    IEnumerator Guard()
    {
        var checks = Checks();
        while (true)
        {
            object wait;
            try
            {
                if (!checks.MoveNext()) { EditorApplication.ExitPlaymode(); yield break; }
                wait = checks.Current;
            }
            catch (Exception error)
            {
                File.WriteAllText("Logs/beber-pocao-validation.txt", report + "FAIL: " + error);
                Debug.LogException(error);
                EditorApplication.ExitPlaymode();
                yield break;
            }
            yield return wait;
        }
    }
    IEnumerator Checks()
    {
        var inventory = Object.FindAnyObjectByType<InventarioEndy>();
        Check(inventory != null, "Endy real da cena encontrado");
        var movement = inventory.GetComponent<StarterAssets.ThirdPersonController>();
        movement.enabled = false;
        inventory.GetComponent<CharacterController>().enabled = false;
        foreach (var boss in Object.FindObjectsByType<CaranguejoChefe>(FindObjectsInactive.Include)) boss.gameObject.SetActive(false);
        var life = inventory.GetComponent<VidaEndy>();
        life.regenerarVida = false;
        inventory.mostrarControles = false;
        inventory.transform.SetPositionAndRotation(new Vector3(0, 1000, 0), Quaternion.identity);
        foreach (var child in inventory.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
        var animator = inventory.animEndy;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.applyRootMotion = false;
        animator.ResetTrigger("Nascer");
        animator.SetBool("Grounded", true);
        animator.SetFloat("Speed", 0);
        animator.Play("Idle Walk Run Blend", 0, 0);
        int layer = animator.GetLayerIndex("Arremesso");
        Check(layer >= 0 && animator.HasState(layer, Animator.StringToHash("Arremesso.BeberPocao")), "Clipe de beber ligado ao Animator real");
        Vector3 originalScale = inventory.transform.localScale;
        Vector3 originalPosition = inventory.transform.position;
        foreach (string letter in new[] { "C", "A" })
        {
            var potion = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Baus/Prefabs/Potion_" + letter + ".prefab")).GetComponent<PocaoMagica>();
            foreach (var child in potion.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            potion.tempoDeEfeito = 2f;
            Check(inventory.ColetarPocao(potion), "Coleta poção " + letter);
            inventory.BeberPocao();
            Check(inventory.EstaBebendoPocao && inventory.temPocao && !inventory.PodeColetarPocao, "Gesto começa mantendo frasco e mão ocupada");
            inventory.BeberPocao();
            inventory.JogarPocao();
            yield return new WaitForSeconds(0.3f);
            Check(animator.GetCurrentAnimatorStateInfo(layer).IsName("Arremesso.BeberPocao"), "Animator executa Use_Item");
            Check(potion != null && potion.transform.parent == inventory.encaixeMao && inventory.transform.localScale == originalScale && movement.MultiplicadorPocao == 1,
                "R/F repetidos não arremessam nem antecipam efeito");
            yield return new WaitForSeconds(0.45f);
            if (letter == "A")
            {
                yield return new WaitForEndOfFrame();
                Capture(inventory.gameObject);
            }
            yield return new WaitForSeconds(0.35f);
            Check(!inventory.temPocao && potion == null && inventory.EstaBebendoPocao, "Gole consome frasco antes de terminar o gesto");
            Check(letter == "A" ? inventory.transform.localScale == originalScale * 0.5f : movement.MultiplicadorPocao == 2,
                "Efeito correto aplicado após o gole: " + letter);
            yield return new WaitForSeconds(0.8f);
            Check(!inventory.EstaBebendoPocao && inventory.PodeColetarPocao && animator.GetCurrentAnimatorStateInfo(layer).IsName("Arremesso.Vazio"),
                "Fim do gesto libera mão e retorna à animação normal");
            yield return new WaitForSeconds(1.2f);
            if (letter == "A")
                Check(inventory.EstaEncolhido && inventory.transform.localScale == originalScale * 0.5f, "Endy continua pequeno após beber a verde");
            else
                Check(movement.MultiplicadorPocao == 1 && inventory.transform.localScale == originalScale, "O bônus da poção azul expira");
        }
        Check(Vector3.Distance(inventory.transform.position, originalPosition) < 0.001f, "Animação não desloca o personagem");
        var interrupted = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Baus/Prefabs/Potion_A.prefab")).GetComponent<PocaoMagica>();
        Check(inventory.ColetarPocao(interrupted), "Coleta para testar interrupção");
        inventory.BeberPocao();
        yield return new WaitForSeconds(0.2f);
        inventory.enabled = false;
        yield return new WaitForSeconds(1.7f);
        Check(!inventory.EstaBebendoPocao && inventory.temPocao && interrupted != null && inventory.EstaEncolhido && inventory.transform.localScale == originalScale * 0.5f,
            "Desativar cancela o gesto sem consumir a poção");
        inventory.enabled = true;
        inventory.BeberPocao();
        yield return new WaitForSeconds(0.2f);
        life.ForcarDerrota();
        yield return new WaitForSeconds(1.7f);
        Check(!inventory.EstaBebendoPocao && inventory.temPocao && interrupted != null, "Morte cancela o gole e preserva animação de derrota");
    }

    void Capture(GameObject player)
    {
        var cameraObject = new GameObject("Preview beber poção");
        var camera = cameraObject.AddComponent<Camera>();
        camera.cullingMask = 1 << 31;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.14f, 0.18f, 0.24f);
        camera.orthographic = true;
        camera.orthographicSize = 1.3f;
        Vector3 center = player.transform.position + Vector3.up * 1.05f;
        camera.transform.position = center + new Vector3(3, 0.3f, 5);
        camera.transform.LookAt(center);
        var texture = new RenderTexture(640, 640, 24);
        var screenshot = new Texture2D(640, 640, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            screenshot.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
            screenshot.Apply();
            File.WriteAllBytes("Logs/beber-pocao-preview.png", screenshot.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            texture.Release();
            Destroy(cameraObject); Destroy(texture); Destroy(screenshot);
        }
    }
}
