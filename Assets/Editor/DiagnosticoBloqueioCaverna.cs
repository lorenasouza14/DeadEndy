using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Pode ser executado com o jogo pausado no ponto do bloqueio, sem mover Endy.
public static class DiagnosticoBloqueioCaverna
{
    [MenuItem("Tools/Gorila/Sopa/Inspecionar bloqueio na posicao do Endy %#&F6")]
    public static void Inspecionar()
    {
        var inventario = Object.FindAnyObjectByType<InventarioEndy>();
        if (inventario == null) { Debug.LogWarning("Endy precisa estar ativo na cena."); return; }
        var cc = inventario.GetComponent<CharacterController>();
        var texto = new StringBuilder();
        texto.AppendLine($"Play={EditorApplication.isPlaying}; pausado={EditorApplication.isPaused}; Endy={cc.transform.position:F3}; controller={cc.enabled}; detectCollisions={cc.detectCollisions}");
        foreach (var area in Object.FindObjectsByType<AreaCaverna>(FindObjectsInactive.Include))
        {
            var caixa = area.GetComponent<BoxCollider>();
            bool dentro = Physics.ComputePenetration(caixa, caixa.transform.position, caixa.transform.rotation,
                cc, cc.transform.position, cc.transform.rotation, out _, out _);
            texto.AppendLine($"Area: ativa={area.isActiveAndEnabled}; caixa={caixa.enabled}; jogador correto={area.jogador == cc}; dentro={dentro}; montanha ativa={area.colliderMontanha?.enabled}; volume={caixa.bounds}");
        }
        foreach (var g in Object.FindObjectsByType<GorilaSopa>(FindObjectsInactive.Include))
        {
            texto.AppendLine($"Gorila: etapa={g.Etapa}; posicao={g.transform.position:F3}");
            foreach (var c in g.bloqueiosDaCaverna)
                if (c != null) texto.AppendLine($"Barreira: {SopaCenaSetup.Caminho(c.transform)}; ativa={c.enabled}");
        }
        Physics.SyncTransforms();
        Vector3 escala = cc.transform.lossyScale;
        float raio = cc.radius * Mathf.Max(Mathf.Abs(escala.x), Mathf.Abs(escala.z));
        float meiaLinha = Mathf.Max(0, cc.height * Mathf.Abs(escala.y) * 0.5f - raio);
        Vector3 centro = cc.transform.TransformPoint(cc.center);
        Vector3 ponta = cc.transform.up * meiaLinha;
        foreach (var hit in Physics.CapsuleCastAll(centro + ponta, centro - ponta, raio * 0.95f,
            cc.transform.forward, 1.5f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            if (hit.collider != cc) texto.AppendLine($"A FRENTE: {SopaCenaSetup.Caminho(hit.transform)}; distancia={hit.distance:F3}; ponto={hit.point:F3}; normal={hit.normal:F3}");
        foreach (var col in Physics.OverlapCapsule(centro + ponta, centro - ponta, raio + 0.1f, ~0, QueryTriggerInteraction.Ignore))
            if (col != cc) texto.AppendLine($"CONTATO: {SopaCenaSetup.Caminho(col.transform)}; tipo={col.GetType().Name}");
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/bloqueio-caverna-posicao-atual.txt", texto.ToString());
        Debug.Log(texto.ToString());
    }
}
