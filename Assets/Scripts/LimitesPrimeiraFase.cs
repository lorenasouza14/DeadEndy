using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// A floresta usa os cubos-guia marcados na cena; os baus mantem seu contorno independente.
[DefaultExecutionOrder(-50)]
public class LimitesPrimeiraFase : MonoBehaviour
{
    public GerenciadorPuzzleTriangulo tochas;
    public InventarioEndy endy;
    public Vector3 pontoInicial = new Vector3(7.61f, 0.54f, -34.98f);
    // Entrada norte cerca de 12 m depois da alavanca (-50.57, 202.29), antes da vista da arena.
    public Vector2[] areaBaus = { new Vector2(18,125), new Vector2(110,125), new Vector2(110,250),
        new Vector2(-38,250), new Vector2(-38,194), new Vector2(18,174) };
    readonly List<BoxCollider> murosFloresta = new List<BoxCollider>();
    readonly List<BoxCollider> murosBaus = new List<BoxCollider>();
    DiarioEndy diario;
    GameObject hud;
    TMP_Text aviso;
    public bool FlorestaLiberada => endy != null && endy.temDiario && diario != null && diario.FoiLido;
    public bool BausLiberados => tochas != null && tochas.Concluido;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Registrar() { SceneManager.sceneLoaded -= AoCarregar; SceneManager.sceneLoaded += AoCarregar; }
    static void AoCarregar(Scene cena, LoadSceneMode modo) { Garantir(cena); }
    public static LimitesPrimeiraFase Garantir(Scene cena)
    {
        if (!cena.IsValid() || !cena.isLoaded || cena.path != ProgressoJogo.CenaJogo) return null;
        foreach (var raiz in cena.GetRootGameObjects())
        {
            var existente = raiz.GetComponentInChildren<LimitesPrimeiraFase>(true);
            if (existente != null) return existente;
        }
        var go = new GameObject("Limites da primeira fase");
        SceneManager.MoveGameObjectToScene(go, cena);
        return go.AddComponent<LimitesPrimeiraFase>();
    }

    void Awake()
    {
        foreach (var raiz in gameObject.scene.GetRootGameObjects())
        {
            if (endy == null) endy = raiz.GetComponentInChildren<InventarioEndy>(true);
            if (tochas == null) tochas = raiz.GetComponentInChildren<GerenciadorPuzzleTriangulo>(true);
        }
        if (endy != null) diario = endy.GetComponent<DiarioEndy>();
        foreach (var raiz in gameObject.scene.GetRootGameObjects())
            foreach (var guia in raiz.GetComponentsInChildren<BloqueioFloresta>(true))
                if (guia.isActiveAndEnabled) murosFloresta.Add(guia.Preparar());
        Construir(areaBaus, "Baus - acender tochas", murosBaus);
        AtualizarBloqueios();
    }

    void Construir(Vector2[] contorno, string nome, List<BoxCollider> destino)
    {
        if (contorno == null || contorno.Length < 3) return;
        for (int i = 0; i < contorno.Length; i++)
        {
            Vector2 a = contorno[i], b = contorno[(i + 1) % contorno.Length];
            var go = new GameObject(nome + " " + (i + 1));
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3((a.x+b.x)*0.5f, 25, (a.y+b.y)*0.5f);
            go.transform.rotation = Quaternion.LookRotation(new Vector3(b.x-a.x, 0, b.y-a.y));
            var col = go.AddComponent<BoxCollider>();
            // Atravessa o solo e ultrapassa o topo dos morros; emendas sobrepostas impedem atalhos.
            col.size = new Vector3(0.8f, 150, Vector2.Distance(a,b) + 0.8f);
            destino.Add(col);
        }
    }

    public void AtualizarBloqueios()
    {
        foreach (var col in murosFloresta) if (col != null) col.enabled = !FlorestaLiberada;
        foreach (var col in murosBaus) col.enabled = !BausLiberados;
    }

    public static bool Contem(Vector2[] contorno, Vector3 ponto)
    {
        if (contorno == null || contorno.Length < 3) return false;
        bool dentro = false;
        for (int i = 0, j = contorno.Length - 1; i < contorno.Length; j = i++)
        {
            Vector2 a = contorno[i], b = contorno[j];
            if ((a.y > ponto.z) != (b.y > ponto.z) && ponto.x < (b.x-a.x)*(ponto.z-a.y)/(b.y-a.y)+a.x)
                dentro = !dentro;
        }
        return dentro;
    }

