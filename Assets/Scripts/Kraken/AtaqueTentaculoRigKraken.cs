using UnityEngine;

/// <summary>Golpeia usando os ossos e a malha originais de um tentaculo do Kraken.</summary>
[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public class AtaqueTentaculoRigKraken : MonoBehaviour
{
    public KrakenNadando nado;
    public BatalhaKraken batalha;
    [Tooltip("Ponto calibrado na malha visivel da ponta, em vez do marcador de importacao.")]
    public Transform pontoDeContatoDaMalha;
    public SkinnedMeshRenderer malhaDoKraken;
    public int verticeDeContato = -1;
    Vector3 verticeLocal;
    BoneWeight pesosDoContato;
    Matrix4x4[] posesDoContato;
    Transform[] ossosDaMalha;
    Vector3 ajusteDeContato;
    [Min(0)] public float tempoDeContato = .16f;
    [Min(0)] public int indiceDoTentaculo = 1;
    [Min(.05f)] public float tempoDePreparacao = .4f;
    [Min(1)] public float esticamentoMaximo = 3f;
    [Min(0)] public float alturaDePreparacao = 26f;
    [Min(0)] public float arcoDoGolpe = 10f;
    Transform[] nos;
    Vector3[] posicoesOriginais, resolvidas;
    Quaternion[] rotacoesNaturais;
    float[] comprimentos;
    float peso, tempoNoEstado, contatoRestante;
    BatalhaKraken.Etapa estadoAnterior = (BatalhaKraken.Etapa)(-1);
    Vector3 preparacao, ultimoAlvo, alvoDoImpacto;
    bool impactoPendente, controleExterno, cocando;
    Vector3 alvoExterno;
    bool capturaCongelada;
    Vector3[] posicoesPresas;
    Quaternion[] rotacoesPresas;
    public void CongelarCaptura()
    {
        if(capturaCongelada || nos==null)return;
        posicoesPresas=new Vector3[nos.Length];rotacoesPresas=new Quaternion[nos.Length];
        for(int i=0;i<nos.Length;i++){posicoesPresas[i]=nos[i].position;rotacoesPresas[i]=nos[i].rotation;}
        capturaCongelada=true;
    }
    public void DescongelarCaptura(){capturaCongelada=false;}
    public void CocarNoPonto(Vector3 ponto) { if(!cocando)ajusteDeContato=Vector3.zero; cocando=true; controleExterno=true; alvoExterno=ponto; }
    public void SegurarNoPonto(Vector3 ponto) { cocando=false; controleExterno=true; alvoExterno=ponto; }
    public void EncerrarCaptura() { cocando=false; capturaCongelada=false; ajusteDeContato=Vector3.zero; controleExterno=false; impactoPendente=false; contatoRestante=0; }

    void Awake() { PrepararRig(); }
    void Update() { RestaurarComprimentos(); }
    void LateUpdate()
    {
        if (!nado || !batalha) return;
        Animar(batalha.Estado, batalha.PontoDoGolpe, batalha.ProgressoDoGolpe, Time.deltaTime * (batalha.captura && (batalha.captura.Ativa || batalha.captura.Reagindo)
            ? batalha.MultiplicadorVelocidade : batalha.MultiplicadorVelocidadeAtaques));
    }

    bool PrepararRig()
    {
        if (nos != null) return true;
        if (!nado || nado.tentaculos == null || indiceDoTentaculo >= nado.tentaculos.Length) return false;
        var cadeia = nado.tentaculos[indiceDoTentaculo].ossos;
        if (cadeia == null || cadeia.Length < 3) return false;
        var fim = cadeia[cadeia.Length-1];
        Transform ponta = pontoDeContatoDaMalha && pontoDeContatoDaMalha.parent == fim ? pontoDeContatoDaMalha : null;
        for (int i=0;!ponta && i<fim.childCount;i++)
            if (fim.GetChild(i).name.EndsWith("_end")) { ponta=fim.GetChild(i);break; }
        if (!ponta) return false;
        if(malhaDoKraken && verticeDeContato>=0 && verticeDeContato<malhaDoKraken.sharedMesh.vertexCount)
        {
            var mesh=malhaDoKraken.sharedMesh;
            verticeLocal=mesh.vertices[verticeDeContato];
            pesosDoContato=mesh.boneWeights[verticeDeContato];
            posesDoContato=mesh.bindposes;
            ossosDaMalha=malhaDoKraken.bones;
        }
        nos=new Transform[cadeia.Length+1];
        for(int i=0;i<cadeia.Length;i++) nos[i]=cadeia[i];
        nos[nos.Length-1]=ponta;
        posicoesOriginais=new Vector3[nos.Length];
        rotacoesNaturais=new Quaternion[nos.Length];
        resolvidas=new Vector3[nos.Length];
        comprimentos=new float[nos.Length-1];
        for(int i=0;i<nos.Length;i++)
        {
            posicoesOriginais[i]=nos[i].localPosition;
            rotacoesNaturais[i]=nos[i].localRotation;
        }
        return true;
    }

    public void RestaurarComprimentos()
    {
        if (nos == null) return;
        for(int i=0;i<nos.Length;i++) if(nos[i]) nos[i].localPosition=posicoesOriginais[i];
    }

    public void MarcarImpacto(Vector3 ponto, bool contatoNoCorpo = false)
    {
        alvoDoImpacto=contatoNoCorpo?ponto:ponto+Vector3.up*.6f;
        contatoRestante=tempoDeContato;
        impactoPendente=true;
    }

    public void Animar(BatalhaKraken.Etapa estado, Vector3 pontoDoGolpe, float progressoDoGolpe, float deltaTime)
    {
        if(!PrepararRig()) return;
        if(controleExterno && !cocando)
        {
            if(capturaCongelada)
            {
                for(int i=0;i<nos.Length;i++)nos[i].SetPositionAndRotation(posicoesPresas[i],rotacoesPresas[i]);
                return;
            }
            ResolverCapturaPorBaixo(alvoExterno);
            return;
        }
        if(cocando)
        {
            // Mantem os segmentos originais, em vez da curva curta de suporte do jogador.
            RestaurarComprimentos();
            for(int i=0;i<nos.Length;i++)nos[i].localRotation=rotacoesNaturais[i];
        }
        else for(int i=0;i<nos.Length;i++) rotacoesNaturais[i]=nos[i].localRotation;
        Vector3 raiz=nos[0].position, pontaNatural=nos[nos.Length-1].position;
        if(estado!=estadoAnterior)
        {
            tempoNoEstado=0;
            estadoAnterior=estado;
            if(estado==BatalhaKraken.Etapa.Avisando)
            {
                Vector3 frente=Vector3.ProjectOnPlane(pontoDoGolpe-raiz,Vector3.up).normalized;
                if(frente.sqrMagnitude<.01f) frente=transform.forward;
                Vector3 lado=Vector3.Cross(Vector3.up,frente);
                if(Vector3.Dot(nos[1].position-raiz,lado)<0)lado=-lado;
                preparacao=raiz+frente*14f+lado*10f+Vector3.up*alturaDePreparacao;
            }
        }
        tempoNoEstado+=Mathf.Max(0,deltaTime);
        Vector3 alvo=ultimoAlvo;
        if(controleExterno)
        {
            alvo=alvoExterno;
            peso=1;
        }
        else if(impactoPendente || contatoRestante>0)
        {
            alvo=alvoDoImpacto;
            peso=1;
            impactoPendente=false;
            contatoRestante=Mathf.Max(0,contatoRestante-deltaTime);
        }
        else if(estado==BatalhaKraken.Etapa.Avisando)
        {
            peso=Mathf.MoveTowards(peso,1,deltaTime/Mathf.Max(.05f,tempoDePreparacao));
            alvo=preparacao;
        }
        else if(estado==BatalhaKraken.Etapa.Golpeando)
        {
            float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(progressoDoGolpe));
            Vector3 contato=batalha?batalha.PontoDeContato:pontoDoGolpe+Vector3.up*.6f;
            alvo=Vector3.Lerp(preparacao,contato,t)
                +Vector3.up*(Mathf.Sin(Mathf.PI*t)*arcoDoGolpe);
            peso=1;
        }
        else if(estado==BatalhaKraken.Etapa.Recuperando)
        {
            float duracao=batalha?batalha.intervalo:2.2f;
            peso=1-Mathf.SmoothStep(0,1,Mathf.Clamp01(tempoNoEstado/Mathf.Max(.1f,duracao)));
            alvo=Vector3.Lerp(ultimoAlvo,pontaNatural,Mathf.Clamp01(deltaTime*4));
        }
        else
        {
            peso=Mathf.MoveTowards(peso,0,deltaTime/.3f);
            alvo=Vector3.Lerp(ultimoAlvo,pontaNatural,Mathf.Clamp01(deltaTime*6));
        }
        ultimoAlvo=alvo;
        if(peso<=.0001f) return;
        Vector3 alvoDoRig=alvo+(controleExterno?ajusteDeContato:Vector3.zero);
        Resolver(alvoDoRig,peso);
        // A pele recebe influencia de varios ossos: corrige pelo vertice visivel.
        if(peso>.999f && posesDoContato!=null)
        {
            for(int tentativa=0;tentativa<(controleExterno?48:16);tentativa++)
            {
                Vector3 erro=alvo-ContatoVisivel();
                if(erro.sqrMagnitude<.0004f)break;
                alvoDoRig+=erro*.85f;
                RestaurarComprimentos();
                for(int i=0;i<nos.Length;i++)nos[i].localRotation=rotacoesNaturais[i];
                Resolver(alvoDoRig,1);
            }
            if(controleExterno)ajusteDeContato=Vector3.ClampMagnitude(alvoDoRig-alvo,35);
        }
    }

    // Curva suspensa sob o jogador. Sem a onda do nado e sem mudar de dobra a cada quadro.
    void ResolverCapturaPorBaixo(Vector3 alvo)
    {
        CurvaPorBaixo(alvo);
        // Corrige a pele pela translacao distribuida dos ossos, mantendo a dobra por baixo.
        float influencia=0;var w=pesosDoContato;
        for(int i=1;i<nos.Length-1;i++)
        {
            float f=i/(float)(nos.Length-2);
            int bi=System.Array.IndexOf(ossosDaMalha,nos[i]);
            influencia+=f*((w.boneIndex0==bi?w.weight0:0)+(w.boneIndex1==bi?w.weight1:0)+(w.boneIndex2==bi?w.weight2:0)+(w.boneIndex3==bi?w.weight3:0));
            resolvidas[i]=nos[i].position;
        }
        if(influencia>.001f)
        {
            Vector3 ajuste=(alvo-ContatoVisivel())/influencia;
            for(int i=1;i<nos.Length-1;i++)nos[i].position=resolvidas[i]+ajuste*(i/(float)(nos.Length-2));
        }
    }

    void CurvaPorBaixo(Vector3 alvo)
    {
        Vector3 raiz=nos[0].position;
        Vector3 frente=Vector3.ProjectOnPlane(alvo-raiz,Vector3.up).normalized;
        Vector3 c1=raiz-Vector3.up*24-frente*8;
        Vector3 c2=alvo-Vector3.up*22+frente*6;
        for(int i=0;i<nos.Length;i++)
        {
            float t=i/(float)(nos.Length-1),v=1-t;
            resolvidas[i]=v*v*v*raiz+3*v*v*t*c1+3*v*t*t*c2+t*t*t*alvo;
        }
        RestaurarComprimentos();
        for(int i=0;i<nos.Length;i++)nos[i].localRotation=rotacoesNaturais[i];
        for(int i=0;i<nos.Length-1;i++)
        {
            Vector3 atual=nos[i+1].position-nos[i].position;
            Vector3 desejada=resolvidas[i+1]-resolvidas[i];
            if(atual.sqrMagnitude>.0001f && desejada.sqrMagnitude>.0001f)
                nos[i].rotation=Quaternion.FromToRotation(atual,desejada)*nos[i].rotation;
            if(i>0)nos[i].position=resolvidas[i];
        }
        nos[nos.Length-1].position=resolvidas[resolvidas.Length-1];
    }

    public Vector3 ContatoVisivel()
    {
        if(posesDoContato==null)return pontoDeContatoDaMalha?pontoDeContatoDaMalha.position:transform.position;
        var w=pesosDoContato;var b=ossosDaMalha;
        return w.weight0*(b[w.boneIndex0].localToWorldMatrix*posesDoContato[w.boneIndex0]).MultiplyPoint3x4(verticeLocal)
            +w.weight1*(b[w.boneIndex1].localToWorldMatrix*posesDoContato[w.boneIndex1]).MultiplyPoint3x4(verticeLocal)
            +w.weight2*(b[w.boneIndex2].localToWorldMatrix*posesDoContato[w.boneIndex2]).MultiplyPoint3x4(verticeLocal)
            +w.weight3*(b[w.boneIndex3].localToWorldMatrix*posesDoContato[w.boneIndex3]).MultiplyPoint3x4(verticeLocal);
    }

    void Resolver(Vector3 alvo,float mistura)
    {
        if(controleExterno) ResolverLivre(alvo,mistura);
        else ResolverComPontaCurva(alvo,mistura);
    }

    void ResolverComPontaCurva(Vector3 alvo,float mistura)
    {
        // Os tres ultimos ossos formam uma ponta rigida e enrolada.
        // O ultimo segmento do solver e virtual: vai da base da curva ao contato visivel.
        int curva = Mathf.Max(1, nos.Length - 5);
        int fim = curva + 1;
        float total=0;
        for(int i=0;i<=curva;i++) resolvidas[i]=nos[i].position;
        resolvidas[fim]=ContatoVisivel();
        for(int i=0;i<fim;i++)
        {
            comprimentos[i]=Vector3.Distance(resolvidas[i],resolvidas[i+1]);
            total+=comprimentos[i];
        }
        Vector3 origem=resolvidas[0];
        float esticar=Mathf.Clamp(Vector3.Distance(origem,alvo)/Mathf.Max(.01f,total)*1.04f,1,esticamentoMaximo);
        total=0; for(int i=0;i<fim;i++) { if(i<curva) comprimentos[i]*=esticar; total+=comprimentos[i]; }
        if(Vector3.Distance(origem,alvo)>=total)
        {
            Vector3 direcao=(alvo-origem).normalized;
            for(int i=1;i<(fim+1);i++)resolvidas[i]=resolvidas[i-1]+direcao*comprimentos[i-1];
        }
        else for(int iteracao=0;iteracao<40;iteracao++)
        {
            resolvidas[(fim+1)-1]=alvo;
            for(int i=(fim+1)-2;i>=0;i--)
            {
                Vector3 direcao=(resolvidas[i]-resolvidas[i+1]).normalized;
                if(direcao.sqrMagnitude<.1f)direcao=Vector3.down;
                resolvidas[i]=resolvidas[i+1]+direcao*comprimentos[i];
            }
            resolvidas[0]=origem;
            for(int i=1;i<(fim+1);i++)
            {
                Vector3 direcao=(resolvidas[i]-resolvidas[i-1]).normalized;
                if(direcao.sqrMagnitude<.1f)direcao=Vector3.up;
                resolvidas[i]=resolvidas[i-1]+direcao*comprimentos[i-1];
            }
            if((resolvidas[(fim+1)-1]-alvo).sqrMagnitude<.0004f)break;
        }
        float fator=Mathf.Lerp(1,esticar,mistura);
        for(int i=1;i<nos.Length;i++)nos[i].localPosition=posicoesOriginais[i]*(i<=curva?fator:1);
        for(int i=0;i<=curva;i++)
        {
            Vector3 atual=(i==curva?ContatoVisivel():nos[i+1].position)-nos[i].position;
            Vector3 desejada=resolvidas[i+1]-resolvidas[i];
            if(atual.sqrMagnitude<.0001f||desejada.sqrMagnitude<.0001f)continue;
            Quaternion rotacao=Quaternion.FromToRotation(atual,desejada)*nos[i].rotation;
            nos[i].rotation=Quaternion.Slerp(nos[i].rotation,rotacao,mistura);
        }
    }

    void ResolverLivre(Vector3 alvo,float mistura)
    {
        float total=0;
        for(int i=0;i<nos.Length;i++) resolvidas[i]=nos[i].position;
        for(int i=0;i<comprimentos.Length;i++)
        {
            comprimentos[i]=Vector3.Distance(resolvidas[i],resolvidas[i+1]);
            total+=comprimentos[i];
        }
        Vector3 origem=resolvidas[0];
        float esticar=Mathf.Clamp(Vector3.Distance(origem,alvo)/Mathf.Max(.01f,total)*1.04f,1,esticamentoMaximo);
        total=0; for(int i=0;i<comprimentos.Length;i++) { if(i<comprimentos.Length-1 || !pontoDeContatoDaMalha) comprimentos[i]*=esticar; total+=comprimentos[i]; }
        if(Vector3.Distance(origem,alvo)>=total)
        {
            Vector3 direcao=(alvo-origem).normalized;
            for(int i=1;i<resolvidas.Length;i++)resolvidas[i]=resolvidas[i-1]+direcao*comprimentos[i-1];
        }
        else for(int iteracao=0;iteracao<40;iteracao++)
        {
            resolvidas[resolvidas.Length-1]=alvo;
            for(int i=resolvidas.Length-2;i>=0;i--)
            {
                Vector3 direcao=(resolvidas[i]-resolvidas[i+1]).normalized;
                if(direcao.sqrMagnitude<.1f)direcao=Vector3.down;
                resolvidas[i]=resolvidas[i+1]+direcao*comprimentos[i];
            }
            resolvidas[0]=origem;
            for(int i=1;i<resolvidas.Length;i++)
            {
                Vector3 direcao=(resolvidas[i]-resolvidas[i-1]).normalized;
                if(direcao.sqrMagnitude<.1f)direcao=Vector3.up;
                resolvidas[i]=resolvidas[i-1]+direcao*comprimentos[i-1];
            }
            if((resolvidas[resolvidas.Length-1]-alvo).sqrMagnitude<.0004f)break;
        }
        float fator=Mathf.Lerp(1,esticar,mistura);
        for(int i=1;i<nos.Length;i++)nos[i].localPosition=posicoesOriginais[i]*((i==nos.Length-1 && pontoDeContatoDaMalha)?1:fator);
        for(int i=0;i<nos.Length-1;i++)
        {
            Vector3 atual=nos[i+1].position-nos[i].position;
            Vector3 desejada=resolvidas[i+1]-resolvidas[i];
            if(atual.sqrMagnitude<.0001f||desejada.sqrMagnitude<.0001f)continue;
            Quaternion rotacao=Quaternion.FromToRotation(atual,desejada)*nos[i].rotation;
            nos[i].rotation=Quaternion.Slerp(nos[i].rotation,rotacao,mistura);
        }
    }

    void OnDisable()
    {
        RestaurarComprimentos();
        if(nos!=null)for(int i=0;i<nos.Length;i++)if(nos[i])nos[i].localRotation=rotacoesNaturais[i];
        peso=0;
        impactoPendente=false;
        contatoRestante=0;
        controleExterno=false;
    }
}
