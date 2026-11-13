using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class RotasVedacaoValidation
{
    public static void Validar(Scene cena,Vector2[] areaBaus,StringBuilder report)
    {
        var terreno=Object.FindObjectsByType<Terrain>().First(t=>t.gameObject.scene==cena);
        var guias=cena.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BloqueioFloresta>(true)).Select(g=>g.GetComponent<BoxCollider>()).ToArray();
        var aguas=Object.FindObjectsByType<MeshCollider>().Where(c=>c.gameObject.scene==cena&&c.sharedMesh!=null&&(c.name=="Ocean_Collider"||c.name=="Water_Plane_01")).ToArray();
        const int n=401;float passo=terreno.terrainData.size.x/(n-1);Vector3 origem=terreno.transform.position;
        var livres=new bool[n*n];var alturas=new float[n*n];var visitados=new bool[n*n];var mata=new bool[n*n];
        var inversas=guias.Select(g=>g.transform.worldToLocalMatrix).ToArray();
        var tamanhos=guias.Select(g=>g.size*.5f+new Vector3(.15f/Mathf.Abs(g.transform.lossyScale.x),0,.15f/Mathf.Abs(g.transform.lossyScale.z))).ToArray();
        for(int z=0;z<n;z++)for(int x=0;x<n;x++)
        {
            int k=z*n+x;var p=origem+new Vector3(x*passo,0,z*passo);p.y=terreno.SampleHeight(p)+origem.y;alturas[k]=p.y;
            bool terra=true;
            foreach(var agua in aguas)
            {
                var bounds=agua.sharedMesh.bounds;var centro=agua.transform.TransformPoint(bounds.center);var normal=agua.transform.up;
                float nivel=centro.y-(normal.x*(p.x-centro.x)+normal.z*(p.z-centro.z))/normal.y;
                var local=agua.transform.InverseTransformPoint(new Vector3(p.x,nivel,p.z));
                if(Mathf.Abs(local.x-bounds.center.x)<=bounds.extents.x&&Mathf.Abs(local.z-bounds.center.z)<=bounds.extents.z&&p.y<nivel+.5f)terra=false;
            }
            for(int i=0;i<guias.Length;i++)
            {
                var local=inversas[i].MultiplyPoint3x4(p)-guias[i].center;
                if(Mathf.Abs(local.x)<=tamanhos[i].x&&Mathf.Abs(local.z)<=tamanhos[i].z){mata[k]=true;break;}
            }
            livres[k]=terra&&!mata[k]&&!LimitesPrimeiraFase.Contem(areaBaus,p);
        }
        int Indice(Vector3 p)=>Mathf.Clamp(Mathf.RoundToInt((p.z-origem.z)/passo),0,n-1)*n+Mathf.Clamp(Mathf.RoundToInt((p.x-origem.x)/passo),0,n-1);
        var fila=new Queue<int>();int inicio=Indice(new Vector3(7.61f,0,-34.98f));fila.Enqueue(inicio);visitados[inicio]=true;
        var direcoes=new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down};
        while(fila.Count>0)
        {
            int k=fila.Dequeue();foreach(var delta in direcoes)
            {
                int x=k%n+delta.x,z=k/n+delta.y;if(x<0||z<0||x>=n||z>=n)continue;int outro=z*n+x;
                if(!visitados[outro]&&livres[outro]&&Mathf.Abs(alturas[outro]-alturas[k])<passo){visitados[outro]=true;fila.Enqueue(outro);}
            }
        }
        var mapa=new Texture2D(n,n,TextureFormat.RGB24,false);
        for(int z=0;z<n;z++)for(int x=0;x<n;x++){int k=z*n+x;mapa.SetPixel(x,z,visitados[k]?Color.yellow:mata[k]?Color.red:livres[k]?Color.gray:Color.blue);}
        mapa.Apply();File.WriteAllBytes("Logs/rotas-vedacao.png",mapa.EncodeToPNG());Object.DestroyImmediate(mapa);
        foreach(var alavanca in Object.FindObjectsByType<AlavancaTriangulo>(FindObjectsInactive.Include).Where(a=>a.gameObject.scene==cena))
        {
            int k=Indice(alavanca.transform.position);bool acesso=false;
            for(int dz=-2;dz<=2;dz++)for(int dx=-2;dx<=2;dx++){int x=k%n+dx,z=k/n+dz;if(x>=0&&z>=0&&x<n&&z<n&&visitados[z*n+x])acesso=true;}
            if(!acesso)throw new Exception("Rota pela praia interrompida ate "+alavanca.name);
            report.AppendLine("PASS: Caminho por terra preservado ate "+alavanca.name);
        }
        foreach(var p in new[]{new Vector3(30,0,80),new Vector3(-10,0,150),new Vector3(50,0,110)})
        {
            if(visitados[Indice(p)])throw new Exception("Brecha permite entrar na mata em "+p);
            report.AppendLine("PASS: Interior inacessivel sem diario: "+p);
        }
        var norte=Object.FindObjectsByType<AlavancaTriangulo>().Where(a=>a.gameObject.scene==cena).OrderByDescending(a=>a.transform.position.z).First();
        float distancia=Mathf.Abs(norte.transform.position.x+38);
        if(distancia>16||LimitesPrimeiraFase.Contem(areaBaus,norte.transform.position))throw new Exception("Portao longe ou alavanca dentro do bloqueio");
        report.AppendLine("PASS: Novo portao a "+distancia.ToString("F1")+" metros da alavanca norte");
        foreach(var bau in Object.FindObjectsByType<InteracaoBau>().Where(b=>b.gameObject.scene==cena))
        {
            Vector3 olho=new Vector3(-39,norte.transform.position.y+1.7f,norte.transform.position.z);
            Vector3 alvo=bau.transform.position+Vector3.up;
            bool encoberto=Physics.RaycastAll(olho,(alvo-olho).normalized,(alvo-olho).magnitude-2,~0,QueryTriggerInteraction.Ignore)
                .Any(h=>h.collider.GetComponent<BloqueioFloresta>()==null&&h.collider.GetComponentInParent<LimitesPrimeiraFase>()==null&&
                    h.collider.GetComponentInParent<InventarioEndy>()==null &&
                    (h.collider is TerrainCollider || (h.collider.TryGetComponent<Renderer>(out var visual)&&visual.enabled)));
            if(!encoberto)throw new Exception("Bau visivel da nova entrada: "+bau.transform.root.name);
        }
        report.AppendLine("PASS: Terreno/rochas encobrem os cinco baus vistos da entrada junto a alavanca");
    }
}
