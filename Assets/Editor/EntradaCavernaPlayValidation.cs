using System;
using System.IO;
using System.Linq;
using System.Reflection;
using StarterAssets;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Testa a cena real com todos os colliders criados em runtime, sem gravar save.
[InitializeOnLoad]
public static class EntradaCavernaPlayValidation
{
    const string Chave = "EntradaCavernaPlayValidation";
    const string Relatorio = "Logs/entrada-caverna-play.txt";
    static int etapa, frame, faixa;
    static float prazo;
    static Vector3 origem, frente, lateral, destino;
    static GorilaSopa gorila;
    static CharacterController capsula;
    static ThirdPersonController controle;
    static StarterAssetsInputs entradas;
    static readonly float[] Faixas = { 0f, -0.6f, 0.6f, 1.5f, 2.6f, 0f, 0f, -1.5f, -2.6f };

    static EntradaCavernaPlayValidation()
    {
        EditorApplication.update += Atualizar;
        EditorApplication.playModeStateChanged += estado =>
        {
            if (estado != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Chave, false)) return;
            var auto = UnityEngine.Object.FindAnyObjectByType<AutosaveEndy>();
            if (auto != null) auto.enabled = SessionState.GetBool(Chave + ".autosave", true);
            SessionState.SetBool(Chave, false);
        };
    }

    [MenuItem("Tools/Gorila/Sopa/Testar entrada depois da sopa %#&F9")]
    public static void Iniciar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var auto = UnityEngine.Object.FindAnyObjectByType<AutosaveEndy>();
        SessionState.SetBool(Chave + ".autosave", auto != null && auto.enabled);
        if (auto != null) auto.enabled = false;
        var cena = SceneManager.GetSceneByPath(ProgressoJogo.CenaIntegrada);
        SessionState.SetString(Chave + ".raizes", string.Join(",", cena.GetRootGameObjects().Where(r => r.activeSelf).Select(r => r.transform.GetSiblingIndex().ToString())));
        SessionState.SetBool(Chave, true);
        File.WriteAllText(Relatorio, "Play Mode: tres bananas; aproximacao desde 8 m antes do gorila ate 2,3 m atras do fundo.\n");
        etapa = 0; faixa = 0; frame = -1;
        EditorApplication.isPlaying = true;
    }

    static void Log(string s) { File.AppendAllText(Relatorio, $"{Time.time:F2}: {s}\n"); }
    static void Atualizar()
    {
        if (!SessionState.GetBool(Chave, false) || !EditorApplication.isPlaying || EditorApplication.isPaused || Time.time < 1f || frame == Time.frameCount) return;
        frame = Time.frameCount;
        Application.runInBackground = true;
        try
        {
            if (etapa == 0)
            {
                // O usuario pode ter o menu aberto de forma aditiva. O menu
                // desativa a fase no Awake; isola o teste somente em runtime.
                foreach (var menu in UnityEngine.Object.FindObjectsByType<MenuManager>(FindObjectsInactive.Include))
                    foreach (var raiz in menu.gameObject.scene.GetRootGameObjects()) raiz.SetActive(false);
                var cena = SceneManager.GetSceneByPath(ProgressoJogo.CenaIntegrada);
                SceneManager.SetActiveScene(cena);
                var raizesAtivas = SessionState.GetString(Chave + ".raizes", "").Split(',');
                foreach (var raiz in cena.GetRootGameObjects())
                    if (raizesAtivas.Contains(raiz.transform.GetSiblingIndex().ToString())) raiz.SetActive(true);
                BarreiraDaAgua.Garantir(cena);
                LimitesPrimeiraFase.Garantir(cena);
                etapa = -1;
                return;
            }
            if (etapa == -1)
            {
                var inv = UnityEngine.Object.FindAnyObjectByType<InventarioEndy>();
                controle = inv.GetComponent<ThirdPersonController>();
                Log("Movimento: andar=" + controle.MoveSpeed + "; correr=" + controle.SprintSpeed);
                capsula = inv.GetComponent<CharacterController>();
                entradas = inv.GetComponent<StarterAssetsInputs>();
                inv.GetComponent<PlayerInput>().enabled = false;
                var auto = inv.GetComponent<AutosaveEndy>();
                if (auto != null) { auto.StopAllCoroutines(); auto.enabled = false; }
                var sopa = UnityEngine.Object.FindAnyObjectByType<CaldeiraoSopa>();
                gorila = sopa.gorila; origem = gorila.transform.position;
                frente = gorila.pisoDaPassagem.transform.forward; lateral = Vector3.Cross(Vector3.up, frente);
                sopa.RestaurarProgresso(new DadosSopa { id = sopa.idSave, bananas = sopa.bananasDaReceita.Select(b => b.idSave).ToArray(), passagemLiberada = false });
                if (!sopa.SopaPronta || !gorila.GetComponent<GorilaGuardiao>().DocilPelaSopa) throw new Exception("Sopa nao iniciou a caminhada.");
                Log("Tres bananas: gorila atraido. Origem=" + origem);
                etapa = 1; prazo = Time.time + 90;
                return;
            }
            if (etapa == 1)
            {
                if (!gorila.PassagemLiberada)
                {
                    if (Time.time > prazo) throw new Exception("Gorila nao chegou a sopa; etapa=" + gorila.Etapa);
                    return;
                }
                Log("Gorila comendo em " + gorila.transform.position + "; bloqueios desligados=" + gorila.bloqueiosDaCaverna.All(c => !c.enabled));
                PrepararFaixa();
            }
            if (etapa == 2)
            {
                Vector3 delta = Vector3.ProjectOnPlane(destino - capsula.transform.position, Vector3.up);
                if (delta.magnitude < 0.18f || Time.time > prazo)
                {
                    entradas.move = Vector2.zero;
                    Log((delta.magnitude < 0.3f ? "PASS" : "FAIL") + " faixa=" + Faixas[faixa] + "; pos=" + capsula.transform.position + "; destino=" + destino);
                    foreach (var area in UnityEngine.Object.FindObjectsByType<AreaCaverna>(FindObjectsInactive.Include))
                    {
                        var caixa = area.GetComponent<BoxCollider>();
                        Log("AREA: ativa=" + area.isActiveAndEnabled + "; jogador correto=" + (area.jogador == capsula) +
                            "; caixa=" + caixa.bounds + "; centro jogador=" + capsula.bounds.center +
                            "; montanha=" + (area.colliderMontanha != null ? SopaCenaSetup.Caminho(area.colliderMontanha.transform) + " enabled=" + area.colliderMontanha.enabled : "null"));
                    }
                    if (delta.magnitude >= 0.3f) ScreenCapture.CaptureScreenshot("Logs/entrada-bloqueada-" + faixa + ".png");
                    foreach (var hit in Physics.CapsuleCastAll(capsula.bounds.center + Vector3.up * 0.55f, capsula.bounds.center - Vector3.up * 0.55f, 0.27f, delta.normalized, 2f, ~0, QueryTriggerInteraction.Ignore))
                        if (hit.collider != capsula) Log("COLISAO: " + SopaCenaSetup.Caminho(hit.transform) + "; ponto=" + hit.point);
                    foreach (var col in Physics.OverlapSphere(capsula.bounds.center, 1.5f, ~0, QueryTriggerInteraction.Ignore))
                        if (col != capsula) Log("PROXIMO: " + SopaCenaSetup.Caminho(col.transform) + "; tipo=" + col.GetType().Name);
                    faixa++;
                    if (faixa < Faixas.Length) PrepararFaixa();
                    else { Encerrar("Teste concluido."); return; }
                }
                else
                {
                    Vector3 local = Quaternion.Inverse(Quaternion.Euler(0, Camera.main.transform.eulerAngles.y, 0)) * delta.normalized;
                    entradas.move = new Vector2(local.x, local.z);
                }
            }
        }
        catch (Exception e) { Encerrar("FAIL: " + e); }
    }

    static void PrepararFaixa()
    {
        entradas.move = Vector2.zero; entradas.jump = false; entradas.sprint = false;
        capsula.enabled = false;
        capsula.transform.position = SopaCenaSetup.Solo(origem - frente * 8f + lateral * Faixas[faixa], capsula.transform) + Vector3.up * 0.2f;
        capsula.enabled = true;
        typeof(ThirdPersonController).GetField("_verticalVelocity", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controle, 0f);
        var fundo = GameObject.Find("fase_gorila/entrada_caverna/fundo_entrada_caverna").transform;
        destino = origem + frente * (Vector3.Dot(fundo.position - origem, frente) + 2.3f) + lateral * Faixas[faixa];
        if (faixa == 5) destino += frente * 5f;
        if (faixa == 6)
        {
            var altar = fundo.parent.GetComponentsInChildren<Transform>(true).First(t => t.name == "Altar_round_B");
            Vector3 direcaoAltar = Vector3.ProjectOnPlane(altar.position - origem, Vector3.up).normalized;
            destino = altar.position - direcaoAltar * 1.3f;
            Log("Altar=" + altar.position + "; fundo=" + fundo.position);
        }
        Log("INICIO trajeto=" + faixa + "; origem=" + capsula.transform.position + "; destino=" + destino);
        Physics.SyncTransforms();
        etapa = 2; prazo = Time.time + 10;
    }

    static void Encerrar(string mensagem)
    {
        Log(mensagem);
        if (entradas != null) entradas.move = Vector2.zero;
        Time.timeScale = 1;
        EditorApplication.isPlaying = false;
    }
}
