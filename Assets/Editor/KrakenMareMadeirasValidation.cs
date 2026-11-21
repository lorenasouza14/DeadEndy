using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class KrakenMareMadeirasValidation
{
    const string Chave = "KrakenMareMadeirasValidation.Ativo";
    const string Relatorio = "Logs/KrakenDesmoronamento/madeiras.txt";
    static InundacaoKraken mare;
    static FlutuacaoSimples[] madeiras;
    static float[] alturas;
    static bool[] cinematicos;
    static Quaternion[] rotacoes;
    static float inicio, nivelAnterior, inicioCheia;
    static bool rodando, viuSubida, viuCheia, viuDescida;
    static int ultimoFrame;
    static GameObject flutuanteLivre, aguaLivre;

    static KrakenMareMadeirasValidation()
    {
        EditorApplication.playModeStateChanged += Modo;
        EditorApplication.update += Atualizar;
    }
    [MenuItem("Tools/Kraken/Testar madeiras na mare")]
    public static void Iniciar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Salve a cena aberta antes de testar.");
        Directory.CreateDirectory(Path.GetDirectoryName(Relatorio));
        File.WriteAllText(Relatorio, "Teste de subida, cheia, descida e passageiro.\n");
        SessionState.SetBool(Chave, true);
        EditorSceneManager.OpenScene("Assets/Scenes/Recuperadas/Level_Kraken_4.unity");
        EditorApplication.isPlaying = true;
    }
    static void Exigir(bool ok, string texto) { if (!ok) throw new InvalidOperationException(texto); }
    static void Log(string texto) { File.AppendAllText(Relatorio, "PASS: " + texto + "\n"); }
    static void Modo(PlayModeStateChange estado)
    {
        if (!SessionState.GetBool(Chave, false)) return;
        if (estado == PlayModeStateChange.EnteredPlayMode)
        {
            try
            {
                foreach (var a in UnityEngine.Object.FindObjectsByType<AutosaveEndy>(FindObjectsSortMode.None)) a.enabled = false;
                foreach (var b in UnityEngine.Object.FindObjectsByType<BatalhaKraken>(FindObjectsSortMode.None)) { b.enabled = false; b.StopAllCoroutines(); }
                foreach (var p in UnityEngine.Object.FindObjectsByType<StarterAssets.ThirdPersonController>(FindObjectsSortMode.None))
                {
                    p.enabled = false;
                    var c = p.GetComponent<CharacterController>(); if (c) c.enabled = false;
                    p.transform.position += Vector3.up * 200;
                }
                mare = UnityEngine.Object.FindAnyObjectByType<InundacaoKraken>();
                Exigir(mare && mare.enabled, "Inundacao ausente.");
                madeiras = UnityEngine.Object.FindObjectsByType<FlutuacaoSimples>(FindObjectsSortMode.None)
                    .Where(f => f.planoDaAgua == mare.superficieAgua).ToArray();
                Exigir(madeiras.Length > 0, "Madeiras ausentes.");
                alturas = new float[madeiras.Length]; cinematicos = new bool[madeiras.Length]; rotacoes = new Quaternion[madeiras.Length];
                inicio = Time.time; inicioCheia = -1; ultimoFrame = -1;
                viuSubida = viuCheia = viuDescida = false;
                TestarPassageiro();
                // Um corpo sem maré deve continuar recebendo o empuxo original.
                aguaLivre = new GameObject("Agua do teste livre"); aguaLivre.transform.position = new Vector3(1100, 103, 1100);
                flutuanteLivre = GameObject.CreatePrimitive(PrimitiveType.Cube); flutuanteLivre.name = "Madeira livre de teste";
                flutuanteLivre.transform.position = new Vector3(1100, 100, 1100);
                flutuanteLivre.AddComponent<Rigidbody>();
                flutuanteLivre.AddComponent<FlutuacaoSimples>().planoDaAgua = aguaLivre.transform;
                rodando = true;
            }
            catch (Exception e) { Finalizar(e); }
        }
        if (estado == PlayModeStateChange.EnteredEditMode) { rodando = false; SessionState.SetBool(Chave, false); }
    }
    static void TestarPassageiro()
    {
        var t = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var j = new GameObject("Passageiro do teste");
        try
        {
            t.transform.position = new Vector3(1000, 100, 1000); t.transform.localScale = new Vector3(6, 1, 6);
            var rb = t.AddComponent<Rigidbody>(); rb.useGravity = false;
            var f = t.AddComponent<FlutuacaoSimples>();
            var cc = j.AddComponent<CharacterController>(); cc.height = 2; cc.radius = .3f; cc.center = Vector3.zero;
            j.transform.position = new Vector3(1000, 101.5f, 1000);
            Physics.SyncTransforms();
            f.AcompanharSubida(99.5f, 99.5f, cc);
            float y = j.transform.position.y;
            f.AcompanharSubida(99.5f, 101.15f, cc);
            Exigir(Mathf.Abs(j.transform.position.y - y - 1f) < .06f, "Passageiro nao acompanhou a subida.");
            y = j.transform.position.y;
            f.AcompanharSubida(101.15f, 100.65f, cc);
            Exigir(Mathf.Abs(j.transform.position.y - y + .5f) <= cc.skinWidth + .02f, "Passageiro nao acompanhou a descida. delta=" + (j.transform.position.y-y) + " rb=" + rb.position.y + " transform=" + t.transform.position.y + " topo=" + t.GetComponent<Collider>().bounds.max.y);
            var pos = t.transform.position;
            Time.timeScale = 0; f.AcompanharSubida(100.65f, 103, cc);
            Exigir(t.transform.position == pos, "Madeira se moveu durante pausa.");
            Time.timeScale = 1;
            f.enabled = false;
            Exigir(!rb.isKinematic && !rb.useGravity, "Estado fisico original nao restaurado ao desativar.");
            Log("passageiro acompanha subida e descida; pausa respeitada; estado original restaurado");
        }
        finally { Time.timeScale = 1; UnityEngine.Object.Destroy(t); UnityEngine.Object.Destroy(j); }
    }
    static void Atualizar()
    {
        if (!rodando || !EditorApplication.isPlaying || ultimoFrame == Time.frameCount) return;
        ultimoFrame = Time.frameCount;
        try
        {
            if (Time.time - inicio < .8f) return;
            if (!viuSubida && !mare.EmExecucao)
            {
                Exigir(flutuanteLivre.GetComponent<Rigidbody>().linearVelocity.y > 0, "Empuxo das outras fases foi afetado.");
                UnityEngine.Object.Destroy(flutuanteLivre); UnityEngine.Object.Destroy(aguaLivre);
                Log("flutuacao fisica de objetos sem mare preservada");
                mare.tempoAviso = .1f; mare.tempoMergulho = .1f; mare.atrasoAposMergulho = 0;
                mare.tempoSubida = 2; mare.tempoCheia = 1; mare.tempoDescida = 2; mare.tempoRetorno = .1f;
                mare.Solicitar(); mare.StartCoroutine(mare.Executar());
                for (int i = 0; i < madeiras.Length; i++)
                { alturas[i] = madeiras[i].transform.position.y; cinematicos[i] = false; rotacoes[i] = madeiras[i].transform.rotation; }
                nivelAnterior = mare.NivelAtual;
            }
            if (mare.Fase == InundacaoKraken.EtapaMare.Subindo) viuSubida = true;
            if (mare.Fase == InundacaoKraken.EtapaMare.Cheia)
            { viuCheia = true; if (inicioCheia < 0) inicioCheia = Time.time; }
            if (mare.Fase == InundacaoKraken.EtapaMare.Baixando) viuDescida = true;
            for (int i = 0; i < madeiras.Length; i++)
            {
                var m = madeiras[i]; var rb = m.GetComponent<Rigidbody>();
                float dy = rb.position.y - alturas[i];
                if (rb.isKinematic && cinematicos[i])
                {
                    Exigir(Vector3.Distance(rb.position, m.transform.position) < .001f, "Malha atrasada em relacao ao colisor: " + m.name); Exigir(Quaternion.Angle(rb.rotation, rotacoes[i]) < .01f, "Madeira girou/brincou com a fisica: " + m.name);
                    if (mare.Fase == InundacaoKraken.EtapaMare.Subindo) Exigir(dy >= -.002f, "Pulo para baixo durante subida: " + m.name);
                    if (mare.Fase == InundacaoKraken.EtapaMare.Baixando) Exigir(dy <= .002f, "Pulo para cima durante descida: " + m.name);
                    if (mare.Fase == InundacaoKraken.EtapaMare.Cheia && Time.time - inicioCheia > .4f)
                        Exigir(Mathf.Abs(dy) < .002f, "Madeira quicou na cheia: " + m.name);
                }
                alturas[i] = rb.position.y; cinematicos[i] = rb.isKinematic; rotacoes[i] = rb.rotation;
            }
            nivelAnterior = mare.NivelAtual;
            if (viuDescida && !mare.EmExecucao)
            {
                Exigir(viuCheia && cinematicos.All(x => x), "Nem todas as madeiras acompanharam a mare.");
                Log(madeiras.Length + " madeiras sobem/descem sem inversoes, sem rotacao e sem pulos na cheia");
                Finalizar(null);
            }
            Exigir(Time.time - inicio < 15, "Ciclo da mare excedeu o limite.");
        }
        catch (Exception e) { Finalizar(e); }
    }
    static void Finalizar(Exception erro)
    {
        File.AppendAllText(Relatorio, erro == null ? "RESULTADO: PASS\n" : "FAIL: " + erro + "\n");
        Time.timeScale = 1; rodando = false; EditorApplication.isPlaying = false;
    }
}