    public bool PosicaoBloqueada(Vector3 posicao)
    {
        if (!BausLiberados && Contem(areaBaus, posicao)) return true;
        if (!FlorestaLiberada)
            foreach (var col in murosFloresta)
                if (col != null && col.gameObject.activeInHierarchy &&
                    (col.ClosestPoint(posicao) - posicao).sqrMagnitude < 0.0001f) return true;
        return false;
    }

    // Saves antigos podem ter sido feitos além dos limites que agora existem.
    public bool CorrigirCheckpointBloqueado()
    {
        AtualizarBloqueios();
        if (endy == null || !PosicaoBloqueada(endy.transform.position)) return false;
        var cc = endy.GetComponent<CharacterController>();
        bool ativo = cc != null && cc.enabled;
        if (cc != null) cc.enabled = false;
        endy.transform.position = pontoInicial;
        if (cc != null) cc.enabled = ativo;
        Physics.SyncTransforms();
        return true;
    }

    void Update()
    {
        AtualizarBloqueios();
        if (endy == null || !endy.isActiveAndEnabled || MenuManager.EstaAberto || Time.timeScale == 0)
        { if (hud != null) hud.SetActive(false); return; }
        var vida = endy.GetComponent<VidaEndy>();
        if (vida != null && vida.EstaMorto) { if (hud != null) hud.SetActive(false); return; }
        Vector3 ponto = endy.transform.position + Vector3.up;
        float menor = 3f;
        string mensagem = null;
        foreach (var col in murosBaus)
            if (col != null && col.enabled && col.gameObject.activeInHierarchy && Vector3.Distance(ponto, col.ClosestPoint(ponto)) < menor)
            { menor = Vector3.Distance(ponto, col.ClosestPoint(ponto)); mensagem = "Preciso acender as três tochas antes de seguir para os baús."; }
        foreach (var col in murosFloresta)
            if (col != null && col.enabled && col.gameObject.activeInHierarchy && Vector3.Distance(ponto, col.ClosestPoint(ponto)) < menor)
            { menor = Vector3.Distance(ponto, col.ClosestPoint(ponto)); mensagem = endy.temDiario ?
                "Preciso ler o diário antes de entrar na floresta.  [J] Ler diário" :
                "Preciso encontrar e ler o diário antes de entrar na floresta."; }
        if (mensagem != null && hud == null) CriarAviso();
        if (hud != null) { hud.SetActive(mensagem != null); if (mensagem != null) aviso.text = mensagem; }
    }

    void CriarAviso()
    {
        hud = new GameObject("Aviso de caminho bloqueado", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        hud.transform.SetParent(transform, false);
        hud.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        hud.GetComponent<Canvas>().sortingOrder = 80;
        var scaler = hud.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600,900);
        var go = new GameObject("Mensagem", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(hud.transform, false);
        aviso = go.GetComponent<TextMeshProUGUI>();
        var fonte = endy.GetComponent<IndicadorItemEndy>();
        if (fonte != null && fonte.fonte != null) aviso.font = fonte.fonte;
        aviso.fontSize = 28; aviso.alignment = TextAlignmentOptions.Center; aviso.raycastTarget = false;
        aviso.color = new Color(1,0.91f,0.76f); aviso.outlineWidth = 0.22f; aviso.outlineColor = new Color(0.15f,0.08f,0.03f);
        var rect = aviso.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(0.5f,0);
        rect.pivot = new Vector2(0.5f,0); rect.anchoredPosition = new Vector2(0,115); rect.sizeDelta = new Vector2(1150,90);
    }

    void OnDisable() { if (hud != null) hud.SetActive(false); }
    void OnDrawGizmosSelected()
    {
        Desenhar(areaBaus, Color.yellow);
    }
    static void Desenhar(Vector2[] pontos, Color cor)
    {
        if (pontos == null) return;
        Gizmos.color = cor;
        for (int i=0; i<pontos.Length; i++)
        { var a=pontos[i]; var b=pontos[(i+1)%pontos.Length]; Gizmos.DrawLine(new Vector3(a.x,5,a.y),new Vector3(b.x,5,b.y)); }
    }
}
