using System;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class RuinasPuzzleValidation
{
    private const string Chave = "RuinasPuzzleValidation.Executando";
    private const string Relatorio = "Logs/ruinas-play-validation.txt";
    private static readonly int[] Sequencia = { 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 0, 0, 0, 2 };
    private static PuzzleAltaresRuinas puzzle;
    private static int acao;
    private static int fase;
    private static int frameTecla;
    private static int[] antes;
    private static float aguardar;
    private static float inicioAcao;
    private static float alturaBanana;
    private static double fimPausa;
    private static Quaternion rotacaoPausada;
    private static bool viuTremor;
    private static bool viuFragmentos;

    static RuinasPuzzleValidation()
    {
        EditorApplication.playModeStateChanged += AoMudarModo;
    }

    [MenuItem("Tools/Gorila/Testar puzzle das ruinas %#&o")]
    public static void Iniciar()
    {
        if (EditorApplication.isPlaying)
        {
            SessionState.SetBool(Chave, true);
            AoMudarModo(PlayModeStateChange.EnteredPlayMode);
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.WriteAllText(Relatorio, "Teste do puzzle pelas alavancas e entrada E.\n");
        SessionState.SetBool(Chave, true);
        EditorApplication.isPlaying = true;
    }

    private static void AoMudarModo(PlayModeStateChange estado)
    {
        if (!SessionState.GetBool(Chave, false)) return;
        File.AppendAllText(Relatorio, "Modo: " + estado + "\n");
        if (estado == PlayModeStateChange.EnteredPlayMode)
        {
            puzzle = null;
            acao = fase = 0;
            viuTremor = viuFragmentos = false;
            aguardar = 0f;
            EditorApplication.update -= Atualizar;
            EditorApplication.update += Atualizar;
        }
        if (estado == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update -= Atualizar;
            SessionState.SetBool(Chave, false);
            Time.timeScale = 1f;
        }
    }

    private static void Exigir(bool condicao, string mensagem)
    {
        if (!condicao) throw new InvalidOperationException(mensagem);
    }

    private static void Atualizar()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
        try
        {
            if (fase == 4)
            {
                Exigir(Quaternion.Angle(rotacaoPausada, puzzle.altares[0].pivo.localRotation) < 0.001f, "Giro avancou durante pausa.");
                if (EditorApplication.timeSinceStartup < fimPausa) return;
                Time.timeScale = 1f;
                File.AppendAllText(Relatorio, "PASS: pausa congela giro e impede nova interacao.\n");
                fase = 3;
            }
            if (Time.time < aguardar) return;
            if (puzzle == null)
            {
                puzzle = UnityEngine.Object.FindAnyObjectByType<PuzzleAltaresRuinas>();
                Exigir(puzzle != null && puzzle.enabled, "Puzzle precisa estar configurado e ativo.");
                Exigir(puzzle.altares.Count(a => a.Alinhado) == 1, "Um feixe deve estar aceso no estado inicial.");
                Exigir(puzzle.monolito.banana.isKinematic, "Banana deve ficar no topo ate resolver.");
                Exigir(puzzle.altares.All(a => !puzzle.Acionar(a)), "Alavancas distantes precisam rejeitar interacao.");
                alturaBanana = puzzle.monolito.banana.position.y;
                puzzle.jogador.GetComponent<PlayerInput>().DeactivateInput();
                var entradas = puzzle.jogador.GetComponent<StarterAssets.StarterAssetsInputs>();
                entradas.move = Vector2.zero;
                entradas.jump = false;
                File.AppendAllText(Relatorio, "PASS: estado inicial, banana sustentada e limite de distancia.\n");
            }
            if (acao >= Sequencia.Length)
            {
                ConferirQueda();
                return;
            }
            var altar = puzzle.altares[Sequencia[acao]];
            if (fase == 0)
            {
                Posicionar(altar);
                antes = puzzle.altares.Select(a => a.Passos).ToArray();
                aguardar = Time.time + 0.35f;
                inicioAcao = Time.time;
                fase = 1;
            }
            else if (fase == 1)
            {
                Exigir(puzzle.AltarProximo == altar, "Alavanca inacessivel: " + altar.name);
                Exigir(altar.GetComponent<DicaInteracao>().Visivel, "Dica E precisa aparecer perto da alavanca.");
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.E));
                frameTecla = Time.frameCount;
                fase = 2;
            }
            else if (fase == 2 && Time.frameCount >= frameTecla + 2)
            {
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
                Exigir(puzzle.Ocupado, "A tecla E deve iniciar o giro.");
                for (int i = 0; i < 3; i++)
                {
                    bool gira = i == Sequencia[acao] || i == (Sequencia[acao] + 1) % 3;
                    Exigir(puzzle.altares[i].Passos == (antes[i] + (gira ? 1 : 0)) % 4, "Par de altares incorreto.");
                    if (gira) Exigir(!puzzle.altares[i].feixe.enabled, "Feixe deve apagar durante giro.");
                }
                Exigir(!puzzle.Acionar(altar), "Interacao repetida deve ser bloqueada durante giro.");
                fase = 3;
                if (acao == 0)
                {
                    Time.timeScale = 0f;
                    rotacaoPausada = puzzle.altares[0].pivo.localRotation;
                    fimPausa = EditorApplication.timeSinceStartup + 0.25;
                    Exigir(!puzzle.Acionar(altar), "Pausa deve bloquear interacao.");
                    fase = 4;
                }
            }
            else if (fase == 3 && !puzzle.Ocupado)
            {
                foreach (var a in puzzle.altares)
                {
                    Exigir(Quaternion.Angle(a.pivo.localRotation, a.RotacaoDestino) < 0.01f, "Giro precisa terminar sem deriva.");
                    Exigir(a.Alinhado == (a.Passos == 0), "Feixe deve acompanhar alinhamento: " + a.name);
                }
                File.AppendAllText(Relatorio, $"PASS: E no altar {Sequencia[acao] + 1}, passos [{string.Join(",", puzzle.altares.Select(a => a.Passos))}], feixes corretos.\n");
                acao++;
                fase = 0;
                aguardar = Time.time + 0.1f;
                if (acao == Sequencia.Length) inicioAcao = Time.time;
            }
            Exigir(Time.time - inicioAcao < 8f, "Tempo excedido aguardando giro.");
        }
        catch (Exception erro)
        {
            File.AppendAllText(Relatorio, "FAIL: " + erro + "\n");
            Debug.LogException(erro);
            Time.timeScale = 1f;
            if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            EditorApplication.isPlaying = false;
        }
    }

    private static void Posicionar(AltarRuinas altar)
    {
        var jogador = puzzle.jogador;
        var capsula = jogador.GetComponent<CharacterController>();
        float offset = jogador.transform.position.y - capsula.bounds.min.y;
        Vector3 fora = Vector3.ProjectOnPlane(altar.alavanca.position - altar.pivo.position, Vector3.up).normalized;
        Vector3 posicao = altar.alavanca.position + fora * 1f;
        var solo = Physics.RaycastAll(posicao + Vector3.up * 2f, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore)
            .Where(h => !h.transform.IsChildOf(jogador.transform)).OrderBy(h => h.distance).First();
        capsula.enabled = false;
        jogador.transform.position = solo.point + Vector3.up * (offset + 0.1f);
        jogador.transform.rotation = Quaternion.LookRotation(-fora);
        capsula.enabled = true;
        typeof(StarterAssets.ThirdPersonController).GetField("_verticalVelocity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(jogador, 0f);
        Physics.SyncTransforms();
    }

    private static void ConferirQueda()
    {
        var mono = puzzle.monolito;
        Exigir(puzzle.Resolvido && mono.Iniciado, "Alinhamento triplo deve resolver o puzzle.");
        if (mono.Desabou)
        {
            viuFragmentos |= mono.fragmentos.activeSelf;
            if (CinemachineImpulseManager.Instance.GetImpulseAt(Camera.main.transform.position, false, 8, out var deslocamento, out var rotacao))
                viuTremor |= deslocamento.sqrMagnitude > 0.000001f;
            Exigir(!mono.corpoInteiro.gameObject.activeSelf && !mono.banana.isKinematic, "Monolito deve quebrar e soltar banana.");
        }
        if (Time.time - inicioAcao < 6f) return;
        Exigir(mono.Concluido && !mono.fragmentos.activeSelf && viuFragmentos, "Fragmentos devem desabar e sumir.");
        Exigir(viuTremor, "Camera deve receber tremor do monolito.");
        Exigir(mono.banana.position.y < alturaBanana - 3f && mono.banana.linearVelocity.magnitude < 0.15f, "Banana deve cair e repousar no chao.");
        Exigir(!mono.Desmoronar() && puzzle.altares.All(a => !puzzle.Acionar(a)), "Solucao deve ser disparada uma unica vez.");
        Exigir(mono.banana.GetComponent<ItemColetavel>() == null, "Banana deve ficar disponivel para futura coleta.");
        File.AppendAllText(Relatorio, "PASS: desmoronamento, fragmentos temporarios, tremor, banana no chao e conclusao unica.\nPASS: todas as verificacoes do puzzle.\n");
        Debug.Log("Puzzle das ruinas aprovado em Play Mode.");
        EditorApplication.isPlaying = false;
    }
}
