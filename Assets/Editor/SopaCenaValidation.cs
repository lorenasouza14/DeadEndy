using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using StarterAssets;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Executa sobre as instancias reais, com E no Input System e fisica/animacao em Play Mode.
// Teleportes so encurtam deslocamentos entre desafios; nao alteram a cena salva.
[InitializeOnLoad]
public static class SopaCenaValidation
{
    const string Chave = "SopaCenaValidation.Ativa";
    const string Relatorio = "Logs/sopa-fase-play.txt";
    static Stack<IEnumerator> rotinas;
    static float aguardar;
    static int ultimoFrame;
    static InventarioEndy inv;
    static ThirdPersonController controle;
    static CharacterController capsula;
    static CaldeiraoSopa sopa;
    static GorilaGuardiao guarda;
    static PuzzleAltaresRuinas puzzle;
    static BananaSopa traps, ruinas, santuario;
    static StarterAssetsInputs entradas;
    static PlayerInput playerInput;
    static float vida;
    static bool inicializado;

    static SopaCenaValidation() { EditorApplication.update += Atualizar; }
    [MenuItem("Tools/Gorila/Sopa/Testar fluxo na fase real %#&l")]
    public static void Iniciar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.WriteAllText(Relatorio, "Fluxo real da sopa: entrada E, fisica e animacoes.\n");
        SessionState.SetBool(Chave, true); inicializado = false; EditorApplication.isPlaying = true;
    }
    static void Log(string s) { File.AppendAllText(Relatorio, $"{Time.time:F2}: {s}\n"); }
    static void Exigir(bool ok, string s) { if (!ok) throw new InvalidOperationException(s); }
    static void Atualizar()
    {
        if (!SessionState.GetBool(Chave, false) || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
        Application.runInBackground = true;
        if (Time.time < 2) return;
        try
        {
            if (!inicializado) { rotinas = new Stack<IEnumerator>(); rotinas.Push(Executar()); inicializado = true; aguardar = 0; ultimoFrame = -1; }
            if (Time.time < aguardar || ultimoFrame == Time.frameCount) return;
            ultimoFrame = Time.frameCount;
            while (rotinas.Count > 0)
            {
                var r = rotinas.Peek();
                if (!r.MoveNext()) { rotinas.Pop(); continue; }
                if (r.Current is IEnumerator filho) { rotinas.Push(filho); continue; }
                if (r.Current is float segundos) aguardar = Time.time + segundos;
                return;
            }
            Encerrar("PASS: fluxo completo na fase real.");
        }
        catch (Exception e) { Encerrar("FAIL: " + e); Debug.LogException(e); }
    }
    static void Encerrar(string s)
    {
        Log(s); SessionState.SetBool(Chave, false);
        if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        if (entradas != null) entradas.move = Vector2.zero;
        Time.timeScale = 1; EditorApplication.isPlaying = false;
    }
    static IEnumerator Tecla(params Key[] teclas)
    {
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(teclas));
        int f = Time.frameCount;
        while (Time.frameCount < f + 3) yield return null;
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        yield return 0.3f;
    }
    static void Mover(Vector3 p, Vector3 olhar, bool solo = true)
    {
        entradas.move = Vector2.zero; entradas.sprint = false; entradas.jump = false;
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        if (solo) p = SopaCenaSetup.Solo(p, inv.transform) + Vector3.up * 0.35f;
        capsula.enabled = false; inv.transform.position = p;
        Vector3 dir = Vector3.ProjectOnPlane(olhar - p, Vector3.up); if (dir.sqrMagnitude > 0.001f) inv.transform.rotation = Quaternion.LookRotation(dir);
        capsula.enabled = true;
        typeof(ThirdPersonController).GetField("_verticalVelocity", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controle, 0f);
        Physics.SyncTransforms();
    }
    static void Perto(BananaSopa banana)
    {
        var centro = banana.Centro;
        // Procura um ponto de pe junto ao altar, com passagem para a capsula e linha de coleta.
        foreach (float raio in new[] {1.25f, 1.5f, 1f, 0.8f})
        for (int i = 0; i < 16; i++)
        {
            Vector3 p = centro + Quaternion.Euler(0,i*22.5f,0) * Vector3.back * raio;
            Vector3 chao;
            try { chao = SopaCenaSetup.Solo(p, inv.transform); } catch { continue; }
            if (chao.y > centro.y - 0.1f || chao.y < centro.y - 2.2f) continue;
            var colisoes = Physics.OverlapCapsule(chao + Vector3.up * 0.4f, chao + Vector3.up * 1.55f, 0.26f, ~0, QueryTriggerInteraction.Ignore);
            if (colisoes.Any(c => !c.transform.IsChildOf(inv.transform) && !c.transform.IsChildOf(banana.transform))) continue;
            Mover(chao + Vector3.up * 0.35f, centro, false);
            if (InteracaoSopa.AoAlcance(inv, centro, banana.alcanceColeta, banana.transform)) { Log("Posicao de coleta " + banana.name + ": " + inv.transform.position); return; }
        }
        throw new InvalidOperationException("Nao encontrou acesso a fruta " + SopaCenaSetup.Caminho(banana.transform));
    }
    static IEnumerator Depositar(int quantidade)
    {
        Mover(new Vector3(17.87f,7,112), sopa.PontoInteracao);
        yield return 0.8f;
        Exigir(sopa.PodeDepositar(inv), "Deposito inacessivel. Pos=" + inv.transform.position + " alvo=" + sopa.PontoInteracao);
        Exigir(sopa.GetComponent<DicaInteracao>().Visivel, "Dica do caldeirao ausente.");
        yield return Tecla(Key.E);
        Exigir(sopa.Quantidade == quantidade && !inv.TemBanana, "Contador/mao incorretos apos entrega " + quantidade);
        yield return Tecla(Key.E);
        Exigir(sopa.Quantidade == quantidade, "Entrega duplicada.");
        Exigir(sopa.ObterDica().Contains(quantidade + "/3"), "Progresso incorreto no aviso.");
        Log("PASS: entrega E " + quantidade + "/3, mao livre, aviso e repeticao sem duplicacao.");
    }
    static IEnumerator Executar()
    {
        Application.runInBackground = true;
        inv = UnityEngine.Object.FindAnyObjectByType<InventarioEndy>(); controle = inv.GetComponent<ThirdPersonController>(); capsula = inv.GetComponent<CharacterController>();
        entradas = inv.GetComponent<StarterAssetsInputs>(); playerInput = inv.GetComponent<PlayerInput>();
        sopa = UnityEngine.Object.FindAnyObjectByType<CaldeiraoSopa>(); guarda = sopa.gorila.GetComponent<GorilaGuardiao>(); puzzle = UnityEngine.Object.FindAnyObjectByType<PuzzleAltaresRuinas>();
        traps = sopa.bananasDaReceita.Single(b => b.monolito == null && b.gorilaQueProtege == null);
        ruinas = sopa.bananasDaReceita.Single(b => b.monolito != null); santuario = sopa.bananasDaReceita.Single(b => b.gorilaQueProtege != null);
        vida = inv.GetComponent<VidaEndy>().VidaAtual;
        Exigir(sopa.ReceitaValida() && sopa.Quantidade == 0 && !sopa.vaporSopa.isPlaying, "Configuracao inicial invalida.");
        Exigir(guarda.usarCicloSono && guarda.causarDano && controle.permitirCaminhadaLenta, "Sono/furtividade/dano devem estar ativos.");
        Exigir(sopa.bananasDaReceita.All(b => b.GetComponentsInChildren<ItemColetavel>(true).Length == 1 && b.GetComponentsInChildren<DicaInteracao>(true).Length == 1), "Sensores duplicados.");
        Exigir(!ruinas.DesafioLiberado && !santuario.DesafioLiberado, "Desafios devem iniciar bloqueados.");
        Perto(santuario); yield return Tecla(Key.E); Exigir(!inv.TemBanana, "Coletou santuario acordado.");
        Mover(ruinas.Centro + Vector3.back, ruinas.Centro, false); yield return Tecla(Key.E); Exigir(!inv.TemBanana, "Coletou monolito intacto.");
        Log("PASS: E rejeitado no santuario acordado e monolito intacto.");
        while(controle.EstaArremessado) yield return null;
        Perto(traps); yield return 0.8f;
        Log("DIAG coleta traps: pos="+inv.transform.position+" pode="+traps.PodeColetar(inv)+" interage="+inv.PodeInteragirComSopa+" livre="+inv.PodeColetarPocao+" mao="+inv.encaixeMao+" arremesso="+controle.EstaArremessado+" dica="+traps.GetComponentInChildren<ItemColetavel>().ObterDica()+" sensor="+traps.GetComponentInChildren<SphereCollider>().bounds);
        yield return Tecla(Key.E);
        Exigir(inv.BananaNaMao == traps && traps.transform.parent == inv.encaixeMao, "E nao colocou banana das traps na mao.");
        Exigir(traps.GetComponentsInChildren<Collider>().All(c => !c.enabled), "Colisao da fruta na mao.");
        Exigir(!inv.PegarBanana(ruinas) && !inv.PodeColetarPocao, "Mao permite dois itens.");
        Log("PASS: coleta E, encaixe de mao e exclusividade. Tamanho=" + traps.GetComponent<Renderer>().bounds.size);
        ScreenCapture.CaptureScreenshot("Logs/sopa-banana-na-mao.png"); yield return 2f;
        Mover(new Vector3(15.5f,8,130.3f), guarda.transform.position);
        float limite = Time.time + 55;
        vida = inv.GetComponent<VidaEndy>().VidaAtual;
        while (!controle.EstaArremessado && Time.time < limite) { vida = inv.GetComponent<VidaEndy>().VidaAtual; yield return null; }
        Exigir(controle.EstaArremessado, "Gorila nao arremessou Endy proximo."); yield return 0.2f;
        Exigir(inv.BananaNaMao == traps && traps.NaMao && traps.gameObject.activeInHierarchy &&
            traps.transform.parent == inv.encaixeMao, "Pancada removeu banana do totem da mao.");
        Exigir(!inv.EntregarBanana(sopa) && sopa.Quantidade == 0, "Entrega permitida durante arremesso.");
        Exigir(Mathf.Approximately(inv.GetComponent<VidaEndy>().VidaAtual, vida - inv.GetComponent<VidaEndy>().vidaMaxima / 3f), "Pancada deve tirar um terco da vida maxima.");
        Log("PASS: pancada real preserva banana na mao e tira um terco da vida maxima.");
        while(controle.EstaArremessado) yield return null;
        yield return Depositar(1);
        var solucao = new List<int>(); int melhor = 100;
        for(int a=0;a<4;a++) for(int b=0;b<4;b++) for(int c=0;c<4;c++)
        if ((puzzle.altares[0].Passos+a+c)%4==0 && (puzzle.altares[1].Passos+a+b)%4==0 && (puzzle.altares[2].Passos+b+c)%4==0 && a+b+c<melhor)
        { melhor=a+b+c; solucao=Enumerable.Repeat(0,a).Concat(Enumerable.Repeat(1,b)).Concat(Enumerable.Repeat(2,c)).ToList(); }
        Exigir(melhor<100,"Puzzle sem solucao.");
        foreach(int indice in solucao)
        {
            var altar = puzzle.altares[indice]; Vector3 fora = Vector3.ProjectOnPlane(altar.alavanca.position-altar.pivo.position,Vector3.up).normalized;
            Mover(altar.alavanca.position+fora, altar.pivo.position); yield return 0.5f;
            Exigir(puzzle.AltarProximo==altar,"Alavanca inacessivel."); yield return Tecla(Key.E);
            while(puzzle.Ocupado) yield return null;
        }
        Exigir(puzzle.Resolvido && !ruinas.DesafioLiberado,"Ruinas devem bloquear enquanto desmoronam.");
        yield return 6f;
        Exigir(ruinas.DesafioLiberado && ruinas.Centro.y<7,"Banana nao caiu/repousou apos puzzle.");
        Perto(ruinas); yield return 0.8f; yield return Tecla(Key.E); Exigir(inv.BananaNaMao==ruinas,"Nao coletou banana caida.");
        Log("PASS: puzzle por E, bloqueio no desmoronamento e coleta da fruta no chao."); yield return Depositar(2);
        Mover(new Vector3(20,8,131),santuario.Centro); yield return 0.5f;
        limite=Time.time+55; while(!guarda.EstaDormindo && Time.time<limite) yield return null;
        Exigir(guarda.EstaDormindo,"Gorila nao entrou no sono natural.");
        Perto(santuario); yield return 0.4f;
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.LeftCtrl, Key.D)); yield return 0.65f;
        Exigir(controle.CaminhadaLentaSolicitada && controle.VelocidadeHorizontalAtual<=1.15f && guarda.EstaDormindo,"Ctrl nao manteve furtividade.");
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState()); yield return 0.2f;
        Log("PASS: sono natural e caminhada Ctrl silenciosa.");
        Perto(santuario); yield return 0.5f; yield return Tecla(Key.E); Exigir(inv.BananaNaMao==santuario,"Nao coletou durante sono.");
        // Movimento rapido proximo deve acordar antes do golpe.
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.W,Key.LeftShift));
        limite=Time.time+2; while(guarda.EstaDormindo && Time.time<limite) yield return null;
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
        Exigir(guarda.EstadoSono==GorilaGuardiao.EstadoSonoGorila.Acordando && !guarda.EstaAtacando,"Ruido nao acordou com atraso.");
        float acordou=Time.time; yield return 0.6f; Exigir(!guarda.EstaAtacando,"Ataque antecipado no despertar.");
        Log("PASS: movimento rapido acorda, com transicao antes de atacar.");
        Mover(new Vector3(15.5f,8,130.3f), guarda.transform.position); limite=Time.time+10;
        vida = inv.GetComponent<VidaEndy>().VidaAtual;
        while(!controle.EstaArremessado && Time.time<limite) { vida = inv.GetComponent<VidaEndy>().VidaAtual; yield return null; }
        Exigir(controle.EstaArremessado && Time.time-acordou>=guarda.tempoAcordar,"Golpe apos despertar ausente."); yield return 0.2f;
        Exigir(inv.BananaNaMao == santuario && santuario.NaMao && santuario.gameObject.activeInHierarchy &&
            santuario.transform.parent == inv.encaixeMao && Mathf.Approximately(inv.GetComponent<VidaEndy>().VidaAtual, vida - inv.GetComponent<VidaEndy>().vidaMaxima / 3f),
            "Golpe removeu banana do santuario ou aplicou dano incorreto.");
        while(controle.EstaArremessado) yield return null;
        yield return Depositar(3);
        Exigir(sopa.SopaPronta && sopa.vaporSopa.isPlaying && guarda.DocilPelaSopa,"Sopa pronta nao ativou efeitos/docilidade.");
        limite=Time.time+40; float maiorErroChao=0;
        while(!sopa.gorila.PassagemLiberada && Time.time<limite)
        {
            Exigir(!guarda.EstaAtacando,"Ataque durante ida a sopa.");
            if(sopa.gorila.Etapa==GorilaSopa.EtapaSopa.Caminhando)
            { float y=SopaCenaSetup.Solo(guarda.transform.position).y; maiorErroChao=Mathf.Max(maiorErroChao,Mathf.Abs(guarda.transform.position.y-y)); }
            yield return null;
        }
        Exigir(sopa.gorila.PassagemLiberada && maiorErroChao<0.35f,"Percurso sem chegada/fora do chao: "+maiorErroChao);
        yield return 2f;
        Exigir(guarda.animGorila.GetCurrentAnimatorStateInfo(0).IsName(sopa.gorila.estadoComer),"Animacao Eat ausente.");
        Exigir(sopa.gorila.bloqueiosDaCaverna.All(c=>!c.enabled),"Barreira ainda ativa.");
        Log("PASS: terceira entrega, vapor, Walk ate a sopa, Eat e bloqueios liberados. Erro maximo de chao="+maiorErroChao);
        Mover(new Vector3(18,7,111), sopa.gorila.transform.position); yield return 2f;
        ScreenCapture.CaptureScreenshot("Logs/sopa-gorila-comendo.png");
        Mover(new Vector3(15,8,132), new Vector3(14,8.5f,139)); yield return 0.4f;
        Vector3 inicio = inv.transform.position; limite=Time.time+9;
        while(inv.transform.position.z<139 && Time.time<limite)
        {
            Vector3 direcao=Vector3.ProjectOnPlane(new Vector3(14.4f,8.5f,139.2f)-inv.transform.position,Vector3.up).normalized;
            Vector3 local=Quaternion.Inverse(Quaternion.Euler(0,Camera.main.transform.eulerAngles.y,0))*direcao;
            entradas.move=new Vector2(local.x,local.z); yield return null;
        }
        entradas.move=Vector2.zero;
        Exigir(inv.transform.position.z>138.8f && inv.transform.position.y>7.5f,"Endy nao atravessou entrada: "+inv.transform.position);
        Exigir(!guarda.EstaAtacando && guarda.DocilPelaSopa && !controle.EstaArremessado,"Gorila voltou a atacar.");
        Log("PASS: Endy caminhou pela entrada com CharacterController, de "+inicio+" ate "+inv.transform.position);
        yield return 1f;
    }
}
