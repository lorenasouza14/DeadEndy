using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[DefaultExecutionOrder(300)]
public class PalpebrasKraken : MonoBehaviour
{
    public SkinnedMeshRenderer peleVermelha;
    public SkinnedMeshRenderer malhaOriginal;
    public string nomeDaForma = "PalpebraOriginalFechando";
    [Min(.01f)] public float tempoParaFechar = .16f;
    [Min(.01f)] public float tempoParaAbrir = .12f;
    public float Fechamento { get; private set; }
    public SkinnedMeshRenderer PalpebrasVisuais => palpebrasVisuais;
    float alvo, progresso;
    Mesh fonte, malhaPalpebras;
    Material materialPalpebras;
    SkinnedMeshRenderer palpebrasVisuais;
    Matrix4x4[] ossosNaPose;
    int formaAntiga = -1, pupilaAntiga = -1;
    const string FormaVisual = "FecharSemDeformarRosto";

    struct Ponto
    {
        public Vector3 mundo, normal;
        public Vector2 uv;
        public BoneWeight peso;
    }

    void Awake()
    {
        if (!malhaOriginal || !malhaOriginal.sharedMesh) return;
        fonte = malhaOriginal.sharedMesh;
        formaAntiga = fonte.GetBlendShapeIndex(nomeDaForma);
        pupilaAntiga = fonte.GetBlendShapeIndex("PiscarOlhos");
        RestaurarOriginal();
        CriarPalpebras();
    }

    void RestaurarOriginal()
    {
        if (peleVermelha) peleVermelha.enabled = false;
        if (!malhaOriginal) return;
        if (formaAntiga >= 0) malhaOriginal.SetBlendShapeWeight(formaAntiga, 0);
        if (pupilaAntiga >= 0) malhaOriginal.SetBlendShapeWeight(pupilaAntiga, 0);
    }

    static int Raiz(int[] pai, int i)
    {
        while (pai[i] != i) { pai[i] = pai[pai[i]]; i = pai[i]; }
        return i;
    }

    Matrix4x4 Matriz(BoneWeight w)
    {
        var a = ossosNaPose[w.boneIndex0]; var b = ossosNaPose[w.boneIndex1];
        var c = ossosNaPose[w.boneIndex2]; var d = ossosNaPose[w.boneIndex3];
        var m = new Matrix4x4();
        for (int r = 0; r < 4; r++) for (int col = 0; col < 4; col++)
            m[r,col] = a[r,col]*w.weight0+b[r,col]*w.weight1+c[r,col]*w.weight2+d[r,col]*w.weight3;
        return m;
    }

    static BoneWeight MisturarPesos(BoneWeight a, BoneWeight b, float t)
    {
        var pesos = new Dictionary<int,float>();
        Somar(pesos,a.boneIndex0,a.weight0*(1-t)); Somar(pesos,a.boneIndex1,a.weight1*(1-t));
        Somar(pesos,a.boneIndex2,a.weight2*(1-t)); Somar(pesos,a.boneIndex3,a.weight3*(1-t));
        Somar(pesos,b.boneIndex0,b.weight0*t); Somar(pesos,b.boneIndex1,b.weight1*t);
        Somar(pesos,b.boneIndex2,b.weight2*t); Somar(pesos,b.boneIndex3,b.weight3*t);
        var top = pesos.OrderByDescending(p=>p.Value).Take(4).ToArray();
        var indices = new int[4]; var valores = new float[4]; float total=top.Sum(p=>p.Value);
        for(int i=0;i<top.Length;i++){indices[i]=top[i].Key;valores[i]=top[i].Value/total;}
        return new BoneWeight { boneIndex0=indices[0],boneIndex1=indices[1],boneIndex2=indices[2],boneIndex3=indices[3],
            weight0=valores[0],weight1=valores[1],weight2=valores[2],weight3=valores[3] };
    }

    static void Somar(Dictionary<int,float> pesos,int osso,float valor)
    {
        if(valor<=0)return;
        pesos.TryGetValue(osso,out float atual);pesos[osso]=atual+valor;
    }

    static Ponto Interpolar(Ponto a,Ponto b,float t)
    {
        return new Ponto { mundo=Vector3.Lerp(a.mundo,b.mundo,t), normal=Vector3.Lerp(a.normal,b.normal,t).normalized,
            uv=Vector2.Lerp(a.uv,b.uv,t),peso=MisturarPesos(a.peso,b.peso,t) };
    }

