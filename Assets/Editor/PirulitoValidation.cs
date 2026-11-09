using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PirulitoValidation
{
    static PirulitoValidation() { EditorApplication.playModeStateChanged += Changed; }
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Execute esta validação em uma sessão batch separada do Unity.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("PirulitoValidation.Pending", true);
        EditorApplication.EnterPlaymode();
    }
    static void Changed(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("PirulitoValidation.Pending", false))
        {
            SessionState.SetBool("PirulitoValidation.Pending", false);
            new GameObject("Validação do pirulito").AddComponent<PirulitoValidationRunner>().Run();
        }
    }
}

public class PirulitoValidationRunner : MonoBehaviour
{
    string report = "";
    public void Run() { StartCoroutine(Guard()); }
    void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        report += "PASS: " + message + "\n";
        File.WriteAllText("Logs/pirulito-validation.txt", report);
    }
    IEnumerator Guard()
    {
        var checks = Checks();
        while (true)
        {
            object wait;
            try
            {
                if (!checks.MoveNext()) { EditorApplication.Exit(0); yield break; }
                wait = checks.Current;
            }
            catch (Exception error)
            {
                File.WriteAllText("Logs/pirulito-validation.txt", report + "FAIL: " + error);
                Debug.LogException(error);
                EditorApplication.Exit(1);
                yield break;
            }
            yield return wait;
        }
    }
    IEnumerator Checks()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Monsters Ultimate Pack 02 Cute Series/Hermit King Cute Series/Prefabs/Hermit King.prefab");
        var root = Instantiate(model);
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;
        var body = root.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeAll;
        var collider = root.AddComponent<BoxCollider>();
        collider.center = new Vector3(0, 3, 0);
        collider.size = new Vector3(6, 6, 6);
        var boss = root.AddComponent<CaranguejoChefe>();
        boss.animChefe = root.GetComponentInChildren<Animator>();
        Check(boss.animChefe.HasState(0, Animator.StringToHash("Base Layer.Comendo")), "Estado Comendo presente no Animator real");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Baus/Prefabs/Lollipop_01.prefab");
        var candy = Instantiate(prefab, new Vector3(0, 3, 5), Quaternion.identity).GetComponent<EfeitoPirulito>();
        candy.tempoDePaz = 3;
        candy.ConfigurarArremesso(null, Vector3.back * 6);
        float limit = Time.time + 2;
        while (!boss.EstaCalmo && Time.time < limit) yield return new WaitForFixedUpdate();
        Check(boss.EstaCalmo, "Colisão física do projétil inicia distração");
        var eating = boss.GetComponent<ComerPirulitoCaranguejo>();
        Check(eating != null && eating.Ativa, "Mastigação iniciada automaticamente");
        yield return new WaitForSeconds(0.8f);
        Check(boss.animChefe.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Comendo"), "Animator permanece comendo sem voltar ao ataque");
        Transform jaw = null;
        foreach (var t in root.GetComponentsInChildren<Transform>()) if (t.name == "RigJaw") jaw = t;
        Check(jaw != null, "Mandíbula do modelo encontrada");
        Quaternion pose = jaw.localRotation;
        yield return new WaitForSeconds(0.08f);
        Check(Quaternion.Angle(pose, jaw.localRotation) > 0.5f, "Mandíbula mastiga em movimento");
        var visual = root.transform.Find("Pirulito sendo devorado");
        Check(visual != null && visual.gameObject.activeInHierarchy && visual.GetComponentsInChildren<MeshRenderer>().Length > 0, "Pirulito visível durante mastigação");
        Check(visual.GetComponentsInChildren<Collider>().Length == 0 && visual.GetComponentsInChildren<Rigidbody>().Length == 0, "Visual não cria novas colisões");
        Check(root.transform.position.sqrMagnitude < 0.001f, "Mastigação não desloca o corpo físico");
        SavePreview();
        yield return new WaitForSeconds(2.4f);
        Check(!boss.EstaCalmo && !eating.Ativa && boss.estadoAtual == CaranguejoChefe.EstadoChefe.Perseguindo, "Retoma perseguição após acabar o efeito");
        Check(root.transform.Find("Pirulito sendo devorado") == null, "Visual removido ao terminar");
        boss.FicarCalmo(0.2f);
        yield return new WaitForSeconds(0.1f);
        boss.FicarCalmo(0.5f);
        yield return new WaitForSeconds(0.2f);
        Check(boss.EstaCalmo && eating.Ativa, "Novo pirulito renova a distração");
        boss.enabled = false;
        Check(!eating.Ativa && !boss.EstaCalmo, "Desativação limpa animação e efeito");
        boss.enabled = true;
        boss.FicarCalmo(3);
        boss.TomarPocaoEncolhedora();
        Check(!eating.Ativa && !boss.EstaCalmo && boss.estadoAtual == CaranguejoChefe.EstadoChefe.Derrotado, "Derrota interrompe a mastigação");
        boss.FicarCalmo(3);
        Check(!eating.Ativa && boss.estadoAtual == CaranguejoChefe.EstadoChefe.Derrotado, "Pirulito não reativa chefe derrotado");
    }
    void SavePreview()
    {
        var light = new GameObject("Luz").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 2;
        light.transform.rotation = Quaternion.Euler(45, -30, 0);
        RenderSettings.ambientLight = Color.gray;
        var camera = new GameObject("Preview").AddComponent<Camera>();
        camera.transform.position = new Vector3(8, 5, 10);
        camera.transform.LookAt(new Vector3(0, 2, 0));
        camera.backgroundColor = new Color(0.18f, 0.22f, 0.26f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        var rt = new RenderTexture(900, 700, 24);
        camera.targetTexture = rt;
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = rt;
        var image = new Texture2D(900, 700, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 900, 700), 0, 0);
        image.Apply();
        File.WriteAllBytes("Logs/pirulito-preview.png", image.EncodeToPNG());
        RenderTexture.active = previous;
        camera.targetTexture = null;
        rt.Release();
        Destroy(image); Destroy(rt); Destroy(camera.gameObject); Destroy(light.gameObject);
    }
}
