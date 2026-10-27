using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class TrompeteValidation
{
    const string Pending = "TrompeteValidation.Pending";
    static TrompeteValidation() { EditorApplication.playModeStateChanged += Changed; }
    [MenuItem("Tools/Trompete/Validar coleta e gesto")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }
    static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        new GameObject("Trumpet validation").AddComponent<TrompeteValidationRunner>().Run();
    }
    public static void Finish(Exception error)
    {
        Directory.CreateDirectory("Logs");
        string result = error == null ? "PASS: trumpet collection, mouth pose, effect and cleanup" : "FAIL: " + error;
        File.WriteAllText("Logs/trumpet-validation.txt", result);
        Debug.Log(result);
        EditorApplication.ExitPlaymode();
    }
}

public class TrompeteValidationRunner : MonoBehaviour
{
    public void Run() { StartCoroutine(Guard()); }
    IEnumerator Guard()
    {
        IEnumerator checks = Checks();
        while (true)
        {
            object wait;
            try
            {
                if (!checks.MoveNext()) { TrompeteValidation.Finish(null); yield break; }
                wait = checks.Current;
            }
            catch (Exception error) { TrompeteValidation.Finish(error); yield break; }
            yield return wait;
        }
    }
    static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        Debug.Log("TRUMPET PASS: " + message);
    }
    IEnumerator Checks()
    {
        var inventory = Object.FindAnyObjectByType<InventarioEndy>();
        Check(inventory != null, "Endy da cena encontrado");
        var movement = inventory.GetComponent<StarterAssets.ThirdPersonController>();
        if (movement != null) movement.enabled = false;
        var life = inventory.GetComponent<VidaEndy>();
        if (life != null) life.enabled = false;
        inventory.animEndy.SetFloat("Speed", 0);
        inventory.animEndy.Play("Idle Walk Run Blend", 0, 0);
        yield return null;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Baus/Prefabs/Trumpet_01.prefab");
        var trumpet = Object.Instantiate(prefab).GetComponent<EfeitoTrompete>();
        trumpet.tempoTocando = 0.7f;
        trumpet.tempoDeFuria = 0.3f;
        Check(trumpet.GetComponent<ItemColetavel>().TentarColetar(inventory), "E coleta o trompete");
        Check(inventory.TemTrompete && !trumpet.JaTocou && trumpet.transform.parent == inventory.encaixeMao,
            "Coleta segura na mão sem tocar nem consumir");
        Check(!trumpet.GetComponent<Collider>().enabled && trumpet.GetComponent<Rigidbody>().isKinematic,
            "Trompete seguro não cai nem colide");
        Check(trumpet.GetComponentInChildren<Renderer>().bounds.size.magnitude < 1f, "Tamanho da malha preservado na mão");
        inventory.BeberPocao();
        Check(inventory.TemTrompete, "R não consome trompete");
        var potion = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Baus/Prefabs/Potion_A.prefab"));
        Check(!inventory.ColetarPocao(potion.GetComponent<PocaoMagica>()), "Mão ocupada preserva outras poções");
        Object.Destroy(potion);
        var bossObject = new GameObject("Trumpet test boss");
        bossObject.transform.position = inventory.transform.position + Vector3.forward * 100;
        var rb = bossObject.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeAll;
        var boss = bossObject.AddComponent<CaranguejoChefe>();
        boss.alvoEndy = inventory.transform;
        float speed = boss.velPerseguicao;
        inventory.JogarPocao();
        inventory.JogarPocao();
        Check(inventory.EstaTocandoTrompete && !trumpet.JaTocou && boss.velPerseguicao == speed,
            "F inicia gesto sem antecipar efeito nem repetir uso");
        yield return new WaitForSeconds(0.55f);
        var pose = inventory.GetComponent<AnimacaoTrompete>();
        Check(pose.TemBracos, "Os dois braços reais do Endy participam da pose");
        Check(trumpet.JaTocou && boss.velPerseguicao > speed, "Efeito dispara após levantar o trompete");
        yield return new WaitForEndOfFrame();
        float gap = Vector3.Distance(trumpet.transform.TransformPoint(trumpet.bocalLocal), pose.PosicaoBoca);
        Check(gap < 0.02f, "Bocal encosta no alvo da boca (distância " + gap + ")");
        Capture(inventory.transform, "Logs/trumpet-mouth.png");
        yield return new WaitForSeconds(1.2f);
        Check(!inventory.TemTrompete && !inventory.EstaTocandoTrompete && inventory.PodeColetarPocao,
            "Gesto termina, consome o item e libera a mão");
        Check(boss.velPerseguicao == speed, "Fúria temporária expira");
        Check(pose.peso == 0, "Braços retornam à animação normal");
        trumpet = Object.Instantiate(prefab).GetComponent<EfeitoTrompete>();
        Check(inventory.ColetarTrompete(trumpet), "Pode coletar outro trompete");
        inventory.TocarTrompete();
        yield return new WaitForSeconds(0.1f);
        inventory.enabled = false;
        Check(!inventory.EstaTocandoTrompete && pose.peso == 0 && !trumpet.JaTocou,
            "Interromper antes do toque restaura braços e preserva item");
    }
    static void Capture(Transform player, string path)
    {
        var cameraObject = new GameObject("Trumpet preview camera");
        var camera = cameraObject.AddComponent<Camera>();
        Vector3 target = player.GetComponent<AnimacaoTrompete>().PosicaoBoca - player.up * 0.1f;
        camera.transform.position = target + player.forward * 2.2f + player.right * 1.7f + player.up * 0.4f;
        camera.transform.LookAt(target);
        camera.fieldOfView = 45;
        var rt = new RenderTexture(1000, 1000, 24);
        var texture = new Texture2D(1000, 1000, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 1000, 1000), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            rt.Release();
            Object.Destroy(rt);
            Object.Destroy(texture);
            Object.Destroy(cameraObject);
        }
    }
}