    static List<Ponto> Recortar(Ponto[] tri,float linha,bool superior)
    {
        var resultado=new List<Ponto>();
        for(int i=0;i<3;i++)
        {
            var a=tri[i];var b=tri[(i+1)%3];
            bool dentroA=superior?a.mundo.y>=linha:a.mundo.y<=linha;
            bool dentroB=superior?b.mundo.y>=linha:b.mundo.y<=linha;
            if(dentroA)resultado.Add(a);
            if(dentroA!=dentroB)resultado.Add(Interpolar(a,b,(linha-a.mundo.y)/(b.mundo.y-a.mundo.y)));
        }
        return resultado;
    }

    void CriarPalpebras()
    {
        if(fonte.subMeshCount<2 || malhaOriginal.sharedMaterials.Length<2)return;
        var vertices=fonte.vertices;var normais=fonte.normals;var pesos=fonte.boneWeights;var uv=fonte.uv;
        var poses=fonte.bindposes;var bones=malhaOriginal.bones;
        ossosNaPose=bones.Select((b,i)=>b.localToWorldMatrix*poses[i]).ToArray();
        var triangles=fonte.GetTriangles(1);var ids=triangles.Distinct().ToArray();
        var pai=Enumerable.Range(0,vertices.Length).ToArray();var weld=new Dictionary<Vector3,int>();
        foreach(int i in ids){if(weld.TryGetValue(vertices[i],out int j))pai[Raiz(pai,i)]=Raiz(pai,j);else weld[vertices[i]]=i;}
        for(int t=0;t<triangles.Length;t+=3){pai[Raiz(pai,triangles[t])]=Raiz(pai,triangles[t+1]);pai[Raiz(pai,triangles[t+1])]=Raiz(pai,triangles[t+2]);}
        var eyes=ids.GroupBy(i=>Raiz(pai,i)).OrderByDescending(g=>g.Count()).Take(2).Select(g=>g.ToArray()).ToArray();
        if(eyes.Length!=2)return;
        var pontos=new Ponto[vertices.Length];
        foreach(int i in ids)pontos[i]=new Ponto { mundo=Matriz(pesos[i]).MultiplyPoint3x4(vertices[i]),normal=normais[i],
            uv=uv.Length==vertices.Length?uv[i]:Vector2.zero,peso=pesos[i] };
        var centros=eyes.Select(g=>g.Aggregate(Vector3.zero,(s,i)=>s+pontos[i].mundo)/g.Length).ToArray();
        Vector3 meio=(centros[0]+centros[1])*.5f;
        var abertos=new List<Vector3>();var fechados=new List<Vector3>();var ns=new List<Vector3>();
        var uvs=new List<Vector2>();var ws=new List<BoneWeight>();var tr=new List<int>();
        for(int e=0;e<2;e++)
        {
            var conjunto=new HashSet<int>(eyes[e]);
            var faces=new List<int[]>();
            for(int t=0;t<triangles.Length;t+=3)if(conjunto.Contains(triangles[t]))faces.Add(new[]{triangles[t],triangles[t+1],triangles[t+2]});
            Vector3 centro=centros[e], fora=Vector3.ProjectOnPlane(centro-meio,Vector3.up).normalized;
            Vector3 lado=Vector3.Cross(Vector3.up,fora).normalized;
            float baixo=eyes[e].Min(i=>pontos[i].mundo.y), alto=eyes[e].Max(i=>pontos[i].mundo.y);
            float altura=alto-baixo, linha=(alto+baixo)*.5f, folga=altura*.012f, espessura=altura*.006f;
            var cache=new Dictionary<long,Vector3>();
            for(int metade=0;metade<2;metade++)
            {
                bool superior=metade==0;
                foreach(var face in faces)
                {
                    var poligono=Recortar(face.Select(i=>pontos[i]).ToArray(),linha+(superior?folga:-folga),superior);
                    if(poligono.Count<3)continue;
                    int inicio=abertos.Count;
                    foreach(var p in poligono)
                    {
                        float x=Vector3.Dot(p.mundo-centro,lado);
                        long chave=((long)Mathf.RoundToInt(x*10000)<<1)|metade;
                        if(!cache.TryGetValue(chave,out Vector3 borda))
                        {
                            borda=p.mundo;float melhor=superior?float.NegativeInfinity:float.PositiveInfinity;
                            foreach(var f in faces)for(int k=0;k<3;k++)
                            {
                                Vector3 a=pontos[f[k]].mundo,b=pontos[f[(k+1)%3]].mundo;
                                float ax=Vector3.Dot(a-centro,lado),bx=Vector3.Dot(b-centro,lado);
                                if(x<Mathf.Min(ax,bx)-.0001f||x>Mathf.Max(ax,bx)+.0001f)continue;
                                Vector3 q=Mathf.Abs(bx-ax)<.00001f?(superior?(a.y>b.y?a:b):(a.y<b.y?a:b)):Vector3.Lerp(a,b,Mathf.Clamp01((x-ax)/(bx-ax)));
                                if(superior?q.y>melhor:q.y<melhor){melhor=q.y;borda=q;}
                            }
                            cache[chave]=borda;
                        }
                        var matriz=Matriz(p.peso);
                        Vector3 normal=matriz.inverse.transpose.MultiplyVector(p.normal).normalized;
                        if(Vector3.Dot(normal,fora)<0)normal=-normal;
                        Vector3 aberta=matriz.inverse.MultiplyPoint3x4(centro+(borda-centro)*1.014f);
                        Vector3 fechada=matriz.inverse.MultiplyPoint3x4(centro+(p.mundo-centro)*1.014f);
                        abertos.Add(aberta);fechados.Add(fechada);ns.Add(p.normal);uvs.Add(p.uv);ws.Add(p.peso);
                    }
                    for(int j=1;j<poligono.Count-1;j++){tr.Add(inicio);tr.Add(inicio+j);tr.Add(inicio+j+1);}
                }
            }
        }
        malhaPalpebras=new Mesh { name="Palpebras superior e inferior ajustadas aos olhos" };
        if(abertos.Count>65535)malhaPalpebras.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
        malhaPalpebras.SetVertices(abertos);malhaPalpebras.SetNormals(ns);malhaPalpebras.SetUVs(0,uvs);
        malhaPalpebras.boneWeights=ws.ToArray();malhaPalpebras.bindposes=poses;malhaPalpebras.SetTriangles(tr,0);
        var delta=abertos.Select((v,i)=>fechados[i]-v).ToArray();
        malhaPalpebras.AddBlendShapeFrame(FormaVisual,100,delta,new Vector3[delta.Length],new Vector3[delta.Length]);
        var go=new GameObject("Palpebras ajustadas aos olhos");go.transform.SetParent(malhaOriginal.transform,false);
        palpebrasVisuais=go.AddComponent<SkinnedMeshRenderer>();palpebrasVisuais.sharedMesh=malhaPalpebras;
        palpebrasVisuais.bones=bones;palpebrasVisuais.rootBone=malhaOriginal.rootBone;
        palpebrasVisuais.localBounds=malhaOriginal.localBounds;palpebrasVisuais.updateWhenOffscreen=true;
        materialPalpebras=new Material(malhaOriginal.sharedMaterials[0]);
        materialPalpebras.name="Pele original das palpebras";
        if(materialPalpebras.HasProperty("_Cull"))materialPalpebras.SetFloat("_Cull",0);
        palpebrasVisuais.sharedMaterial=materialPalpebras;
        palpebrasVisuais.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        palpebrasVisuais.receiveShadows=false;palpebrasVisuais.enabled=false;
    }

    public void Fechar(float valor){alvo=Mathf.Clamp01(valor);}
    void LateUpdate()
    {
        float duracao=alvo>progresso?tempoParaFechar:tempoParaAbrir;
        progresso=Mathf.MoveTowards(progresso,alvo,Time.deltaTime/Mathf.Max(.01f,duracao));
        Fechamento=Mathf.SmoothStep(0,1,progresso);
        RestaurarOriginal();
        if(palpebrasVisuais){palpebrasVisuais.enabled=Fechamento>.001f;palpebrasVisuais.SetBlendShapeWeight(0,Fechamento*100);}
    }
    void OnDisable()
    {
        alvo=progresso=Fechamento=0;RestaurarOriginal();
        if(palpebrasVisuais){palpebrasVisuais.SetBlendShapeWeight(0,0);palpebrasVisuais.enabled=false;}
    }
    void OnDestroy()
    {
        if(palpebrasVisuais)Destroy(palpebrasVisuais.gameObject);
        if(malhaPalpebras)Destroy(malhaPalpebras);
        if(materialPalpebras)Destroy(materialPalpebras);
    }
}
