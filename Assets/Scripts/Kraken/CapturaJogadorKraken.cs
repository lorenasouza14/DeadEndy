using UnityEngine;
using StarterAssets;

/// <summary>Captura, tres ciclos completos da lanterna e devolucao segura ao piso.</summary>
[DefaultExecutionOrder(150), DisallowMultipleComponent]
public class CapturaJogadorKraken : MonoBehaviour
{
    public KrakenNadando nado;
    public AtaqueTentaculoRigKraken tentaculo;
    public BatalhaKraken batalha;
    public Transform olho, retornoNasPedras;
    public Transform centroDoRosto;
    public Vector3 apoioDoTentaculo = new Vector3(0,-1.4f,0);
    public SkinnedMeshRenderer malhaDosOlhos;
    public PalpebrasKraken palpebras;
    [Min(.1f)] public float tempoLimite=6f;
    public float TempoRestante => Estado==Fase.Preso?Mathf.Max(0,prazo-Time.time):0;
    public int Cliques => Piscadas*2+(acendeuNoOlho?1:0);
    public bool Reagindo => tempoDeCoceira>0;
    float prazo, tempoDeCoceira;
    public string nomeDoPiscar = "PiscarOlhos";
    [Min(1)] public float distanciaDoRosto = 5f;
    [Min(.2f)] public float tempoParaPegar = 1.8f, tempoParaSoltar = 1.5f;
    public enum Fase { Livre, Levando, Preso, Soltando }
    public Fase Estado { get; private set; }
    public bool Ativa => Estado != Fase.Livre;
    public int Piscadas { get; private set; }
    [SerializeField] Font fonteInterface;
    public string Mensagem => Estado==Fase.Levando ? "O Kraken te pegou! Prepare a lanterna!" : Estado==Fase.Soltando ? "A luz incomodou o Kraken! Voltando para a entrada..." : "RAPIDO! CLIQUE ESQUERDO: acenda e apague a lanterna!\n"+Cliques+" / 6 cliques   |   "+TempoRestante.ToString("0.0")+" s";
    ThirdPersonController jogador;
    VidaJogadorKraken vida;
    CharacterController capsula;
    LanternaJogador lanterna;
    Animator animador;
    Camera cameraOriginal, cameraDaCaptura;
    bool controleAnterior, capsulaAnterior, animadorAnterior, cameraAnterior, luzAnterior;
    Vector3 inicio, destinoPedras, centroLocal, posicaoPresa;
    Quaternion rotacaoInicial, rotacaoRetorno;
    float tempo, tempoDoPiscar;
    bool acendeuNoOlho;
    int indiceDoPiscar=-1;

