using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class KrakenDesmoronamentoValidation
{
    const string Pasta = "Logs/KrakenDesmoronamento/";
    const string Chave = "KrakenDesmoronamentoValidation.Ativo";
    static PilarKraken[] pilares;
    static Vector3[] correnteAntes;
    static Vector3 pedraAntes;
    static Rigidbody pedra;
    static int etapa;
    static double prazo;
    static Bounds enquadramento; static Vector3 direcaoCamera;
    static bool rodando;
    static float limiteLimpeza;
    static KrakenDesmoronamentoValidation()
    {
        EditorApplication.update += Atualizar;
        EditorApplication.playModeStateChanged += Modo;
    }
    static void Exigir(bool condicao, string mensagem)
    { if (!condicao) throw new InvalidOperationException(mensagem); }
    static void Log(string mensagem) { File.AppendAllText(Pasta + "validation.txt", "PASS: " + mensagem + "\n"); }
    static void Modo(PlayModeStateChange modo)
    {
        if (!SessionState.GetBool(Chave, false)) return;
        if (modo == PlayModeStateChange.EnteredPlayMode)
        {
            try
            {
                foreach (var a in UnityEngine.Object.FindObjectsByType<AutosaveEndy>(FindObjectsSortMode.None)) a.enabled = false;
                foreach (var b in UnityEngine.Object.FindObjectsByType<BatalhaKraken>(FindObjectsSortMode.None)) { b.enabled = false; b.StopAllCoroutines(); }
                pilares = UnityEngine.Object.FindObjectsByType<PilarKraken>(FindObjectsSortMode.None);
                Exigir(pilares.Length == 4, "Apenas quatro pilares devem ter o comportamento.");
                direcaoCamera = Vector3.ProjectOnPlane(pilares[0].batalha.origemTentaculo.position - pilares[0].fragmentos.origem.GetComponent<Renderer>().bounds.center, Vector3.up).normalized; foreach (var p in pilares) p.batalha = null; // Teste visual/fisico nao altera progresso ou vitoria.
                Exigir(pilares.All(p => p.fragmentos && p.fragmentos.malhas.Length == 8 && p.balancoCorrente && p.balancoCorrente.enabled), "Configuracao incompleta.");
                enquadramento = pilares[0].fragmentos.origem.GetComponent<Renderer>().bounds;
                etapa = 0; prazo = EditorApplication.timeSinceStartup + .8; rodando = true;
                Log("quatro pilares configurados; oito fragmentos por suporte; salvamento desativado no teste");
            }
            catch (Exception e) { Falhar(e); }
        }
        if (modo == PlayModeStateChange.EnteredEditMode)
        {
            rodando = false; SessionState.SetBool(Chave, false);
        }
    }
    [MenuItem("Tools/Kraken/Testar desmoronamento e correntes")]
    public static void Iniciar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Salve a cena aberta antes de testar.");
        Directory.CreateDirectory(Pasta);
        File.WriteAllText(Pasta + "validation.txt", "Teste de fisica e balanco dos quatro pilares.\n");
        SessionState.SetBool(Chave, true);
        EditorSceneManager.OpenScene("Assets/Scenes/Recuperadas/Level_Kraken_4.unity");
        EditorApplication.isPlaying = true;
    }
    static void Atualizar()
    {
        if (!rodando || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup < prazo) return;
        try
        {
            var p = pilares[0];
            if (etapa == 0)
            {
                correnteAntes = p.balancoCorrente.visual.sharedMesh.vertices;
                Foto("inteiro.png");
                etapa = 1; prazo = EditorApplication.timeSinceStartup + .65;
            }
            else if (etapa == 1)
            {
                var agora = p.balancoCorrente.visual.sharedMesh.vertices;
                Exigir(agora.Where((v, i) => (v - correnteAntes[i]).sqrMagnitude > .0000001f).Any(), "Corrente sem movimento.");
                Exigir(agora.Where((v, i) => v == correnteAntes[i]).Count() > 8, "Extremidades da corrente nao permaneceram fixas.");
                Log("balanco visivel no meio da corrente com extremos fixos");
                p.ReceberGolpe(p.transform.position + Vector3.forward * 10);
                Exigir(p.Rachado && !p.Quebrado && !p.fragmentos.Desabou && !p.corrente.Liberado, "Primeiro golpe deve apenas rachar.");
                Log("primeiro golpe racha sem destruir ou soltar a corrente");
                p.ReceberGolpe(p.transform.position + Vector3.forward * 10);
                Exigir(p.Quebrado && p.fragmentos.QuantidadeAtiva == 8 && p.corrente.Liberado, "Segundo golpe nao liberou os oito fragmentos/corrente.");
                Exigir(!p.fragmentos.origem.GetComponent<Renderer>().enabled && p.GetComponentsInChildren<Collider>().All(c => !c.enabled), "Corpo inteiro ainda visivel ou bloqueando.");
                Exigir(pilares.Skip(1).All(x => !x.Quebrado && !x.fragmentos.Desabou), "Impacto afetou outro pilar.");
                p.ReceberGolpe(p.transform.position);
                Exigir(p.fragmentos.QuantidadeAtiva == 8, "Golpe repetido duplicou fragmentos.");
                pedra = GameObject.Find("Fragmentos do pilar Kraken").GetComponentInChildren<Rigidbody>();
                pedraAntes = pedra.position;
                etapa = 2; prazo = EditorApplication.timeSinceStartup + .5;
            }
            else if (etapa == 2)
            {
                Exigir((pedra.position - pedraAntes).sqrMagnitude > .01f, "Fragmentos nao cairam.");
                Log("segundo golpe gera oito pedras com fisica, solta corrente e ignora golpes repetidos");
                Foto("desmoronando.png");
                Time.timeScale = 0;
                pedraAntes = pedra.position;
                correnteAntes = pilares[1].balancoCorrente.visual.sharedMesh.vertices;
                etapa = 3; prazo = EditorApplication.timeSinceStartup + .3;
            }
            else if (etapa == 3)
            {
                Exigir((pedra.position - pedraAntes).sqrMagnitude < .000001f, "Pedras moveram durante pausa.");
                Exigir(correnteAntes.SequenceEqual(pilares[1].balancoCorrente.visual.sharedMesh.vertices), "Corrente moveu durante pausa.");
                Log("pausa congela pedras e correntes");
                Time.timeScale = 1;
                foreach (var outro in pilares.Skip(1))
                {
                    outro.ReceberGolpe(outro.transform.position + Vector3.forward * 10);
                    outro.ReceberGolpe(outro.transform.position + Vector3.forward * 10);
                    Exigir(outro.fragmentos.QuantidadeAtiva == 8 && outro.corrente.Liberado, "Falha em um dos quatro pilares.");
                }
                Log("os quatro pilares desmoronam individualmente");
                etapa = 4; prazo = EditorApplication.timeSinceStartup; limiteLimpeza = Time.time + 20;
            }
            else
            {
                foreach (var r in UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
                    if (r.transform.parent && r.transform.parent.name == "Fragmentos do pilar Kraken")
                    {
                        Exigir(r.transform.localScale == Vector3.one, "Fragmento encolheu.");
                        Exigir(!r.detectCollisions, "Fragmento ainda colide com o chao.");
                    }
                if (pilares.Any(x => x.fragmentos.QuantidadeAtiva > 0))
                {
                    Exigir(Time.time < limiteLimpeza, "Fragmentos nao afundaram e foram limpos.");
                    return;
                }
                Log("fragmentos mantem tamanho, atravessam o chao e sao removidos apos afundar");
                foreach (var outro in pilares)
                {
                    outro.RestaurarQuebrado();
                    Exigir(!outro.pilar.gameObject.activeSelf && !outro.corrente.gameObject.activeSelf, "Restauracao de vitoria incompleta.");
                }
                Log("fragmentos desaparecem; restauracao de checkpoint mantem pilares e correntes ocultos");
                File.AppendAllText(Pasta + "validation.txt", "RESULTADO: PASS\n");
                rodando = false; EditorApplication.isPlaying = false;
            }
        }
        catch (Exception e) { Falhar(e); }
    }
    static void Falhar(Exception e)
    {
        File.AppendAllText(Pasta + "validation.txt", "FAIL: " + e + "\n");
        Time.timeScale = 1; rodando = false; EditorApplication.isPlaying = false;
    }
    static void Foto(string nome)
    {
        // Isola a visualizacao em copias temporarias: paredes da arena nao ocultam o efeito.
        var copias = new System.Collections.Generic.List<GameObject>();
        var fontes = pilares[0].GetComponentsInChildren<MeshFilter>()
            .Concat(pilares[0].corrente.GetComponentsInChildren<MeshFilter>());
        var fragmentos = GameObject.Find("Fragmentos do pilar Kraken");
        if (fragmentos) fontes = fontes.Concat(fragmentos.GetComponentsInChildren<MeshFilter>());
        foreach (var f in fontes)
        {
            var r = f.GetComponent<MeshRenderer>();
            if (!r || !r.enabled) continue;
            var copia = new GameObject("Previa temporaria");
            copia.layer = 31;
            copia.transform.SetPositionAndRotation(f.transform.position, f.transform.rotation);
            copia.transform.localScale = f.transform.lossyScale;
            copia.AddComponent<MeshFilter>().sharedMesh = f.sharedMesh;
            copia.AddComponent<MeshRenderer>().sharedMaterials = r.sharedMaterials;
            copias.Add(copia);
        }
        var go = new GameObject("Camera temporaria de verificacao");
        var camera = go.AddComponent<Camera>();
        var alvo = enquadramento.center;
        float altura = enquadramento.size.y;
        go.transform.position = alvo + direcaoCamera * altura * .65f + Vector3.up * altura * .1f;
        go.transform.LookAt(alvo);
        camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.16f, .2f, .24f); camera.farClipPlane = 1000; camera.fieldOfView = 85;
        var rt = new RenderTexture(960, 720, 24);
        var anterior = RenderTexture.active;
        var imagem = new Texture2D(960, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            imagem.ReadPixels(new Rect(0, 0, 960, 720), 0, 0); imagem.Apply();
            File.WriteAllBytes(Pasta + nome, imagem.EncodeToPNG());
        }
        finally
        {
            foreach (var copia in copias) UnityEngine.Object.DestroyImmediate(copia); camera.targetTexture = null; RenderTexture.active = anterior;
            UnityEngine.Object.DestroyImmediate(imagem); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
