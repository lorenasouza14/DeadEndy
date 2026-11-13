using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Ferramenta de verificacao da cena unificada; nao executa automaticamente.
[InitializeOnLoad]
public static class JuncaoFasesValidation
{
    public const string Cena = "Assets/Cenario/PNB_Tropical_Jungle/Scenes/juncao_fase1-2.unity";
    const string Terreno = "Assets/Terrain/Demo_Day_Juncao.asset";
    const string BaseTerreno = "Assets/Terrain/TemporarioBaseJuncao.asset";
    static readonly StringBuilder Relatorio = new StringBuilder();
    static JuncaoFasesValidation()
    {
        EditorApplication.update += Pendente;
        EditorApplication.playModeStateChanged += AoMudarModo;
    }
    static void AoMudarModo(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool("Juncao.RestaurarCenaInicial",false))
        {
            SessionState.SetBool("Juncao.RestaurarCenaInicial",false);
            string anterior=SessionState.GetString("Juncao.CenaInicialAnterior","");
            EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(anterior)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(anterior);
        }
        if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("Juncao.ValidarPlay",false))return;
        SessionState.SetBool("Juncao.ValidarPlay",false);
        foreach(var save in Object.FindObjectsByType<AutosaveEndy>(FindObjectsInactive.Include)) save.enabled=false;
        new GameObject("Verificacao temporaria da juncao").AddComponent<JuncaoFasesPlayProbe>();
    }
    static void Pendente()
    {
        const string request="Logs/juncao/comando.txt";
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request))return;
        string command;
        try{command=File.ReadAllText(request).Trim();File.Delete(request);}
        catch(IOException){return;}
        Relatorio.Clear();
        if(command=="estado")
        {
            var text=new StringBuilder();
            for(int i=0;i<SceneManager.sceneCount;i++){var s=SceneManager.GetSceneAt(i);text.AppendLine(s.path+" dirty="+s.isDirty+" loaded="+s.isLoaded);}
            File.WriteAllText("Logs/juncao/estado-editor.txt",text.ToString());
        }
        else if(command=="terrenos") InspecionarTerrenos();
        else if(command=="unir") UnirTerrenos();
        else if(command=="validar") Validar();
        else if(command=="inspecionar-itens") InspecionarItens();
        else if(command=="ajustar-alavanca") AjustarAlavanca();
        else if(command=="refresh") AssetDatabase.Refresh();
        else if(command=="play")
        {
            if(SceneManager.GetActiveScene().path!=Cena)throw new Exception("Abra a cena unificada antes do teste em Play.");
            IniciarTeste(false);
        }
        else if(command=="testar-alavanca")
        {
            IniciarTeste(true);
        }
    }
    static void IniciarTeste(bool alavanca)
    {
        SessionState.SetString("Juncao.CenaInicialAnterior",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool("Juncao.RestaurarCenaInicial",true);
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(Cena);
        SessionState.SetBool("Juncao.ValidarAlavanca",alavanca);
        SessionState.SetBool("Juncao.ValidarPlay",true);EditorApplication.EnterPlaymode();
    }

    static void InspecionarItens()
    {
        var atual=SceneManager.GetSceneByPath(Cena);
        var origem=EditorSceneManager.OpenPreviewScene(ProgressoJogo.CenaJogo);
        try
        {
            var text=new StringBuilder();
            var antiga=NaCena<Transform>(origem).Single(t=>t.name=="SM_Env_Rock_Ramp_02" && (t.position-new Vector3(-57.32f,1.96f,195.36f)).sqrMagnitude<.01f);
            var nova=NaCena<Transform>(atual).Single(t=>t.name==ArmadilhasRampaSetup.NomePedra);
            text.AppendLine($"RAMPA ORIGINAL pos={antiga.position:F4} rot={antiga.eulerAngles:F4} scale={antiga.lossyScale:F4}");
            text.AppendLine($"RAMPA UNIFICADA pos={nova.position:F4} rot={nova.eulerAngles:F4} scale={nova.lossyScale:F4}");
            foreach(var alavanca in NaCena<AlavancaTriangulo>(atual))
            {
                var original=NaCena<AlavancaTriangulo>(origem).Single(l=>l.idSave==alavanca.idSave);
                var local=antiga.InverseTransformPoint(original.transform.position);
                text.AppendLine($"ALAVANCA {alavanca.name} pos={alavanca.transform.position:F4} original={original.transform.position:F4} localNaRampa={local:F4} remapeada={nova.TransformPoint(local):F4}");
                foreach(var col in alavanca.GetComponents<Collider>())text.AppendLine($" collider={col.GetType().Name} trigger={col.isTrigger} bounds={col.bounds}");
                foreach(var hit in Physics.RaycastAll(alavanca.transform.position+Vector3.up*40,Vector3.down,80,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))text.AppendLine($" solo={Caminho(hit.transform)} ponto={hit.point:F4}");
            }
            foreach(var t in NaCena<Transform>(atual).Where(t=>t.GetComponent<BananaSopa>()!=null || t.GetComponent<AltarRuinas>()!=null || t.name.StartsWith("Moita armadilha")))text.AppendLine($"OBJ {Caminho(t)} pos={t.position:F4}");
            File.WriteAllText("Logs/juncao/itens-regiao.txt",text.ToString());
        }
        finally{EditorSceneManager.ClosePreviewScene(origem);}
    }

    [MenuItem("Tools/Mapa/Juncao/Integrar alavanca da primeira fase a rampa")]
    public static void AjustarAlavanca()
    {
        var cena=SceneManager.GetSceneByPath(Cena);
        if(!cena.IsValid() || !cena.isLoaded || EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Abra a juncao fora de Play.");
        var origem=EditorSceneManager.OpenPreviewScene(ProgressoJogo.CenaJogo);
        try
        {
            var original=NaCena<AlavancaTriangulo>(origem).Single(l=>l.indiceDaLuz==2);
            var alavanca=NaCena<AlavancaTriangulo>(cena).Single(l=>l.idSave==original.idSave);
            var rampa=NaCena<Transform>(cena).Single(t=>t.name==ArmadilhasRampaSetup.NomePedra).GetComponent<MeshCollider>();
            Physics.SyncTransforms();
            var ray=new Ray(original.transform.position+Vector3.up*40,Vector3.down);
            Exigir(rampa.Raycast(ray,out var hit,80),"Superficie da pedra ampliada encontrada acima da alavanca original");
            Exigir(hit.normal.y>.5f,"Alavanca fica sobre uma superficie caminhavel");
            Directory.CreateDirectory("Logs/juncao");EditorSceneManager.SaveScene(cena,"Logs/juncao/antes-alavanca-rampa.unity",true);
            Undo.RecordObject(alavanca.transform,"Integrar alavanca com a pedra das traps");
            alavanca.transform.SetPositionAndRotation(hit.point,original.transform.rotation);
            // Encosta a malha da base na superficie, sem enterrar o pivô nem deixar a alavanca flutuando.
            var baseMesh=alavanca.GetComponentsInChildren<MeshFilter>(true).Single(m=>m.name=="Switch_base");
            var tamanho=baseMesh.sharedMesh.bounds.size;
            var eixo=tamanho.x<tamanho.y && tamanho.x<tamanho.z?Vector3.right:tamanho.y<tamanho.z?Vector3.up:Vector3.forward;
            var normalBase=baseMesh.transform.TransformDirection(eixo);
            if(normalBase.y<0)normalBase=-normalBase;
            alavanca.transform.rotation=Quaternion.FromToRotation(normalBase,hit.normal)*alavanca.transform.rotation;
            float minimo=baseMesh.sharedMesh.vertices.Min(v=>Vector3.Dot(baseMesh.transform.TransformPoint(v)-hit.point,hit.normal));
            alavanca.transform.position+=hit.normal*(.015f-minimo);
            Physics.SyncTransforms();
            float margem=NaCena<ArmadilhaMola>(cena).Min(t=>Vector3.Distance(t.transform.position,alavanca.transform.position));
            Exigir(margem>2f,"Alavanca separada da trap mais proxima: "+margem.ToString("F2")+" m");
            Exigir(alavanca.gerenciadorCentral!=null && alavanca.haste!=null && alavanca.isActiveAndEnabled,"Haste, luz 2 e gerenciador do puzzle preservados");
            Relatorio.AppendLine("Alavanca ajustada: "+alavanca.transform.position.ToString("F4")+" normal="+hit.normal.ToString("F4"));
            EditorUtility.SetDirty(alavanca.transform);EditorSceneManager.MarkSceneDirty(cena);EditorSceneManager.SaveScene(cena);
            Selection.activeGameObject=alavanca.gameObject;
            if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.LookAt(alavanca.transform.position,Quaternion.Euler(25,-30,0),8);
            Renderizar("alavanca-traps",alavanca.transform.position+new Vector3(5,4,6),alavanca.transform.position+Vector3.up*.5f,0);
            EditorSceneManager.SaveScene(cena);
            File.WriteAllText("Logs/juncao/alavanca-ajustada.txt",Relatorio.ToString());
        }
        finally{EditorSceneManager.ClosePreviewScene(origem);}
    }

    [MenuItem("Tools/Mapa/Juncao/Combinar terrenos das duas fases")]
    public static void UnirTerrenos()
    {
        try
        {
            var o=AssetDatabase.LoadAssetAtPath<TerrainData>(BaseTerreno);
            var a=AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/Terrain/Demo_Day_ExpandedCoast.asset");
            var b=AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/Terrain/Demo_Day_ExpandedCoast_Gorila.asset");
            var destino=AssetDatabase.LoadAssetAtPath<TerrainData>(Terreno);
            Exigir(o!=null && a!=null && b!=null && destino!=null,"Terrenos de origem e destino disponiveis");
            int n=o.heightmapResolution;
            Exigir(n==a.heightmapResolution && n==b.heightmapResolution && o.size==a.size && o.size==b.size,"Grade comum preservada");
            var oh=o.GetHeights(0,0,n,n);var ah=a.GetHeights(0,0,n,n);var bh=b.GetHeights(0,0,n,n);
            var resultado=(float[,])ah.Clone();int aplicados=0;
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                if(bh[y,x]==oh[y,x])continue;
                if(ah[y,x]!=oh[y,x] && ah[y,x]!=bh[y,x])throw new Exception($"Alturas conflitantes em {x},{y}");
                resultado[y,x]=bh[y,x];aplicados++;
            }
            var oa=o.GetAlphamaps(0,0,o.alphamapWidth,o.alphamapHeight);
            var aa=a.GetAlphamaps(0,0,a.alphamapWidth,a.alphamapHeight);
            var ba=b.GetAlphamaps(0,0,b.alphamapWidth,b.alphamapHeight);
            Exigir(o.terrainLayers.SequenceEqual(a.terrainLayers) && o.terrainLayers.SequenceEqual(b.terrainLayers),"Mesmas camadas de pintura");
            Exigir(oa.Cast<float>().SequenceEqual(aa.Cast<float>()) && oa.Cast<float>().SequenceEqual(ba.Cast<float>()),"Pintura do terreno identica nas duas versoes");
            Exigir(o.detailPrototypes.Length==0 && a.detailPrototypes.Length==0 && b.detailPrototypes.Length==0 && o.treeInstanceCount==0 && a.treeInstanceCount==0 && b.treeInstanceCount==0,"Vegetacao autorada em objetos da cena, sem dados de terrain perdidos");
            int h=o.holesResolution;var holesO=o.GetHoles(0,0,h,h);var holesA=a.GetHoles(0,0,h,h);var holesB=b.GetHoles(0,0,h,h);
            for(int y=0;y<h;y++)for(int x=0;x<h;x++)if(holesB[y,x]!=holesO[y,x])holesA[y,x]=holesB[y,x];
            EditorUtility.CopySerialized(a,destino);
            destino.name="Demo_Day_Juncao";destino.SetHeights(0,0,resultado);destino.SetHoles(0,0,holesA);
            EditorUtility.SetDirty(destino);AssetDatabase.SaveAssetIfDirty(destino);
            var final=destino.GetHeights(0,0,n,n);
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)if(Mathf.Abs(final[y,x]-resultado[y,x])>0.000001f)throw new Exception("Altura perdida ao salvar terreno");
            Exigir(true,$"Relevo combinado: {aplicados} amostras do gorila; todas as alteracoes da primeira fase preservadas");
            File.WriteAllText("Logs/juncao/terreno-unificado.txt",Relatorio.ToString());
            for(int i=0;i<SceneManager.sceneCount;i++)Exigir(!SceneManager.GetSceneAt(i).isDirty,"Cenas salvas antes de recompor a juncao");
            EditorSceneManager.OpenScene(Cena);AjustarAlavanca();Validar();
        }
        catch(Exception e){Falhar(e);}
    }

    static void Exigir(bool ok, string mensagem)
    {
        Relatorio.AppendLine((ok ? "PASS: " : "FAIL: ") + mensagem);
        if (!ok) throw new InvalidOperationException(mensagem);
    }

    public static void InspecionarTerrenos()
    {
        try
        {
            Directory.CreateDirectory("Logs/juncao");
            AssetDatabase.Refresh();
            var o = AssetDatabase.LoadAssetAtPath<TerrainData>(BaseTerreno);
            var a = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/Terrain/Demo_Day_ExpandedCoast.asset");
            var b = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/Terrain/Demo_Day_ExpandedCoast_Gorila.asset");
            Exigir(o != null && a != null && b != null, "Tres terrenos carregados");
            Exigir(o.heightmapResolution == a.heightmapResolution && o.heightmapResolution == b.heightmapResolution && o.size == a.size && o.size == b.size, "Resolucao e dimensoes iguais");
            var cena = SceneManager.GetSceneByPath(Cena);
            if(!cena.IsValid() || !cena.isLoaded) cena=EditorSceneManager.OpenScene(Cena,OpenSceneMode.Additive);
            var terrain = NaCena<Terrain>(cena).Single();
            Relatorio.AppendLine("Terrain origin=" + terrain.transform.position + " size=" + a.size);
            int n = o.heightmapResolution;
            Comparar(o.GetHeights(0,0,n,n), a.GetHeights(0,0,n,n), b.GetHeights(0,0,n,n), "alturas");
            Exigir(o.terrainLayers.SequenceEqual(a.terrainLayers) && o.terrainLayers.SequenceEqual(b.terrainLayers), "Camadas de textura identicas");
            Relatorio.AppendLine($"Alphamap {a.alphamapWidth}x{a.alphamapHeight} layers={a.alphamapLayers}");
            var oa = o.GetAlphamaps(0,0,o.alphamapWidth,o.alphamapHeight);
            var aa = a.GetAlphamaps(0,0,a.alphamapWidth,a.alphamapHeight);
            var ba = b.GetAlphamaps(0,0,b.alphamapWidth,b.alphamapHeight);
            for (int l=0; l<a.alphamapLayers; l++)
            {
                var x=new float[a.alphamapHeight,a.alphamapWidth]; var y=(float[,])x.Clone(); var z=(float[,])x.Clone();
                for(int row=0;row<a.alphamapHeight;row++) for(int col=0;col<a.alphamapWidth;col++) {x[row,col]=oa[row,col,l];y[row,col]=aa[row,col,l];z[row,col]=ba[row,col,l];}
                Comparar(x,y,z,"textura "+l);
            }
            Relatorio.AppendLine($"Details prototypes: {o.detailPrototypes.Length}/{a.detailPrototypes.Length}/{b.detailPrototypes.Length}; trees: {o.treeInstanceCount}/{a.treeInstanceCount}/{b.treeInstanceCount}");
            File.WriteAllText("Logs/juncao/terrenos.txt",Relatorio.ToString());
            if(Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch(Exception e) { Falhar(e); }
    }

    static void Comparar(float[,] o,float[,] a,float[,] b,string nome)
    {
        int ca=0,cb=0,conflito=0,minX=int.MaxValue,minY=int.MaxValue,maxX=0,maxY=0;
        float maior=0;
        for(int y=0;y<o.GetLength(0);y++) for(int x=0;x<o.GetLength(1);x++)
        {
            bool da=Mathf.Abs(a[y,x]-o[y,x])>0.000001f, db=Mathf.Abs(b[y,x]-o[y,x])>0.000001f;
            if(da)ca++;if(db){cb++;minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}
            if(da&&db&&Mathf.Abs(a[y,x]-b[y,x])>0.000001f){conflito++;maior=Mathf.Max(maior,Mathf.Abs(a[y,x]-b[y,x]));}
        }
        Relatorio.AppendLine($"{nome}: primeira={ca}, gorila={cb}, conflitos={conflito}, deltaMax={maior}, regiaoGorila=({minX},{minY})-({maxX},{maxY})");
    }

    static T[] NaCena<T>(Scene cena) where T:Component => cena.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).ToArray();

    [MenuItem("Tools/Mapa/Juncao/Validar cena unificada")]
    public static void Validar()
    {
        try
        {
            Directory.CreateDirectory("Logs/juncao");
            for(int i=0;i<SceneManager.sceneCount;i++)
                Exigir(!SceneManager.GetSceneAt(i).isDirty,"Cena aberta sem alteracoes nao salvas: "+SceneManager.GetSceneAt(i).path);
            var cena = EditorSceneManager.OpenScene(Cena);
            var player = NaCena<InventarioEndy>(cena);
            Exigir(player.Length == 1, "Um unico Endy");
            Exigir(NaCena<Camera>(cena).Count(c=>c.CompareTag("MainCamera")) == 1, "Uma unica camera principal");
            Exigir(NaCena<UnityEngine.EventSystems.EventSystem>(cena).Length == 1, "Um unico EventSystem");
            Exigir(NaCena<Terrain>(cena).Length == 1, "Um unico terreno");
            var terreno = NaCena<Terrain>(cena).Single();
            Exigir(AssetDatabase.GetAssetPath(terreno.terrainData)==Terreno && terreno.GetComponent<TerrainCollider>().terrainData==terreno.terrainData, "Terreno e colisao usam o asset unificado");
            Exigir(NaCena<AlavancaTriangulo>(cena).Length==3 && NaCena<InteracaoBau>(cena).Length==5, "Tres alavancas e cinco baus da primeira fase");
            Exigir(NaCena<CaranguejoChefe>(cena).Length==1 && NaCena<BauDiario>(cena).Length==1, "Chefe caranguejo e bau do diario preservados");
            Exigir(NaCena<GorilaGuardiao>(cena).Length==1 && NaCena<CaldeiraoSopa>(cena).Length==1, "Gorila e caldeirao preservados");
            Exigir(NaCena<BananaSopa>(cena).Length==3, "Tres bananas da receita preservadas");
            Exigir(NaCena<ArmadilhaMola>(cena).Length>0 && NaCena<AltarRuinas>(cena).Length>0, "Armadilhas e altares presentes");
            Exigir(player[0].encaixeMao!=null, "Mao do jogador ligada ao inventario");
            foreach(var raiz in cena.GetRootGameObjects()) foreach(var t in raiz.GetComponentsInChildren<Transform>(true))
                if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0) throw new Exception("Script ausente em "+Caminho(t));
            Exigir(true,"Nenhum componente com script ausente");
            foreach(var component in NaCena<MonoBehaviour>(cena))
            {
                var so=new SerializedObject(component); var prop=so.GetIterator();
                while(prop.Next(true)) if(prop.propertyType==SerializedPropertyType.ObjectReference)
                {
                    var value=prop.objectReferenceValue;
                    if(value==null && !prop.objectReferenceEntityIdValue.Equals(default(EntityId))) throw new Exception("Referencia quebrada: "+Caminho(component.transform)+"."+prop.propertyPath);
                    if(value is Component c && c.gameObject.scene.IsValid() && c.gameObject.scene!=cena) throw new Exception("Referencia externa de cena: "+prop.propertyPath);
                    if(value is GameObject g && g.scene.IsValid() && g.scene!=cena) throw new Exception("Referencia externa de cena: "+prop.propertyPath);
                }
            }
            Exigir(true,"Referencias de scripts resolvidas na propria cena");
            foreach(var trap in NaCena<ArmadilhaMola>(cena)) Exigir(trap.pontoDeQueda!=null, "Destino da armadilha: "+Caminho(trap.transform));
            var sopa=NaCena<CaldeiraoSopa>(cena).Single();
            Exigir(sopa.jogador==player[0] && sopa.bananasDaReceita.Length==3 && sopa.bananasDaReceita.All(x=>x!=null) && sopa.gorila!=null, "Receita, gorila e jogador conectados");
            var alavancaRampa=NaCena<AlavancaTriangulo>(cena).Single(l=>l.indiceDaLuz==2);
            var rampa=NaCena<Transform>(cena).Single(t=>t.name==ArmadilhasRampaSetup.NomePedra).GetComponent<MeshCollider>();
            Physics.SyncTransforms();
            Exigir(rampa.Raycast(new Ray(alavancaRampa.transform.position+Vector3.up*40,Vector3.down),out var apoioAlavanca,80),"Alavanca da fase 1 permanece na pedra das traps");
            var meshBase=alavancaRampa.GetComponentsInChildren<MeshFilter>(true).Single(m=>m.name=="Switch_base");
            float folgaBase=meshBase.sharedMesh.vertices.Min(v=>Vector3.Dot(meshBase.transform.TransformPoint(v)-apoioAlavanca.point,apoioAlavanca.normal));
            Exigir(folgaBase>=-.02f && folgaBase<.08f,"Base da alavanca apoiada sobre a pedra, sem ficar enterrada");
            Relatorio.AppendLine("GameObjects="+NaCena<Transform>(cena).Length+" traps="+NaCena<ArmadilhaMola>(cena).Length+" altares="+NaCena<AltarRuinas>(cena).Length);
            Physics.SyncTransforms();
            Renderizar("mapa",new Vector3(-5,330,100),new Vector3(-5,0,100),205);
            Renderizar("caverna",new Vector3(12,12,119),new Vector3(18,9,136),0);
            Renderizar("armadilhas",new Vector3(-85,40,176),new Vector3(-57,8,202),0);
            Renderizar("altares",new Vector3(-24,32,81),new Vector3(27,4,102),0);
            File.WriteAllText("Logs/juncao/validacao.txt",Relatorio.ToString());
            // Somente as cameras temporarias alteraram o estado em memoria; recarrega a cena salva.
            EditorSceneManager.OpenScene(Cena);
            if(Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch(Exception e) { Falhar(e); }
    }
    static string Caminho(Transform t)=>t.parent==null?t.name:Caminho(t.parent)+"/"+t.name;
    static void Renderizar(string nome,Vector3 pos,Vector3 alvo,float ortho)
    {
        var go=new GameObject("Camera de validacao");var cam=go.AddComponent<Camera>();
        go.transform.position=pos;go.transform.LookAt(alvo);cam.farClipPlane=1200;cam.nearClipPlane=.1f;
        cam.orthographic=ortho>0;cam.orthographicSize=ortho;cam.fieldOfView=65;
        var rt=new RenderTexture(1400,1000,24);var previous=RenderTexture.active;cam.targetTexture=rt;
        try { cam.Render();RenderTexture.active=rt;var image=new Texture2D(1400,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1400,1000),0,0);image.Apply();File.WriteAllBytes("Logs/juncao/"+nome+".png",image.EncodeToPNG());Object.DestroyImmediate(image); }
        finally {RenderTexture.active=previous;cam.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}
    }
    static void Falhar(Exception e)
    {Directory.CreateDirectory("Logs/juncao");File.WriteAllText("Logs/juncao/falha.txt",Relatorio+"\n"+e);Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);else throw e;}
}