    public bool PodeCapturar(VidaJogadorKraken alvo)
    {
        return PodeCapturar(alvo, out _, out _);
    }
    bool PodeCapturar(VidaJogadorKraken alvo, out ThirdPersonController controle, out LanternaJogador luz)
    {
        controle = null; luz = null;
        if (!LocalizarEntradaDaFase()) return false;
        if(!isActiveAndEnabled || Ativa || Reagindo || !alvo || alvo.Morto || !olho || !retornoNasPedras || !tentaculo)return false;
        if(batalha && (batalha.EmIntroducao || (batalha.inundacao && batalha.inundacao.EmExecucao)
            || (batalha.sabao && (batalha.sabao.EmExecucao || batalha.sabao.BolhasAtivas > 0))))return false;
        controle=alvo.GetComponent<ThirdPersonController>();
        luz=alvo.GetComponentInChildren<LanternaJogador>(true);
        if(!luz && LanternaJogador.Instancia && LanternaJogador.Instancia.foiColetada)luz=LanternaJogador.Instancia;
        if(!controle || !luz || !luz.foiColetada)return false;
        return true;
    }
    public bool TentarCapturar(VidaJogadorKraken alvo)
    {
        if (!PodeCapturar(alvo, out var controle, out var luz)) return false;
        jogador=controle;vida=alvo;capsula=alvo.GetComponent<CharacterController>();lanterna=luz;
        inicio=jogador.transform.position;rotacaoInicial=jogador.transform.rotation;
        destinoPedras=retornoNasPedras.position;
        rotacaoRetorno=Quaternion.Euler(0,retornoNasPedras.eulerAngles.y,0);
        controleAnterior=jogador.enabled;capsulaAnterior=capsula && capsula.enabled;
        centroLocal=apoioDoTentaculo;
        jogador.enabled=false;if(capsula)capsula.enabled=false;
        animador=jogador.GetComponent<Animator>();animadorAnterior=animador && animador.enabled;
        if(animador){animador.SetFloat("Speed",0);animador.SetBool("Jump",false);animador.SetBool("FreeFall",false);animador.enabled=false;}
        luzAnterior=lanterna.Ligada;lanterna.AlvoDeMiraTemporario=centroDoRosto?centroDoRosto:olho;
        lanterna.EstadoAlterado+=AoAlternarLanterna;
        // O nado pausa a pose do corpo ate terminar a captura e a coceira.
        cameraOriginal=Camera.main;
        if(cameraOriginal){cameraAnterior=cameraOriginal.enabled;var go=new GameObject("Camera da captura do Kraken");cameraDaCaptura=go.AddComponent<Camera>();cameraDaCaptura.CopyFrom(cameraOriginal);cameraDaCaptura.fieldOfView=68;cameraDaCaptura.nearClipPlane=.1f;cameraDaCaptura.depth=cameraOriginal.depth+1;cameraOriginal.enabled=false;}
        indiceDoPiscar=malhaDosOlhos && malhaDosOlhos.sharedMesh?malhaDosOlhos.sharedMesh.GetBlendShapeIndex(nomeDoPiscar):-1;
        Piscadas=0;tempo=0;acendeuNoOlho=false;Estado=Fase.Levando;
        return true;
    }
    bool LocalizarEntradaDaFase()
    {
        if (retornoNasPedras && retornoNasPedras.name == "PontoEntrada" &&
            retornoNasPedras.gameObject.scene == gameObject.scene && retornoNasPedras.gameObject.activeInHierarchy) return true;
        // Usa o mesmo marcador que posiciona o jogador ao entrar nesta fase.
        foreach (var raiz in gameObject.scene.GetRootGameObjects())
            foreach (var ponto in raiz.GetComponentsInChildren<Transform>(true))
                if (ponto.name == "PontoEntrada" && ponto.gameObject.activeInHierarchy)
                {
                    retornoNasPedras = ponto;
                    return true;
                }
        return false;
    }
    void AoAlternarLanterna(bool ligada)
    {
        if(Estado!=Fase.Preso)return;
        if(Time.time>=prazo){Falhar();return;}
        if(ligada){acendeuNoOlho=lanterna.EstaIluminando((centroDoRosto?centroDoRosto:olho).position,nado.transform);if(acendeuNoOlho)tempoDoPiscar=.65f;}
        else if(acendeuNoOlho)
        {
            acendeuNoOlho=false;Piscadas++;
            if(Piscadas>=3){inicio=jogador.transform.position;tempo=0;Estado=Fase.Soltando;tentaculo.DescongelarCaptura();tempoDoPiscar=1.1f;}
        }
    }
    void LateUpdate()
    {
        if(!Ativa)
        {
            if(Reagindo)
            {
                tempoDeCoceira=Mathf.Max(0,tempoDeCoceira-Time.deltaTime*(batalha?batalha.MultiplicadorVelocidade:1f));
                if(olho && tentaculo)
                {
                    Vector3 lado=Vector3.Cross(Vector3.up,olho.forward).normalized;
                    Vector3 esfregar=olho.position+olho.forward*.4f+Vector3.up*(Mathf.Sin(Time.time*11)*.7f)+lado*(Mathf.Sin(Time.time*5)*.25f);
                    tentaculo.CocarNoPonto(esfregar);
                }
                if(palpebras)palpebras.Fechar(1);
                if(!Reagindo)EncerrarReacao();
            }
            return;
        }
        if(Estado==Fase.Preso && Time.time>=prazo){Falhar();return;}
        if(!jogador || !vida || vida.Morto || !olho || !tentaculo || (batalha && (batalha.Estado==BatalhaKraken.Etapa.Derrotado || batalha.Estado==BatalhaKraken.Etapa.ToucaCaindo))){Cancelar();return;}
        tempo+=Time.deltaTime*(batalha?batalha.MultiplicadorVelocidade:1f);
        Transform rosto=centroDoRosto?centroDoRosto:olho;
        Vector3 perto=rosto.position+rosto.forward*distanciaDoRosto-Vector3.up*1.25f;
        Vector3 pos;
        if(Estado==Fase.Levando)
        {
            float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(tempo/tempoParaPegar));
            pos=Vector3.Lerp(inicio,perto,t)+Vector3.up*(Mathf.Sin(Mathf.PI*t)*2);
            if(tempo>=tempoParaPegar){Estado=Fase.Preso;posicaoPresa=perto;tempo=0;prazo=Time.time+tempoLimite;lanterna.DefinirLigada(false);}
        }
        else if(Estado==Fase.Soltando)
        {
            float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(tempo/tempoParaSoltar));
            pos=Vector3.Lerp(inicio,destinoPedras,t)+Vector3.up*(Mathf.Sin(Mathf.PI*t)*3);
        }
        else pos=posicaoPresa;
        jogador.transform.position=pos;
        Vector3 frente=Vector3.ProjectOnPlane(rosto.position-pos,Vector3.up);
        if(frente.sqrMagnitude>.01f)jogador.transform.rotation=Quaternion.LookRotation(frente,Vector3.up);
        tentaculo.SegurarNoPonto(jogador.transform.TransformPoint(centroLocal));
        if(cameraDaCaptura)
        {
            Vector3 direcao=(rosto.position-(pos+Vector3.up)).normalized;
            Vector3 lado=Vector3.Cross(Vector3.up,direcao).normalized;
            Vector3 cam=pos-direcao*10f+Vector3.up*4f;
            float mistura=Estado==Fase.Levando?Mathf.SmoothStep(0,1,Mathf.Clamp01(tempo/tempoParaPegar)):1;
            cameraDaCaptura.transform.position=Vector3.Lerp(cameraOriginal.transform.position,cam,mistura);
            cameraDaCaptura.transform.rotation=Quaternion.Slerp(cameraOriginal.transform.rotation,Quaternion.LookRotation(rosto.position-cameraDaCaptura.transform.position,Vector3.up),mistura);
        }
        if(indiceDoPiscar>=0)
        {
            tempoDoPiscar=Mathf.Max(0,tempoDoPiscar-Time.deltaTime);
            float fechar=Estado==Fase.Soltando?1:tempoDoPiscar>0?Mathf.Clamp01(tempoDoPiscar/.12f):0;
            if(!palpebras)malhaDosOlhos.SetBlendShapeWeight(indiceDoPiscar,fechar*100);
            if(palpebras)palpebras.Fechar(fechar);
        }
        if(Estado==Fase.Soltando && tempo>=tempoParaSoltar)Finalizar(true,true);
    }
    public void AcompanharPontaResolvida()
    {
        if(!Ativa || !jogador || !tentaculo)return;
        Vector3 deslocamento=tentaculo.ContatoVisivel()-jogador.transform.TransformPoint(centroLocal);
        jogador.transform.position+=deslocamento;
        if(cameraDaCaptura){cameraDaCaptura.transform.position+=deslocamento;cameraDaCaptura.transform.LookAt(centroDoRosto?centroDoRosto.position:olho.position,Vector3.up);}
        if(Estado==Fase.Preso)tentaculo.CongelarCaptura();
        if(lanterna)lanterna.AtualizarDirecao();
    }
    void Falhar()
    {
        var vitima=vida;
        Finalizar(false);
        if(vitima)vitima.Morrer();
    }
    void EncerrarReacao()
    {
        tempoDeCoceira=0;
        if(tentaculo)tentaculo.EncerrarCaptura();
        if(palpebras)palpebras.Fechar(0);
    }
    public void Cancelar(){if(Ativa)Finalizar(true);if(Reagindo)EncerrarReacao();}
    void Finalizar(bool devolver, bool reagir=false)
    {
        if(lanterna){lanterna.EstadoAlterado-=AoAlternarLanterna;lanterna.AlvoDeMiraTemporario=null;lanterna.DefinirLigada(luzAnterior);}
        if(jogador){if(devolver && (!vida || !vida.Morto))jogador.transform.position=destinoPedras;jogador.transform.rotation=devolver?rotacaoRetorno:rotacaoInicial;Physics.SyncTransforms();if(capsula)capsula.enabled=capsulaAnterior;if(animador)animador.enabled=animadorAnterior;if(!vida || !vida.Morto){jogador.PrepararTrocaDeMundo(cameraOriginal);jogador.enabled=controleAnterior;}}
        if(tentaculo)tentaculo.EncerrarCaptura();
        if(reagir)tempoDeCoceira=2.8f;
        if(palpebras)palpebras.Fechar(reagir?1:0);
        if(malhaDosOlhos && indiceDoPiscar>=0)malhaDosOlhos.SetBlendShapeWeight(indiceDoPiscar,0);
        if(cameraOriginal)cameraOriginal.enabled=cameraAnterior;
        if(cameraDaCaptura)Destroy(cameraDaCaptura.gameObject);
        Estado=Fase.Livre;jogador=null;lanterna=null;tempoDoPiscar=0;
        foreach(var cam in FindObjectsByType<Unity.Cinemachine.CinemachineCamera>())cam.PreviousStateIsValid=false;
    }
    void OnDisable(){Cancelar();}
    void OnGUI()
    {
        if(!Ativa)return;
        var style=new GUIStyle(GUI.skin.label){font=fonteInterface,fontSize=Mathf.Clamp(Screen.height/30,18,32),alignment=TextAnchor.MiddleCenter,wordWrap=true};
        float width=Mathf.Min(Screen.width-32,700);var rect=new Rect((Screen.width-width)/2,Screen.height-150,width,125);
        GUI.Box(rect,"");GUI.Label(new Rect(rect.x+12,rect.y+8,rect.width-24,rect.height-16),Mensagem,style);
    }
}
