using System;
using System.IO;
using System.Reflection;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Usa uma cena temporaria e preserva inclusive alteracoes ainda nao salvas na fase.
public static class GorilaSonoValidation
{
    private static string report;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Private).Invoke(obj, args);
    private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private).SetValue(obj, value);
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        report += "PASS: " + message + "\n";
    }

    [MenuItem("Tools/Gorila/Validar sono e furtividade %#&s")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        report = "";
        var original = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        Keyboard keyboard = null;
        Mouse mouse = null;
        InputActionAsset actions = null;
        InputUser inputUser = default;
        int exitCode = 0;
        try
        {
            var origin = new Vector3(10000, 10000, 10000);
            var player = new GameObject("Endy teste sono");
            player.transform.position = origin + Vector3.forward * 2;
            var movement = player.AddComponent<ThirdPersonController>();
            var capsule = player.GetComponent<CharacterController>();
            capsule.center = Vector3.up;
            capsule.height = 2;
            capsule.radius = 0.3f;
            Set(movement, "_controller", capsule);
            var life = player.AddComponent<VidaEndy>();
            life.mostrarIndicadorVida = life.regenerarVida = false;
            life.scriptDeMovimento = movement;
            life.controleDeColisao = capsule;
            Call(life, "Awake");

            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            var input = player.GetComponent<PlayerInput>();
            actions = Object.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Starter Assets/Runtime/InputSystem/StarterAssets.inputactions"));
            input.actions = actions;
            inputUser = InputUser.PerformPairingWithDevice(keyboard);
            inputUser = InputUser.PerformPairingWithDevice(mouse, inputUser);
            inputUser.AssociateActionsWithUser(actions);
            Set(input, "m_InputUser", inputUser);
            input.SwitchCurrentControlScheme("KeyboardMouse", keyboard, mouse);
            input.SwitchCurrentActionMap("Player");
            input.ActivateInput();
            // PlayerInput nao executa OnEnable em Edit Mode: prepara somente o mapa da instancia de teste.
            var mapa = actions.FindActionMap("Player", true);
            mapa.Enable();
            Set(input, "m_CurrentActionMap", mapa);
            Set(movement, "_playerInput", input);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
            typeof(InputSystem).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(InputUpdateType) }, null).Invoke(null, new object[] { InputUpdateType.Editor });
            report += $"Entrada: scheme={input.currentControlScheme}, active={input.inputIsActive}, component={input.isActiveAndEnabled}, map={input.currentActionMap?.name}, move={input.currentActionMap?.FindAction("Move").enabled}, ctrl={Keyboard.current?.leftCtrlKey.isPressed}, scale={Time.timeScale}\n";
            Check(movement.CaminhadaLentaSolicitada && (float)Call(movement, "LimitarVelocidadeFurtiva", 12f) == 1f,
                "Ctrl limita movimento a 1 m/s mesmo com sprint e bonus");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            typeof(InputSystem).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(InputUpdateType) }, null).Invoke(null, new object[] { InputUpdateType.Editor });
            Check(!movement.CaminhadaLentaSolicitada && (float)Call(movement, "LimitarVelocidadeFurtiva", 5f) == 5f,
                "Soltar Ctrl preserva movimento normal");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Suriyun/Cute Zoo 2/Prefab/GorillaA.prefab");
            var gorilla = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            gorilla.transform.position = origin;
            var guard = gorilla.AddComponent<GorilaGuardiao>();
            guard.alvoEndy = life;
            guard.usarCicloSono = true;
            guard.causarDano = false;
            guard.intervaloAcordado = new Vector2(20, 35);
            var animator = gorilla.GetComponentInChildren<Animator>();
            animator.Rebind();
            animator.Update(0f);
            Call(guard, "Start");
            Physics.SyncTransforms();
            Check(guard.enabled && guard.EstadoSono == GorilaGuardiao.EstadoSonoGorila.Acordado, "Gorila comeca acordado");
            for (int i = 0; i < 25; i++)
            {
                Call(guard, "PrepararSono");
                float t = (float)typeof(GorilaGuardiao).GetField("tempoEstadoSono", Private).GetValue(guard);
                Check(t >= 20 && t <= 35, "Intervalo acordado dentro de 20 a 35 segundos");
            }

            Action dormir = () =>
            {
                Set(movement, "_dodgeTimeLeft", 0f);
                Set(movement, "_verticalVelocity", 0f);
                Call(movement, "CancelKnockback");
                Call(guard, "PrepararSono");
                Set(guard, "tempoEstadoSono", 0f);
                Call(guard, "AtualizarSono", 0.01f);
                animator.Update(guard.tempoAdormecer + 0.1f);
                Call(guard, "AtualizarSono", guard.tempoAdormecer + 0.1f);
            };
            dormir();
            Check(guard.EstaDormindo && animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Rest") && animator.GetInteger("animation") == 5,
                "Sono usa Rest original e parametro 5 sem sair da pose");
            Call(guard, "AplicarImpacto");
            Check(life.VidaAtual == 5 && !movement.EstaArremessado, "Dormindo nao aplica golpe");
            Call(guard, "AtualizarSono", 0f);
            Check(guard.EstaDormindo, "Pausa nao avanca sono");
            Check(!(bool)Call(guard, "AcumularRuido", 1f, false, 1f), "Caminhada lenta nao acorda");
            Check(!(bool)Call(guard, "AcumularRuido", 3f, false, 0.02f), "Oscilacao curta respeita tolerancia");
            Check((bool)Call(guard, "AcumularRuido", 3f, false, 0.1f), "Movimento rapido produz ruido");
            Set(movement, "_dodgeTimeLeft", 0.2f);
            player.transform.position = origin + Vector3.forward * 20;
            Physics.SyncTransforms();
            Call(guard, "AtualizarSono", 0.01f);
            Check(guard.EstaDormindo, "Esquiva fora do raio nao acorda");
            player.transform.position = origin + Vector3.forward * 2;
            Physics.SyncTransforms();
            Call(guard, "AtualizarSono", 0.01f);
            Check(guard.EstadoSono == GorilaGuardiao.EstadoSonoGorila.Acordando, "Esquiva proxima inicia despertar");
            Call(guard, "AplicarImpacto");
            Check(!movement.EstaArremessado, "Nao golpeia durante a transicao de despertar");
            Call(guard, "AtualizarSono", guard.tempoAcordar * 0.5f);
            Check(guard.SonoImpedeAtaque, "Metade do atraso ainda protege Endy");
            Call(guard, "AtualizarSono", guard.tempoAcordar);
            Check(!guard.SonoImpedeAtaque, "Depois do atraso pode atacar");
            Set(movement, "_dodgeTimeLeft", 0f);
            Call(guard, "IniciarAtaque");
            animator.Play(guard.estadoAtaque, 0, 0.5f);
            animator.Update(0f);
            Call(guard, "AtualizarAtaque", 0.02f);
            Check(movement.EstaArremessado && life.VidaAtual == 5, "Ataque original arremessa sem retirar vida");
            Call(guard, "CancelarAtaque");
            dormir();
            Set(guard, "tempoEstadoSono", 0f);
            Call(guard, "AtualizarSono", 0.02f);
            Check(guard.EstadoSono == GorilaGuardiao.EstadoSonoGorila.Acordando, "Fim do sono inicia mesmo atraso sem aviso");
            Call(guard, "AtualizarSono", guard.tempoAcordar + 0.1f);
            player.transform.position = origin + Vector3.forward * 20;
            Physics.SyncTransforms();
            Call(guard, "AplicarImpacto");
            Check(!movement.EstaArremessado, "Recuar durante despertar evita golpe a distancia");
            dormir();
            guard.DefinirGuarda(false);
            Check(!guard.SonoImpedeAtaque && !guard.guardandoCaverna, "Integracao futura com sopa cancela sono e guarda");
            Check(gorilla.activeSelf, "Sono nao remove o objeto nem libera a passagem");
            report += "PASS: todas as verificacoes de sono e furtividade.\n";
        }
        catch (Exception e) { exitCode = 1; report += "FAIL: " + e + "\n"; Debug.LogException(e); }
        finally
        {
            if (inputUser.valid) inputUser.UnpairDevicesAndRemoveUser();
            EditorSceneManager.CloseScene(scene, true);
            SceneManager.SetActiveScene(original);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (actions != null) Object.DestroyImmediate(actions);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/gorila-sono-validation.txt", report);
        }
        if (exitCode == 0) Debug.Log("Sono e furtividade aprovados. Veja Logs/gorila-sono-validation.txt");
        if (Application.isBatchMode) EditorApplication.Exit(exitCode);
    }
}
