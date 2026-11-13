using System;
using System.IO;
using System.Linq;
using System.Reflection;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class GorilaDanoValidation
{
    const string Chave = "GorilaDanoValidation.Ativa";
    const string Relatorio = "Logs/gorila-dano-terco.txt";
    static GorilaGuardiao gorila;
    static VidaEndy vida;
    static ThirdPersonController movimento;
    static CharacterController capsula;
    static int golpes;
    static float inicio, proximo;
    static bool reposicionar;
    static readonly BindingFlags Privado = BindingFlags.NonPublic | BindingFlags.Instance;
    static GorilaDanoValidation() { EditorApplication.update += Atualizar; }

    [MenuItem("Tools/Gorila/Ativar dano de um terco na fase %#&h")]
    public static void Configurar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var cena = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (cena.name != "Demo_day_fase_gorilla") throw new InvalidOperationException("Abra a fase do gorila.");
        var g = UnityEngine.Object.FindObjectsByType<GorilaGuardiao>(FindObjectsInactive.Include).Single(x => x.gameObject.scene == cena);
        Undo.RecordObject(g, "Ativar dano de um terco");
        g.causarDano = true;
        PrefabUtility.RecordPrefabInstancePropertyModifications(g);
        EditorUtility.SetDirty(g);
        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        Selection.activeGameObject = g.gameObject;
        Debug.Log("Gorila configurado: cada impacto tira 1/3 da vida maxima. Cena salva.", g);
    }

    [MenuItem("Tools/Gorila/Testar dano de um terco na fase %#&n")]
    public static void Testar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Directory.CreateDirectory("Logs"); File.WriteAllText(Relatorio, "Teste de dano proporcional e tres golpes reais na fase.\n");
        gorila = null; SessionState.SetBool(Chave, true); EditorApplication.isPlaying = true;
    }
    static void Exigir(bool ok, string texto)
    { if (!ok) throw new InvalidOperationException(texto); File.AppendAllText(Relatorio, "PASS: " + texto + "\n"); }
    static void ConferirVidaFracionada()
    {
        foreach (int maximo in new[] { 1, 5, 10, 100 })
        {
            var go = new GameObject("Teste temporario de vida fracionada"); go.SetActive(false);
            var v = go.AddComponent<VidaEndy>(); v.vidaMaxima = maximo; v.regenerarVida = false; v.mostrarIndicadorVida = false; go.SetActive(true);
            try
            {
                v.TomarDano(maximo / 3f);
                Exigir(Mathf.Approximately(v.FracaoVida, 2f / 3f) && !v.EstaMorto, "Vida maxima " + maximo + ": primeiro golpe deixa 2/3.");
                v.TomarDano(maximo / 3f);
                Exigir(Mathf.Approximately(v.FracaoVida, 1f / 3f) && !v.EstaMorto, "Vida maxima " + maximo + ": segundo golpe deixa 1/3.");
                v.TomarDano(maximo / 3f);
                Exigir(v.EstaMorto && v.VidaAtual == 0, "Vida maxima " + maximo + ": terceiro golpe mata sem residuo fracionario.");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        var obj = new GameObject("Teste temporario de recuperacao"); obj.SetActive(false);
        var life = obj.AddComponent<VidaEndy>(); life.mostrarIndicadorVida = false; obj.SetActive(true);
        try
        {
            life.TomarDano(); Exigir(life.VidaAtual == 4, "Ataques antigos continuam retirando um ponto.");
            Time.timeScale = 0; life.TomarDano(5f/3f); Time.timeScale = 1;
            Exigir(life.VidaAtual == 4, "Pause impede dano proporcional.");
            life.TomarDano(-1); life.TomarDano(0); life.TomarDano(float.NaN); life.TomarDano(float.PositiveInfinity);
            Exigir(life.VidaAtual == 4, "Dano invalido nao altera vida.");
            life.TomarDano(5f/3f);
            typeof(VidaEndy).GetMethod("AtualizarRecuperacao", Privado).Invoke(life,new object[] {20f});
            Exigir(life.VidaAtual == 5 && !life.EstaRegenerando, "Recuperacao fracionada para na vida maxima.");
        }
        finally { Time.timeScale = 1; UnityEngine.Object.DestroyImmediate(obj); }
    }
    static void Posicionar()
    {
        var p = gorila.transform.position + Vector3.back * 3.65f + Vector3.right * 0.3f;
        p = SopaCenaSetup.Solo(p, vida.transform) + Vector3.up * 0.25f;
        capsula.enabled = false; vida.transform.position = p; vida.transform.rotation = Quaternion.identity; capsula.enabled = true;
        typeof(ThirdPersonController).GetField("_verticalVelocity", Privado).SetValue(movimento,0f);
        var input = vida.GetComponent<StarterAssetsInputs>(); input.move = Vector2.zero; input.jump = false; input.sprint = false;
        Physics.SyncTransforms();
    }
    static void Atualizar()
    {
        if (!SessionState.GetBool(Chave, false) || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
        Application.runInBackground = true;
        if (Time.time < 2f) return;
        try
        {
            if (gorila == null)
            {
                ConferirVidaFracionada();
                gorila = UnityEngine.Object.FindAnyObjectByType<GorilaGuardiao>(); vida = gorila.alvoEndy;
                movimento = vida.GetComponent<ThirdPersonController>(); capsula = vida.GetComponent<CharacterController>();
                Exigir(gorila.causarDano, "Dano esta habilitado na instancia salva da fase.");
                // Apenas nesta execucao: elimina esperas aleatorias e cura entre os tres golpes.
                gorila.usarCicloSono = false; vida.regenerarVida = false;
                golpes = 0; inicio = Time.time; reposicionar = false; Posicionar();
            }
            if (Time.time - inicio > 35) throw new Exception("Tempo excedido aguardando ataques reais.");
            float esperadoAntes = vida.vidaMaxima * (1f - golpes / 3f);
            if (vida.VidaAtual < esperadoAntes - 0.0001f)
            {
                golpes++;
                Exigir(Mathf.Approximately(vida.FracaoVida, (3-golpes)/3f), "Golpe real " + golpes + ": vida " + vida.VidaAtual.ToString("F4") + "/" + vida.vidaMaxima);
                Exigir(golpes==3 ? vida.EstaMorto && !movimento.enabled && !capsula.enabled : movimento.EstaArremessado && !vida.EstaMorto,
                    golpes==3 ? "Terceiro golpe dispara a morte existente." : "Dano acompanha impacto e arremesso.");
                reposicionar = true; proximo = Time.time + 1.1f;
                if (golpes==3) { Finalizar(null); return; }
            }
            if (reposicionar && Time.time>=proximo && !movimento.EstaArremessado && !gorila.EstaAtacando)
            { Posicionar(); reposicionar=false; }
        }
        catch(Exception e) { Finalizar(e); }
    }
    static void Finalizar(Exception erro)
    {
        File.AppendAllText(Relatorio, erro==null ? "PASS: verificacoes completas.\n" : "FAIL: "+erro+"\n");
        SessionState.SetBool(Chave,false); Time.timeScale=1;
        if(erro!=null) Debug.LogException(erro); else Debug.Log("Dano de 1/3 validado, incluindo tres ataques reais na fase.");
        EditorApplication.isPlaying=false;
    }
}
