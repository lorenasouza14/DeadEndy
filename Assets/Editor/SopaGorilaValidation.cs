using System;
using System.IO;
using System.Reflection;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Verificacao com objetos reais numa cena temporaria, sem modificar a fase aberta.
public static class SopaGorilaValidation
{
    private const BindingFlags Privado = BindingFlags.NonPublic | BindingFlags.Instance;
    private static string report;
    private static object Call(object alvo, string metodo, params object[] args) => alvo.GetType().GetMethod(metodo, Privado).Invoke(alvo, args);
    private static void Set(object alvo, string campo, object valor) => alvo.GetType().GetField(campo, Privado).SetValue(alvo, valor);
    private static void Check(bool ok, string texto)
    {
        if (!ok) throw new Exception(texto);
        report += "PASS: " + texto + "\n";
    }

    [MenuItem("Tools/Gorila/Sopa/Validar coleta e sopa %#&b")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var original = SceneManager.GetActiveScene();
        var temporaria = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(temporaria);
        var random = UnityEngine.Random.state;
        float escalaTempo = Time.timeScale;
        report = "";
        bool sucesso = false;
        try
        {
            Time.timeScale = 1;
            var origem = new Vector3(10000, 10000, 10000);
            var jogador = new GameObject("Endy teste sopa");
            jogador.transform.position = origem;
            var movimento = jogador.AddComponent<ThirdPersonController>();
            var capsula = jogador.GetComponent<CharacterController>();
            capsula.center = Vector3.up;
            capsula.height = 2;
            capsula.radius = 0.3f;
            Set(movimento, "_controller", capsula);
            var vida = jogador.AddComponent<VidaEndy>();
            vida.mostrarIndicadorVida = vida.regenerarVida = false;
            vida.scriptDeMovimento = movimento;
            vida.controleDeColisao = capsula;
            Call(vida, "Awake");
            var inventario = jogador.AddComponent<InventarioEndy>();
            var mao = new GameObject("handslot.r").transform;
            mao.SetParent(jogador.transform, false);
            mao.localPosition = new Vector3(0.4f, 1, 0.2f);
            Call(inventario, "Awake");
            Check(inventario.encaixeMao == mao, "Reaproveita encaixe existente da mao");

            var gorilaObj = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Suriyun/Cute Zoo 2/Prefab/GorillaA.prefab"), temporaria);
            gorilaObj.transform.position = origem + Vector3.forward * 10;
            var guarda = gorilaObj.AddComponent<GorilaGuardiao>();
            guarda.alvoEndy = vida;
            guarda.usarCicloSono = true;
            guarda.causarDano = false;
            var anim = gorilaObj.GetComponentInChildren<Animator>();
            anim.Rebind(); anim.Update(0);
            Call(guarda, "Start");
            var gorila = gorilaObj.AddComponent<GorilaSopa>();
            var destino = new GameObject("Ponto para comer").transform;
            destino.position = origem + Vector3.forward * 16;
            gorila.percurso = new[] { destino };
            var panela = new GameObject("Caldeirao teste");
            panela.transform.position = origem + Vector3.right;
            var caldeirao = panela.AddComponent<CaldeiraoSopa>();
            caldeirao.jogador = inventario;
            caldeirao.gorila = gorila;
            gorila.olharParaSopa = panela.transform;
            var bloqueio = new GameObject("Bloqueio de passagem").AddComponent<BoxCollider>();
            bloqueio.transform.position = origem + Vector3.forward * 20;
            gorila.bloqueiosDaCaverna = new Collider[] { bloqueio };

            var bananas = new BananaSopa[3];
            var itens = new ItemColetavel[3];
            for (int i = 0; i < 3; i++)
            {
                var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.name = "Banana teste " + i;
                obj.transform.position = origem + Vector3.right * (5 + i * 2) + Vector3.up;
                obj.transform.localScale = Vector3.one * 0.3f;
                obj.AddComponent<Rigidbody>().isKinematic = true;
                bananas[i] = obj.AddComponent<BananaSopa>();
                var sensor = new GameObject("InteracaoBanana");
                sensor.transform.SetParent(obj.transform, false);
                sensor.AddComponent<SphereCollider>().isTrigger = true;
                itens[i] = sensor.AddComponent<ItemColetavel>();
            }
            caldeirao.bananasDaReceita = bananas;
            Check(caldeirao.ReceitaValida(), "Receita com tres bananas e animacoes Walk/Eat originais valida");
            caldeirao.bananasDaReceita = new[] { bananas[0], bananas[0], bananas[2] };
            Check(!caldeirao.ReceitaValida(), "Receita duplicada e rejeitada");
            caldeirao.bananasDaReceita = bananas;
            bananas[0].transform.position = origem + Vector3.forward + Vector3.up;
            Physics.SyncTransforms();
            Call(itens[0], "RegistrarContato", capsula);
            Check(itens[0].ObterDica() == "[E] Pegar banana", "Aviso E reutiliza ItemColetavel");
            bananas[0].gorilaQueProtege = guarda;
            Check(!itens[0].TentarColetar(inventario), "Banana do santuario bloqueada enquanto gorila acordado");
            Set(guarda, "tempoEstadoSono", 0f);
            Call(guarda, "AtualizarSono", 0.01f);
            Call(guarda, "AtualizarSono", 1f);
            Check(guarda.EstaDormindo && itens[0].TentarColetar(inventario), "Banana protegida pode ser pega durante sono");
            Check(bananas[0].transform.parent == mao && !bananas[0].GetComponent<Collider>().enabled && !inventario.PodeColetarPocao,
                "Banana visivel na mao sem colisoes; bloqueia outros objetos na mesma mao");
            Check(!inventario.EntregarBanana(caldeirao), "Mesmo E nao pega e deposita na mesma atualizacao");
            inventario.DevolverBanana();
            Check(!inventario.TemBanana && bananas[0].GetComponent<Collider>().enabled && !bananas[0].NaMao,
                "Devolucao restaura objeto, colisoes e disponibilidade");
            Set(inventario, "frameColeta", -1);
            Time.timeScale = 0;
            Check(!itens[0].TentarColetar(inventario), "Coleta bloqueada durante pausa");
            Time.timeScale = 1;
            var monolito = new GameObject("Monolito teste").AddComponent<MonolitoDesmoronavel>();
            monolito.banana = bananas[1].GetComponent<Rigidbody>();
            bananas[1].monolito = monolito;
            bananas[1].transform.position = origem + Vector3.forward + Vector3.up;
            bananas[0].transform.position = origem + Vector3.left * 5;
            Physics.SyncTransforms();
            Check(!itens[1].TentarColetar(inventario), "Banana do monolito bloqueada antes da conclusao");
            Set(monolito, "<Concluido>k__BackingField", true);
            Check(itens[1].TentarColetar(inventario), "Banana caida do monolito pode ser transportada");
            movimento.ReceberArremesso(Vector3.back, 5, 2, 1, 8);
            Call(inventario, "AtualizarBananaCarregada");
            Check(!inventario.TemBanana && !bananas[1].NaMao, "Pancada devolve banana ao local da coleta sem perde-la");
            Call(movimento, "CancelKnockback");
            for (int i = 0; i < 3; i++)
            {
                Set(inventario, "frameColeta", -1);
                foreach (var b in bananas) if (!b.Depositada) b.transform.position = origem + Vector3.left * 5;
                bananas[i].transform.position = origem + Vector3.forward + Vector3.up;
                Physics.SyncTransforms();
                Check(itens[i].TentarColetar(inventario), "Coleta banana " + (i + 1));
                Check(!itens[(i + 1) % 3].TentarColetar(inventario), "Nao carrega segunda banana");
                Set(inventario, "frameColeta", -1);
                panela.transform.position = origem + Vector3.right * 10;
                Check(!inventario.EntregarBanana(caldeirao), "Deposito distante rejeitado");
                panela.transform.position = origem + Vector3.right;
                Check(inventario.EntregarBanana(caldeirao) && caldeirao.Quantidade == i + 1 && !inventario.TemBanana,
                    "Entrega unica incrementa receita e libera mao");
                Check(!inventario.EntregarBanana(caldeirao), "Nao conta entrega repetida");
                if (i < 2) Check(!guarda.DocilPelaSopa && bloqueio.enabled, "Antes da terceira banana gorila continua guardando passagem");
            }
            Check(caldeirao.SopaPronta && guarda.DocilPelaSopa && !guarda.guardandoCaverna && !guarda.EstaAtacando,
                "Terceira entrega prepara sopa e cancela guarda/ataque");
            Check(!gorila.AtrairPelaSopa(caldeirao), "Atracao nao reinicia ao ser chamada novamente");
            guarda.DefinirGuarda(true);
            Check(!guarda.guardandoCaverna, "Gorila permanece docil mesmo se alguem tentar reativar guarda");
            var antes = gorila.transform.position;
            Call(gorila, "Avancar", 0f);
            Check(gorila.transform.position == antes, "Pausa congela deslocamento");
            Call(gorila, "Avancar", 2f);
            anim.Update(0.3f);
            Check(anim.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Walk"), "Caminhada usa animacao Walk");
            for (int i = 0; i < 60; i++) { Call(gorila, "Avancar", 0.1f); anim.Update(0.1f); }
            Check(gorila.PassagemLiberada && !bloqueio.enabled && (gorila.transform.position - destino.position).sqrMagnitude < 0.001f,
                "Gorila chega ao ponto de comer e libera bloqueio");
            Check(anim.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Eat"), "Gorila permanece comendo com animacao Eat");
            Check(vida.VidaAtual == 5, "Fluxo da sopa preserva vida do jogador");
            sucesso = true;
        }
        catch (Exception e) { report += "FAIL: " + e + "\n"; Debug.LogException(e); }
        finally
        {
            EditorSceneManager.CloseScene(temporaria, true);
            SceneManager.SetActiveScene(original);
            Time.timeScale = escalaTempo;
            UnityEngine.Random.state = random;
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/sopa-gorila-validation.txt", report);
        }
        if (sucesso) Debug.Log("Coleta, transporte, receita e gorila docil aprovados. Logs/sopa-gorila-validation.txt");
        if (Application.isBatchMode) EditorApplication.Exit(sucesso ? 0 : 1);
    }
}
