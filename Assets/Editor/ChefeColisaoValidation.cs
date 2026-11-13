using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ChefeColisaoValidation
{
    static ChefeColisaoValidation() { EditorApplication.playModeStateChanged += Changed; }
    [MenuItem("Tools/Combate/Validar investidas do chefe")]
    public static void RunInEditor()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool("ChefeColisaoValidation.Pending", true);
        EditorApplication.EnterPlaymode();
    }

    public static void Finish(int code)
    {
        if (Application.isBatchMode) EditorApplication.Exit(code);
        else EditorApplication.ExitPlaymode();
    }
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Execute em uma sessão batch separada.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("ChefeColisaoValidation.Pending", true);
        EditorApplication.EnterPlaymode();
    }
    static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("ChefeColisaoValidation.Pending", false)) return;
        SessionState.SetBool("ChefeColisaoValidation.Pending", false);
        // Isola os testes durante Play Mode, sem salvar nem modificar a cena de edição.
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            root.SetActive(false);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(
            UnityEngine.SceneManagement.SceneManager.CreateScene("Teste de investidas"));
        new GameObject("Teste colisão chefe").AddComponent<ChefeColisaoValidationRunner>().Run();
    }
}

public class ChefeColisaoValidationRunner : MonoBehaviour
{
    string report = "";
    public void Run() { StartCoroutine(Guard()); }
    void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        report += "PASS: " + message + "\n";
        File.WriteAllText("Logs/chefe-colisao-validation.txt", report);
    }
    IEnumerator Guard()
    {
        var checks = Checks();
        while (true)
        {
            object wait;
            try
            {
                if (!checks.MoveNext()) { ChefeColisaoValidation.Finish(0); yield break; }
                wait = checks.Current;
            }
            catch (Exception error)
            {
                File.WriteAllText("Logs/chefe-colisao-validation.txt", report + "FAIL: " + error);
                Debug.LogException(error);
                ChefeColisaoValidation.Finish(1);
                yield break;
            }
            yield return wait;
        }
    }
    IEnumerator Checks()
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = new Vector3(0, -0.5f, 0);
        floor.transform.localScale = new Vector3(100, 1, 100);
        var player = new GameObject("Endy de teste");
        player.tag = "Player";
        player.transform.position = new Vector3(0, 0, 10);
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f; cc.radius = 0.28f; cc.center = new Vector3(0, 0.93f, 0);
        cc.skinWidth = 0.02f; cc.stepOffset = 0.25f;
        var life = player.AddComponent<VidaEndy>();
        life.mostrarIndicadorVida = false;
        life.regenerarVida = false;
        var root = new GameObject("Chefe de teste");
        root.transform.localScale = Vector3.one * 2.5f;
        var box = root.AddComponent<BoxCollider>();
        box.center = new Vector3(0.05f, 1.23f, 0.12f);
        box.size = new Vector3(1.9f, 2.28f, 2.4f);
        var body = root.AddComponent<Rigidbody>();
        body.constraints = RigidbodyConstraints.FreezeRotation;
        var boss = root.AddComponent<CaranguejoChefe>();
        boss.alvoEndy = player.transform;
        boss.tempoEntreAtaques = boss.tempoEntreSocos = 100;
        yield return null;
        Check(player.GetComponent<ContatoChefeEndy>() != null, "Contatos de CharacterController registrados automaticamente");
        // Reproduz impulso ascendente e garante que contatos repetidos não repetem o dano.
        boss.InvestidaPunitiva(false);
        body.linearVelocity = new Vector3(25, 40, 0);
        float health = life.VidaAtual;
        boss.RegistrarContatoEndy(player);
        boss.RegistrarContatoEndy(player);
        Check(body.linearVelocity.sqrMagnitude < 0.001f, "Impacto cancela impulso horizontal e ascendente");
        Check(life.VidaAtual == health - 3, "Uma investida forte causa 3 de dano, sem duplicar callbacks");
        body.linearVelocity = new Vector3(0, -4, 0);
        boss.RegistrarContatoEndy(player);
        Check(body.linearVelocity.y == -4 && body.useGravity, "Queda e gravidade preservadas");
        yield return new WaitForSeconds(0.2f);
        // Usa dimensões da cena e colisão real, com Endy correndo e pulando contra o chefe.
        foreach (float speed in new[] { 25f, 65f })
        {
            life.SendMessage("Awake");
            cc.enabled = false;
            player.transform.position = new Vector3(0, 0, 10);
            cc.enabled = true;
            body.position = root.transform.position = Vector3.zero;
            body.linearVelocity = Vector3.zero;
            boss.velInvestida = speed;
            boss.InvestidaPunitiva(false);
            Physics.SyncTransforms();
            float highest = body.position.y;
            float playerHighest = player.transform.position.y;
            float vertical = speed > 25 ? 7f : 0;
            health = life.VidaAtual;
            for (int i = 0; i < 80; i++)
            {
                vertical = cc.isGrounded && vertical < 0 ? -2 : vertical - 15f * Time.fixedDeltaTime;
                cc.Move(new Vector3(0, vertical, -6) * Time.fixedDeltaTime);
                yield return new WaitForFixedUpdate();
                highest = Mathf.Max(highest, body.position.y);
                playerHighest = Mathf.Max(playerHighest, player.transform.position.y);
            }
            Check(life.VidaAtual == health - 3, "Colisão real acerta 3 uma vez, velocidade " + speed);
            Check(highest < 1f, "Chefe não é lançado para o alto, velocidade " + speed + "; altura " + highest);
            Check(playerHighest < (speed > 25 ? 2.5f : 0.3f), "Endy não é ejetado, velocidade " + speed + "; altura " + playerHighest);
            Check(boss.estadoAtual == CaranguejoChefe.EstadoChefe.Perseguindo, "Investida encerrada pelo contato");
        }

        // Endy parado não chama Move: o dano deve vir da varredura do chefe.
        foreach (float speed in new[] { 25f, 65f })
        {
            life.SendMessage("Awake");
            cc.enabled = false;
            player.transform.position = new Vector3(0, 0, 10);
            cc.enabled = true;
            body.position = root.transform.position = Vector3.zero;
            body.linearVelocity = Vector3.zero;
            boss.velInvestida = speed;
            Physics.SyncTransforms();
            boss.InvestidaPunitiva(true);
            yield return new WaitForSeconds(1.6f);
            Check(life.VidaAtual == life.vidaMaxima - 3, "Investida teleguiada acerta Endy parado uma vez: " + speed);
            Check(Mathf.Abs(player.transform.position.y) < 0.01f, "Endy parado permanece no chão: " + speed);
        }

        life.SendMessage("Awake");
        body.position = root.transform.position = new Vector3(0, 0, 8);
        body.linearVelocity = Vector3.zero;
        Physics.SyncTransforms();
        boss.InvestidaPunitiva(true);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Check(life.VidaAtual == life.vidaMaxima - 3, "Investida iniciada já encostada aplica dano uma vez");

        life.SendMessage("Awake");
        body.position = root.transform.position = Vector3.zero;
        body.linearVelocity = Vector3.zero;
        boss.velInvestida = 25f;
        Physics.SyncTransforms();
        boss.StartCoroutine("RotinaPrepararGiro");
        yield return new WaitForSeconds(3.2f);
        Check(life.VidaAtual == life.vidaMaxima - 1, "Investida comum mantém dano 1");

        life.SendMessage("Awake");
        body.position = root.transform.position = Vector3.zero;
        body.linearVelocity = Vector3.zero;
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = new Vector3(0, 3, 5);
        wall.transform.localScale = new Vector3(20, 6, 1);
        Physics.SyncTransforms();
        boss.InvestidaPunitiva(true);
        yield return new WaitForSeconds(1.6f);
        Check(life.VidaAtual == life.vidaMaxima, "Parede bloqueia investida sem causar dano através dela; vida=" +
            life.VidaAtual + ", chefe=" + body.position + ", Endy=" + player.transform.position);
        Destroy(wall);

        life.TomarDano(3);
        life.TomarDano(3);
        Check(life.EstaMorto && life.VidaAtual == 0, "Dano forte fatal respeita morte e limita vida a zero");
        cc.enabled = false;
        player.transform.position = new Vector3(40, 0, 40);
        boss.estadoAtual = CaranguejoChefe.EstadoChefe.Escondido;
        body.position = root.transform.position = new Vector3(0, 5, 0);
        body.linearVelocity = Vector3.zero;
        yield return new WaitForSeconds(1.5f);
        Check(body.position.y < 0.1f && body.position.y > -0.4f, "Chefe elevado volta ao chão sem ficar suspenso");
    }
}
