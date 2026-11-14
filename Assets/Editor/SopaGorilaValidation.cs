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
            var legado = RecarregarArquivoDeTeste("{\"versao\":1,\"jogo\":\"DeadEndy\",\"cena\":\"" +
                ProgressoJogo.CenaJogo + "\",\"salvoEmUtc\":\"2026-11-13T00:00:00Z\",\"vida\":5,\"alavancas\":[],\"baus\":[]}");
            Check(legado.Valido() && legado.gorila == null, "JSON de save antigo sem gorila continua carregavel no Unity");
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
            Call(caldeirao, "Awake");
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
                Call(bananas[i], "Awake");
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
            // Avanca a transicao e o clipe em passos como os frames do jogo.
            for (int i = 0; i < 100 && !guarda.EstaDormindo; i++)
            {
                anim.Update(0.1f);
                Call(guarda, "AtualizarSono", 0.1f);
            }
            Check(guarda.EstaDormindo, "Gorila termina animacao de deitar antes da coleta");
            Check(itens[0].TentarColetar(inventario), "Banana protegida pode ser pega durante sono");
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
            Check(inventario.BananaNaMao == bananas[1] && bananas[1].NaMao && bananas[1].transform.parent == mao,
                "Pancada preserva banana do monolito na mao");
            Set(inventario, "frameColeta", -1);
            Check(!inventario.EntregarBanana(caldeirao) && caldeirao.Quantidade == 0,
                "Arremesso bloqueia entrega sem consumir banana");
            Call(movimento, "CancelKnockback");
            inventario.DevolverBanana(); // Limpa a preparacao para testar as tres entregas abaixo.
            for (int i = 0; i < 3; i++)
            {
                Set(inventario, "frameColeta", -1);
                foreach (var b in bananas) if (!b.Depositada) b.transform.position = origem + Vector3.left * 5;
                bananas[i].transform.position = origem + Vector3.forward + Vector3.up;
                Physics.SyncTransforms();
                Check(itens[i].TentarColetar(inventario), "Coleta banana " + (i + 1));
                VerificarRetomada(caldeirao, inventario.BananaNaMao.idSave, temporaria, origem + Vector3.right * 100);
                Check(!itens[(i + 1) % 3].TentarColetar(inventario), "Nao carrega segunda banana");
                Set(inventario, "frameColeta", -1);
                for (int golpe = 0; golpe < 2; golpe++)
                {
                    movimento.ReceberArremesso(Vector3.back, 5, 2, 1, 8);
                    Call(inventario, "AtualizarBananaCarregada");
                    Check(inventario.BananaNaMao == bananas[i] && bananas[i].NaMao && !bananas[i].Depositada &&
                        bananas[i].gameObject.activeInHierarchy && bananas[i].transform.parent == mao &&
                        !bananas[i].GetComponent<Collider>().enabled,
                        "Golpe repetido preserva banana " + (i + 1) + " visivel e sem colisao na mao");
                    Check(!inventario.EntregarBanana(caldeirao) && caldeirao.Quantidade == i,
                        "Golpe nao deposita nem altera progresso da sopa");
                    Call(movimento, "CancelKnockback");
                }
                panela.transform.position = origem + Vector3.right * 10;
                Check(!inventario.EntregarBanana(caldeirao), "Deposito distante rejeitado");
                panela.transform.position = origem + Vector3.right;
                Check(inventario.EntregarBanana(caldeirao) && caldeirao.Quantidade == i + 1 && !inventario.TemBanana,
                    "Entrega apos golpes, sem nova coleta, incrementa receita uma vez e libera mao");
                VerificarRetomada(caldeirao, null, temporaria, origem + Vector3.right * 100);
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
            VerificarRetomada(caldeirao, null, temporaria, origem + Vector3.right * 100);
            VerificarRuinas(origem + Vector3.left * 100);
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

    private static void VerificarRetomada(CaldeiraoSopa original, string bananaNaMao, Scene cena, Vector3 origem)
    {
        var dados = new DadosGorila { bananaNaMao = bananaNaMao, ruinas = new DadosRuinas[0],
            sopas = new[] { original.CapturarProgresso() } };
        dados = RecarregarArquivoDeTeste(JsonUtility.ToJson(new DadosJogo {
            versao = 2, jogo = "DeadEndy", cena = ProgressoJogo.CenaIntegrada,
            salvoEmUtc = DateTime.UtcNow.ToString("O"), vida = 5,
            alavancas = new string[0], baus = new string[0], gorila = dados })).gorila;
        Check(dados.Valido(), "Progresso da sopa e banana na mao sobrevivem a serializacao");
        var raiz = new GameObject("Retomada independente");
        var panela = new GameObject("Caldeirao retomado");
        panela.transform.SetParent(raiz.transform);
        panela.transform.position = origem + Vector3.right;
        var sopa = panela.AddComponent<CaldeiraoSopa>();
        sopa.idSave = original.idSave;
        var endy = new GameObject("Endy retomado");
        endy.transform.SetParent(raiz.transform);
        endy.transform.position = origem;
        var inventario = endy.AddComponent<InventarioEndy>();
        inventario.encaixeMao = new GameObject("Mao").transform;
        inventario.encaixeMao.SetParent(endy.transform, false);
        var gorilaObj = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Suriyun/Cute Zoo 2/Prefab/GorillaA.prefab"), cena);
        gorilaObj.transform.SetParent(raiz.transform);
        gorilaObj.transform.position = origem + Vector3.forward * 10;
        var guarda = gorilaObj.AddComponent<GorilaGuardiao>();
        var anim = gorilaObj.GetComponentInChildren<Animator>();
        anim.Rebind(); anim.Update(0);
        Call(guarda, "Start");
        var gorila = gorilaObj.AddComponent<GorilaSopa>();
        var destino = new GameObject("Destino retomado").transform;
        destino.SetParent(raiz.transform);
        destino.position = origem + Vector3.forward * 15;
        gorila.percurso = new[] { destino };
        gorila.olharParaSopa = panela.transform;
        var bloqueio = new GameObject("Bloqueio retomado").AddComponent<BoxCollider>();
        bloqueio.transform.SetParent(raiz.transform);
        bloqueio.transform.position = origem + Vector3.forward * 20;
        gorila.bloqueiosDaCaverna = new Collider[] { bloqueio };
        sopa.gorila = gorila;
        sopa.jogador = inventario;
        for (int i = 0; i < 3; i++)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.transform.SetParent(raiz.transform);
            obj.transform.position = origem + Vector3.left * (5 + i);
            obj.AddComponent<Rigidbody>().isKinematic = true;
            var banana = obj.AddComponent<BananaSopa>();
            banana.idSave = original.bananasDaReceita[i].idSave;
            sopa.bananasDaReceita[i] = banana;
        }
        Call(sopa, "Start");
        sopa.RestaurarProgresso(dados.sopas[0]);
        Check(sopa.Quantidade == original.Quantidade, "Retomada recupera contador " + original.Quantidade + "/3");
        foreach (var banana in sopa.bananasDaReceita)
        {
            bool entregue = Array.IndexOf(dados.sopas[0].bananas, banana.idSave) >= 0;
            Check(banana.Depositada == entregue && banana.gameObject.activeSelf != entregue,
                "Retomada remove somente a banana ja entregue");
            if (banana.idSave == bananaNaMao)
            {
                Check(inventario.RestaurarBananaNaMao(banana) && banana.NaMao &&
                    banana.transform.parent == inventario.encaixeMao && banana.idSave == bananaNaMao,
                    "Banana coletada retorna a mao com identificador original");
                Check(!inventario.RestaurarBananaNaMao(banana), "Restauracao nao duplica banana na mao");
                Set(inventario, "frameColeta", -1);
                Physics.SyncTransforms();
                Check(inventario.EntregarBanana(sopa) && sopa.Quantidade == original.Quantidade + 1,
                    "Banana recuperada pode ser entregue normalmente");
            }
        }
        if (dados.sopas[0].passagemLiberada)
            Check(gorila.PassagemLiberada && guarda.DocilPelaSopa && !bloqueio.enabled &&
                gorila.transform.position == destino.position, "Retomada preserva gorila comendo e passagem aberta");
        else if (original.SopaPronta)
        {
            Check(guarda.DocilPelaSopa && !gorila.PassagemLiberada, "Sair durante caminhada conserva sopa pronta");
            Call(gorila, "Avancar", 100f);
            Check(gorila.PassagemLiberada && !bloqueio.enabled, "Caminhada retomada termina e libera passagem");
        }
        Object.DestroyImmediate(raiz);
    }

    private static void VerificarRuinas(Vector3 origem)
    {
        var raiz = new GameObject("Ruinas retomadas");
        raiz.transform.position = origem;
        var mono = raiz.AddComponent<MonolitoDesmoronavel>();
        mono.corpoInteiro = GameObject.CreatePrimitive(PrimitiveType.Cube).GetComponent<Collider>();
        mono.corpoInteiro.transform.SetParent(raiz.transform, false);
        mono.fragmentos = new GameObject("Fragmentos");
        mono.fragmentos.transform.SetParent(raiz.transform, false);
        mono.banana = new GameObject("Banana ruinas").AddComponent<Rigidbody>();
        mono.banana.transform.SetParent(raiz.transform, false);
        mono.banana.isKinematic = true;
        Call(mono, "Awake");
        var puzzle = raiz.AddComponent<PuzzleAltaresRuinas>();
        puzzle.monolito = mono;
        puzzle.altares = new AltarRuinas[3];
        for (int i = 0; i < 3; i++)
        {
            var altar = new GameObject("Altar " + i).AddComponent<AltarRuinas>();
            altar.transform.SetParent(raiz.transform, false);
            altar.pivo = altar.alavanca = altar.emissor = altar.transform;
            altar.caboAlavanca = new GameObject("Cabo").transform;
            altar.caboAlavanca.SetParent(altar.transform, false);
            altar.feixe = altar.gameObject.AddComponent<LineRenderer>();
            puzzle.altares[i] = altar;
        }
        Call(puzzle, "Awake");
        Call(puzzle, "Start");
        var dados = new DadosRuinas { id = puzzle.idSave, passos = new[] { 1, 2, 3 } };
        puzzle.RestaurarProgresso(JsonUtility.FromJson<DadosRuinas>(JsonUtility.ToJson(dados)));
        Check(puzzle.altares[0].Passos == 1 && puzzle.altares[1].Passos == 2 && puzzle.altares[2].Passos == 3 &&
            !puzzle.Resolvido && mono.corpoInteiro.gameObject.activeSelf, "Retomada preserva giros parciais das tres ruinas");
        foreach (var altar in puzzle.altares)
            Check(Quaternion.Angle(altar.pivo.localRotation, altar.RotacaoDestino) < 0.01f,
                "Retomada aplica orientacao visual do altar");
        dados.resolvido = true;
        puzzle.RestaurarProgresso(dados);
        Check(puzzle.Resolvido && mono.Concluido && !mono.corpoInteiro.gameObject.activeSelf &&
            !mono.fragmentos.activeSelf && !mono.banana.isKinematic && !mono.Desmoronar(),
            "Ruinas resolvidas retornam sem monolito nem repeticao do desafio");
        Object.DestroyImmediate(raiz);
    }

    [Serializable] private class RegistroTeste { public string dados, sha256; }

    private static DadosJogo RecarregarArquivoDeTeste(string json)
    {
        string hash;
        using (var sha = System.Security.Cryptography.SHA256.Create())
            hash = Convert.ToBase64String(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json)));
        string registro = JsonUtility.ToJson(new RegistroTeste { dados = json, sha256 = hash });
        var arquivo = new ArquivoSave(Path.Combine("Logs", "autosave-gorila-tests", Guid.NewGuid().ToString("N") + ".json"));
        Check(arquivo.Gravar(registro, t => ProgressoJogo.Decodificar(t, out _), true, out _),
            "Checkpoint do gorila gravado em arquivo isolado");
        if (!arquivo.Ler(t => ProgressoJogo.Decodificar(t, out _), out var texto, out _, out _) ||
            !ProgressoJogo.Decodificar(texto, out var dados)) throw new Exception("Falha ao recarregar checkpoint de teste");
        return dados;
    }
}