public class JuncaoFasesPlayProbe:MonoBehaviour
{
    readonly StringBuilder report=new StringBuilder();
    readonly System.Collections.Generic.List<string> erros=new System.Collections.Generic.List<string>();
    void Awake(){Application.logMessageReceived+=RegistrarErro;StartCoroutine(SessionState.GetBool("Juncao.ValidarAlavanca",false)?TestarAlavanca():Executar());}
    void RegistrarErro(string texto,string pilha,LogType tipo)
    {
        if(tipo!=LogType.Error && tipo!=LogType.Exception && tipo!=LogType.Assert)return;
        if(pilha.Contains("CaveGroundDoctor.Inspect") || pilha.Contains("PlayerSceneDoctor.Inspect") || pilha.Contains("RepairMiningURP.Run"))
        {report.AppendLine("AVISO: ferramenta antiga do editor: "+texto);return;}
        erros.Add(texto);
    }
    System.Collections.IEnumerator TestarAlavanca()
    {
        SessionState.SetBool("Juncao.ValidarAlavanca",false);
        yield return new WaitForSecondsRealtime(2);
        AlavancaTriangulo lever=null;
        try
        {
            var scene=SceneManager.GetActiveScene();
            var roots=scene.GetRootGameObjects();
            var player=roots.SelectMany(g=>g.GetComponentsInChildren<InventarioEndy>(true)).Single();
            lever=roots.SelectMany(g=>g.GetComponentsInChildren<AlavancaTriangulo>(true)).Single(l=>l.indiceDaLuz==2);
            var collider=player.GetComponent<CharacterController>();
            var movement=player.GetComponent<StarterAssets.ThirdPersonController>();movement.enabled=false;
            var rampa=roots.SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Single(t=>t.name==ArmadilhasRampaSetup.NomePedra).GetComponent<MeshCollider>();
            var alvo=lever.transform.position+new Vector3(-.5f,0,.6f);
            Exigir(rampa.Raycast(new Ray(alvo+Vector3.up*20,Vector3.down),out var hit,40),"Existe chao acessivel ao lado da alavanca");
            collider.enabled=false;player.transform.position=hit.point+Vector3.up*.04f;collider.enabled=true;
            Physics.SyncTransforms();collider.Move(Vector3.down*.03f);
            var center=player.transform.TransformPoint(collider.center);float radius=collider.radius*Mathf.Max(player.transform.lossyScale.x,player.transform.lossyScale.z);
            float half=Mathf.Max(0,collider.height*player.transform.lossyScale.y*.5f-radius);
            var overlaps=Physics.OverlapCapsule(center+Vector3.up*half,center-Vector3.up*half,radius,~0,QueryTriggerInteraction.Collide);
            Exigir(overlaps.Contains(lever.GetComponent<Collider>()),"Capsula do Endy alcanca o gatilho da alavanca sobre a pedra");
            Exigir(!overlaps.Any(c=>c.GetComponentInParent<ArmadilhaMola>()!=null),"Interacao com a alavanca nao exige pisar em uma trap");
            lever.SendMessage("OnTriggerEnter",collider);
            Exigir(lever.MostrarDica,"Aviso de interacao fica disponivel");
            lever.SendMessage("PuxarAlavanca");
        }
        catch(Exception e){report.AppendLine("FAIL: "+e);}
        yield return new WaitForSecondsRealtime(1);
        try
        {
            Exigir(lever!=null && lever.Concluida,"Alavanca conclui animacao de acionamento");
            Exigir(lever.gerenciadorCentral.fogoDasTochas[2].activeSelf,"Alavanca acende a terceira tocha do puzzle da fase 1");
            Exigir(erros.Count==0,"Sem erros de jogo durante a interacao: "+string.Join(" | ",erros));
        }
        catch(Exception e){report.AppendLine("FAIL: "+e);}
        finally
        {
            Application.logMessageReceived-=RegistrarErro;
            File.WriteAllText("Logs/juncao/alavanca-play.txt",report.ToString());EditorApplication.ExitPlaymode();
        }
    }
    void Exigir(bool ok,string texto){report.AppendLine((ok?"PASS: ":"FAIL: ")+texto);if(!ok)throw new Exception(texto);}
    System.Collections.IEnumerator Executar()
    {
        yield return new WaitForSecondsRealtime(2);
        try
        {
            var scene=SceneManager.GetActiveScene();
            var roots=scene.GetRootGameObjects();
            var player=roots.SelectMany(g=>g.GetComponentsInChildren<InventarioEndy>(true)).Single();
            var sopa=roots.SelectMany(g=>g.GetComponentsInChildren<CaldeiraoSopa>(true)).Single();
            var traps=roots.SelectMany(g=>g.GetComponentsInChildren<ArmadilhaMola>(true)).ToArray();
            Exigir(player.GetComponent<VidaEndy>().VidaAtual>0,"Endy inicia vivo");
            Exigir(player.GetComponent<StarterAssets.ThirdPersonController>().enabled,"Controle de movimento ativo");
            Exigir(sopa.ReceitaValida(),"Receita da sopa e animacoes do gorila validas em execucao");
            Exigir(traps.Length==22 && traps.All(t=>t.isActiveAndEnabled && t.pontoDeQueda!=null && t.animador!=null),"22 traps ativas com destino e animador");
            Exigir(Object.FindAnyObjectByType<LimitesPrimeiraFase>()!=null,"Bloqueios da primeira fase instalados na juncao");
            Exigir(player.GetComponent<AutosaveEndy>()!=null,"Autosave reconhece a cena unificada (desativado somente durante o teste)");
            Exigir(player.GetComponent<DiarioEndy>()!=null,"Diario da primeira fase presente");
            Physics.SyncTransforms();
            foreach(var t in sopa.gorila.percurso)
                Exigir(Physics.RaycastAll(t.position+Vector3.up*0.7f,Vector3.down,2f,~0,QueryTriggerInteraction.Ignore).Any(h=>h.normal.y>.5f),"Chao sob percurso do gorila: "+t.name);
            Exigir(erros.Count==0,"Sem novos erros de execucao: "+string.Join(" | ",erros));
        }
        catch(Exception e){report.AppendLine("FAIL: "+e);}
        finally
        {
            Application.logMessageReceived-=RegistrarErro;
            File.WriteAllText("Logs/juncao/play.txt",report.ToString());
            EditorApplication.ExitPlaymode();
        }
    }
}
