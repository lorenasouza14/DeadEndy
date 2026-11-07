using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using StarterAssets;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class ArmadilhasRampaValidation
{
    private const string ChavePlay = "ArmadilhasRampaValidation.Play";
    private static ArmadilhaMola[] trapsPlay;
    private static ThirdPersonController endyPlay;
    private static CharacterController capsulaPlay;
    private static VidaEndy vidaPlay;
    private static int indicePlay;
    private static int vidaInicial;
    private static float proximoTeste;
    private static float inicioTeste;
    private static bool posicionado;
    private static bool lancou;
    private static bool animou;

    static ArmadilhasRampaValidation()
    {
        EditorApplication.playModeStateChanged += EstadoPlay;
    }

    [MenuItem("Tools/Gorila/Testar traps em Play Mode %#&y")]
    public static void TestarPlay()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(ChavePlay, true);
        File.WriteAllText("Logs/spring-play-validation.txt", "Iniciando teste com o Endy da cena.\n");
        EditorApplication.isPlaying = true;
    }

    private static void EstadoPlay(PlayModeStateChange estado)
    {
        if (!SessionState.GetBool(ChavePlay, false)) return;
        if (estado == PlayModeStateChange.EnteredPlayMode)
        {
            proximoTeste = Time.time + 2f;
            indicePlay = 0;
            posicionado = false;
            trapsPlay = null;
            EditorApplication.update += AtualizarPlay;
        }
        else if (estado == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update -= AtualizarPlay;
            SessionState.SetBool(ChavePlay, false);
        }
    }

    private static void AtualizarPlay()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isPaused || Time.time < proximoTeste) return;
        try
        {
            if (trapsPlay == null)
            {
                var pedra = GameObject.Find(ArmadilhasRampaSetup.NomePedra);
                var todas = pedra.GetComponentsInChildren<ArmadilhaMola>().OrderBy(t => t.transform.parent.name).ToArray();
                trapsPlay = todas.Concat(new[] { todas[0] }).ToArray();
                endyPlay = Object.FindAnyObjectByType<ThirdPersonController>();
                capsulaPlay = endyPlay.GetComponent<CharacterController>();
                vidaPlay = endyPlay.GetComponentInChildren<VidaEndy>();
                vidaInicial = vidaPlay.VidaAtual;
                endyPlay.GetComponent<UnityEngine.InputSystem.PlayerInput>().DeactivateInput();
                var inputs = endyPlay.GetComponent<StarterAssetsInputs>();
                inputs.move = Vector2.zero;
                inputs.jump = false;
                inputs.sprint = false;
            }
            if (indicePlay >= trapsPlay.Length)
            {
                File.AppendAllText("Logs/spring-play-validation.txt", "PASS: 20 triggers reais, animacoes, quedas e repeticao da primeira trap.\n");
                EditorApplication.isPlaying = false;
                return;
            }
            var trap = trapsPlay[indicePlay];
            if (!posicionado)
            {
                float offset = endyPlay.transform.position.y - capsulaPlay.bounds.min.y;
                var pedra = GameObject.Find(ArmadilhasRampaSetup.NomePedra).GetComponent<MeshCollider>();
                pedra.Raycast(new Ray(trap.transform.position + Vector3.up * 10f, Vector3.down), out var hit, 30f);
                capsulaPlay.enabled = false;
                endyPlay.transform.position = hit.point + Vector3.up * (offset + 0.6f);
                capsulaPlay.enabled = true;
                Campo(endyPlay, "_verticalVelocity", 0f);
                endyPlay.Grounded = false;
                Physics.SyncTransforms();
                inicioTeste = Time.time;
                posicionado = true;
                lancou = animou = false;
                return;
            }
            lancou |= endyPlay.EstaArremessado;
            animou |= trap.Disparada && trap.animador.speed > 0f &&
                trap.transform.Find("Obstacle-8.001").localScale.y > 0.3f;
            if (lancou && !endyPlay.EstaArremessado)
            {
                float erro = Vector3.ProjectOnPlane(endyPlay.transform.position - trap.pontoDeQueda.position, Vector3.up).magnitude;
                Verificar(erro < 1f && animou && vidaPlay.VidaAtual == vidaInicial,
                    $"Falha em {trap.transform.parent.name}: erro={erro:F3}, animacao={animou}, vida={vidaPlay.VidaAtual}");
                File.AppendAllText("Logs/spring-play-validation.txt", $"PASS: {trap.transform.parent.name}, animacao e queda, erro={erro:F3}\n");
                indicePlay++;
                posicionado = false;
                proximoTeste = Time.time + 0.5f;
            }
            Verificar(Time.time - inicioTeste < 12f, "Trigger nao ativou ou arremesso nao terminou em " + trap.transform.parent.name);
        }
        catch (Exception erro)
        {
            File.AppendAllText("Logs/spring-play-validation.txt", "FAIL: " + erro + "\n");
            Debug.LogException(erro);
            EditorApplication.isPlaying = false;
        }
    }

    private const BindingFlags Privado = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object Chamar(object obj, string metodo, params object[] args)
        => obj.GetType().GetMethod(metodo, Privado).Invoke(obj, args);
    private static void Campo(object obj, string nome, object valor)
        => obj.GetType().GetField(nome, Privado).SetValue(obj, valor);
    private static void Verificar(bool sucesso, string mensagem)
    {
        if (!sucesso) throw new InvalidOperationException(mensagem);
    }

    [MenuItem("Tools/Gorila/Validar armadilhas da rampa %#&v")]
    public static void Validar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var report = new StringBuilder();
        GameObject jogador = null;
        GameObject testeMola = null;
        try
        {
            var pedra = GameObject.Find(ArmadilhasRampaSetup.NomePedra);
            var traps = pedra.GetComponentsInChildren<ArmadilhaMola>();
            Verificar(traps.Length == 20, "Devem existir 20 armadilhas configuradas.");
            Verificar(pedra.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Moita segura "))
                .All(t => t.GetComponentInChildren<ArmadilhaMola>() == null), "Moitas seguras precisam estar livres de armadilhas.");
            var original = Object.FindAnyObjectByType<ThirdPersonController>();
            var capsulaOriginal = original.GetComponent<CharacterController>();
            jogador = new GameObject("Validacao temporaria das traps");
            jogador.layer = original.gameObject.layer;
            jogador.transform.localScale = original.transform.lossyScale;
            var movimento = jogador.AddComponent<ThirdPersonController>();
            var capsula = jogador.GetComponent<CharacterController>();
            EditorUtility.CopySerialized(capsulaOriginal, capsula);
            movimento.Gravity = original.Gravity;
            Campo(movimento, "_controller", capsula);
            var vida = jogador.AddComponent<VidaEndy>();
            Chamar(vida, "Awake");
            int vidaAntes = vida.VidaAtual;

            foreach (var trap in traps)
            foreach (float passo in new[] { 1f / 60f, 1f / 30f, 0.1f })
            {
                Chamar(movimento, "CancelKnockback");
                capsula.enabled = false;
                jogador.transform.position = trap.transform.position + Vector3.up * 2f;
                capsula.enabled = true;
                Physics.SyncTransforms();
                float origemAoPe = jogador.transform.position.y - capsula.bounds.min.y;
                var hits = Physics.RaycastAll(trap.transform.position + Vector3.up * 10f, Vector3.down, 30f, ~0,
                    QueryTriggerInteraction.Ignore).Where(h => !h.transform.IsChildOf(jogador.transform)).OrderBy(h => h.distance);
                var solo = hits.First();
                capsula.enabled = false;
                jogador.transform.position = solo.point + Vector3.up * (origemAoPe + 0.3f);
                capsula.enabled = true;
                Physics.SyncTransforms();
                Vector3 destino = trap.pontoDeQueda.position + Vector3.up * (origemAoPe + capsula.skinWidth + 0.05f);
                Verificar(movimento.ReceberCatapulta(destino, trap.alturaDoArco), "Catapulta deve aceitar jogador disponivel.");
                Verificar(!movimento.ReceberCatapulta(destino, trap.alturaDoArco), "Arremesso em andamento nao pode ser reiniciado.");
                Vector3 inicio = jogador.transform.position;
                Chamar(movimento, "MoveKnockback", 0f);
                Verificar(jogador.transform.position == inicio, "Pausa deve congelar a catapulta.");
                int frames = 0;
                while (movimento.EstaArremessado && frames++ < 1500) Chamar(movimento, "MoveKnockback", passo);
                float erro = Vector3.ProjectOnPlane(jogador.transform.position - destino, Vector3.up).magnitude;
                report.AppendLine($"{trap.transform.parent.name} passo={passo:F3} erro={erro:F3} pos={jogador.transform.position:F3}");
                Verificar(!movimento.EstaArremessado && erro < 1f, "Arremesso interrompido antes do pe do morro: " + trap.transform.parent.name);
                Verificar(vida.VidaAtual == vidaAntes, "Trap deve preservar a vida.");
            }
            report.AppendLine("PASS: 60 trajetorias com colisoes, pausa e bloqueio de arremessos simultaneos.");

            testeMola = Object.Instantiate(traps[0].gameObject);
            var mola = testeMola.GetComponent<ArmadilhaMola>();
            mola.animador.Rebind();
            mola.animador.Update(0f);
            Chamar(mola, "Awake");
            Chamar(mola, "Start");
            Verificar(mola.animador.speed == 0f, "Mola deve ficar recolhida em repouso.");
            var visual = testeMola.transform.Find("Obstacle-8.001");
            float recolhida = visual.localScale.y;
            mola.animador.speed = 1f;
            mola.animador.Play(mola.estadoAnimacao, 0, 0f);
            mola.animador.Update(0f);
            mola.animador.Update(0.25f);
            report.AppendLine($"Mola recolhida={recolhida:F3}, estendida={visual.localScale.y:F3}");
            Verificar(visual.localScale.y > recolhida * 2f, "Animacao original precisa estender a mola.");
            Chamar(mola, "Repousar");
            Verificar(Mathf.Abs(visual.localScale.y - recolhida) < 0.001f, "Rearmar deve recolher a mola.");
            report.AppendLine("PASS: animacao original estende e recolhe a mola.");

            var banana = pedra.GetComponentsInChildren<Transform>().Single(t => t.name == "Banana do altar");
            Verificar(banana.GetComponents<MonoBehaviour>().Length == 0, "Banana deve ser apenas visual.");
            var bananaBounds = banana.GetComponent<Renderer>().bounds;
            var altarBounds = banana.parent.GetComponent<Renderer>().bounds;
            Verificar(Mathf.Abs(bananaBounds.min.y - altarBounds.max.y - 0.03f) < 0.01f, "Banana deve estar sobre o altar.");
            Verificar(banana.GetComponent<Renderer>().sharedMaterial.shader.name.StartsWith("Universal Render Pipeline/"), "Material da banana deve ser URP.");
            report.AppendLine("PASS: banana posicionada no altar com material URP.");
            movimento.ReceberCatapulta(jogador.transform.position + Vector3.forward * 10f, 7f);
            Chamar(movimento, "OnDisable");
            Verificar(!movimento.EstaArremessado, "Desativar jogador deve cancelar a catapulta.");
            report.AppendLine("PASS: cancelamento ao desativar jogador.");
        }
        catch (Exception erro)
        {
            report.AppendLine("FAIL: " + erro);
            Debug.LogException(erro);
        }
        finally
        {
            if (testeMola != null) Object.DestroyImmediate(testeMola);
            if (jogador != null) Object.DestroyImmediate(jogador);
            File.WriteAllText("Logs/spring-validation.txt", report.ToString());
            Debug.Log(report.ToString());
        }
    }
}
