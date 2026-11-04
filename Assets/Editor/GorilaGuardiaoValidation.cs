using System;
using System.IO;
using System.Reflection;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Executar em batchmode com -executeMethod GorilaGuardiaoValidation.Run.
// Usa uma cena temporaria; nunca salva ou modifica a fase do usuario.
public static class GorilaGuardiaoValidation
{
    private static string report;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object Call(object obj, string method, params object[] args)
        => obj.GetType().GetMethod(method, Private).Invoke(obj, args);
    private static void Set(object obj, string field, object value)
        => obj.GetType().GetField(field, Private).SetValue(obj, value);
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        report += "PASS: " + message + "\n";
    }

    [MenuItem("Tools/Gorila/Validar ataque e arremesso")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        report = "";
        var original = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        int exitCode = 0;
        try
        {
            var origin = new Vector3(10000f, 10000f, 10000f);
            var player = new GameObject("Endy teste");
            player.transform.position = origin + Vector3.forward * 2f;
            var movement = player.AddComponent<ThirdPersonController>();
            var capsule = player.GetComponent<CharacterController>();
            capsule.center = Vector3.up;
            capsule.height = 2f;
            capsule.radius = 0.3f;
            Set(movement, "_controller", capsule);
            var life = player.AddComponent<VidaEndy>();
            life.mostrarIndicadorVida = false;
            life.regenerarVida = false;
            life.scriptDeMovimento = movement;
            life.controleDeColisao = capsule;
            Call(life, "Awake");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Suriyun/Cute Zoo 2/Prefab/GorillaA.prefab");
            var gorilla = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            gorilla.transform.position = origin;
            var guard = gorilla.AddComponent<GorilaGuardiao>();
            guard.alvoEndy = life;
            // Reproduz o erro de arrastar o FBX Attack do Project para Anim Gorila.
            var attackAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Suriyun/Animations_Cute_Animals/Gorilla/Animal_Gorilla_Attack.fbx");
            guard.animGorila = attackAsset.GetComponent<Animator>();
            var animator = gorilla.GetComponentInChildren<Animator>();
            animator.Rebind();
            animator.Update(0f);
            Call(guard, "Start");
            Physics.SyncTransforms();
            Check(guard.animGorila == animator, "Referencia ao FBX substituida pelo Animator da instancia");
            Check(guard.enabled && animator.HasState(0, Animator.StringToHash(guard.estadoAtaque)), "Controller original contem o estado de ataque configurado");
            Check((bool)Call(guard, "AlvoAoAlcance"), "Endy proximo e detectado");
            var body = gorilla.AddComponent<BoxCollider>();
            body.size = Vector3.one * 0.2f;
            gorilla.transform.localScale = new Vector3(12f, 15f, 12f);
            Set(guard, "collidersDoCorpo", new Collider[] { body });
            Physics.SyncTransforms();
            Check((bool)Call(guard, "AlvoAoAlcance"), "Gorila ampliado detecta Endy junto ao corpo sem elevar o centro pela escala");
            Object.DestroyImmediate(body);
            gorilla.transform.localScale = Vector3.one;
            Physics.SyncTransforms();

            Action<float> frame = normalized =>
            {
                animator.Play(guard.estadoAtaque, 0, normalized);
                animator.Update(0f);
                Call(guard, "AtualizarAtaque", 0.02f);
            };
            Call(guard, "IniciarAtaque");
            frame(0.2f);
            Check(life.VidaAtual == 5, "Preparacao da animacao nao causa dano antecipado");
            frame(0.5f);
            Check(life.VidaAtual == 4 && movement.EstaArremessado, "Impacto da animacao tira exatamente uma vida e inicia arremesso");
            frame(0.7f);
            Check(life.VidaAtual == 4, "Mesmo ataque nao causa dano repetido");
            frame(1f);
            Check(!guard.EstaAtacando && animator.GetInteger("animation") == 0, "Fim do clip retorna ao repouso");
            Check((float)typeof(GorilaGuardiao).GetField("proximoAtaque", Private).GetValue(guard) > Time.time,
                "Recuperacao impede outro ataque imediato");

            var start = player.transform.position;
            Call(movement, "MoveKnockback", 0f);
            Check(player.transform.position == start && movement.EstaArremessado, "Pausa congela o arremesso");
            for (int i = 0; i < 41; i++) Call(movement, "MoveKnockback", 0.02f);
            Check(player.transform.position.z > start.z + 5f && !movement.EstaArremessado,
                "Arremesso desloca Endy para longe e libera o controle ao terminar");

            player.transform.position = origin + Vector3.forward * 2f;
            Physics.SyncTransforms();
            Call(guard, "IniciarAtaque");
            player.transform.position = origin + Vector3.forward * 10f;
            Physics.SyncTransforms();
            frame(0.5f);
            Check(life.VidaAtual == 4, "Sair do alcance antes do impacto evita dano");
            Call(guard, "CancelarAtaque");

            player.transform.position = origin + Vector3.forward * 2f;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = origin + new Vector3(0f, 1f, 1f);
            wall.transform.localScale = new Vector3(5f, 4f, 0.2f);
            Physics.SyncTransforms();
            Check(!(bool)Call(guard, "AlvoAoAlcance"), "Parede bloqueia deteccao e golpe");
            Object.DestroyImmediate(wall);
            Physics.SyncTransforms();

            Call(guard, "IniciarAtaque");
            guard.DefinirGuarda(false);
            frame(0.5f);
            Check(life.VidaAtual == 4 && !guard.EstaAtacando, "Distracao cancela golpe pendente");
            guard.DefinirGuarda(true);
            var outside = new GameObject("Ponto de expulsao");
            outside.transform.position = origin + Vector3.left * 10f;
            guard.pontoDeExpulsao = outside.transform;
            Call(guard, "IniciarAtaque");
            frame(0.5f);
            start = player.transform.position;
            Call(movement, "MoveKnockback", 0.1f);
            Check(player.transform.position.x < start.x, "Ponto de expulsao direciona golpe para fora da caverna");
            Call(guard, "CancelarAtaque");

            movement.ReceberArremesso(Vector3.forward, 12f, 0f, 0.8f, 8f);
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = player.transform.position + new Vector3(0f, 0f, 1.5f);
            wall.transform.localScale = new Vector3(10f, 10f, 0.5f);
            Physics.SyncTransforms();
            float wallZ = wall.transform.position.z;
            for (int i = 0; i < 41; i++) Call(movement, "MoveKnockback", 0.02f);
            Check(player.transform.position.z < wallZ, "Capsula impede atravessar paredes durante arremesso");
            Object.DestroyImmediate(wall);

            Set(movement, "_dodgeTimeLeft", 0.2f);
            movement.ReceberArremesso(Vector3.forward, 12f, 5f, 0.8f, 8f);
            Check(!movement.IsDodging && movement.EstaArremessado, "Impacto cancela esquiva em andamento");
            Call(movement, "OnDisable");
            Check(!movement.EstaArremessado, "Desativar movimento limpa arremesso pendente");
            Set(life, "estaMorto", true);
            Check(!(bool)Call(guard, "AlvoAoAlcance"), "Guardiao ignora Endy morto");

            report += "PASS: todas as verificacoes do guardiao\n";
        }
        catch (Exception error)
        {
            report += "FAIL: " + error + "\n";
            Debug.LogException(error);
            exitCode = 1;
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (original.IsValid()) SceneManager.SetActiveScene(original);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/gorila-validation.txt", report);
        }
        if (exitCode == 0) Debug.Log(report);
        if (Application.isBatchMode) EditorApplication.Exit(exitCode);
    }
}
