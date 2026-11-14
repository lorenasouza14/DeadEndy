using UnityEngine;

public class BolhaSabaoKraken : MonoBehaviour
{
    public AtaqueSabaoKraken dono;
    public VidaJogadorKraken jogador;
    public CharacterController capsula;
    public float raio = 4f, velocidade = 7, duracao = 28;
    public float impulsoVertical = 6f, gravidade = 2f;
    public float tempoAtePouso = 1f, desvioLateral;
    Vector3 direcao, origem;
    float inicio, fase, tempoAnterior;
    bool estourou, atingiuJogador;
    public void Preparar(Vector3 rumo, int indice)
    {
        direcao = rumo.normalized; origem = transform.position;
        inicio = Time.time; fase = indice * 1.7f; tempoAnterior = 0;
    }
    void Update()
    {
        if (estourou) return;
        if (!dono || !dono.PodeManterBolhas || !jogador || jogador.Morto || Time.time - inicio >= duracao)
        { Estourar(); return; }
        float t = Time.time - inicio;
        Vector3 destino = PosicaoNoTempo(t);
        // Subpassos evitam atravessar o jogador ou o piso em quadros lentos.
        float percurso = Vector3.Distance(transform.position, destino);
        int passos = Mathf.Max(1, Mathf.CeilToInt(percurso / Mathf.Max(.1f, raio * .25f)));
        for (int i = 1; i <= passos; i++)
        {
            float tempo = Mathf.Lerp(tempoAnterior, t, i / (float)passos);
            transform.position = PosicaoNoTempo(tempo);
            float raioAtual = raio * (1 + Mathf.Sin(tempo * 2 + fase) * .035f);
            transform.localScale = Vector3.one * (raioAtual * 2);
            if (capsula && capsula.enabled && (capsula.ClosestPoint(transform.position) - transform.position).sqrMagnitude <= raioAtual * raioAtual)
            { AtingirJogador(); return; }
            if (impulsoVertical - gravidade * tempo <= 0 && TocouChao(transform.position, raioAtual)) { Estourar(); return; }
        }
        tempoAnterior = t;
    }
    Vector3 PosicaoNoTempo(float t) => origem + direcao * (velocidade * t)
        + Vector3.up * (impulsoVertical * t - .5f * gravidade * t * t)
        + Vector3.Cross(Vector3.up, direcao) * (Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / Mathf.Max(.1f, tempoAtePouso)))
            * Mathf.Sin(t * 1.3f + fase) * desvioLateral);

    bool TocouChao(Vector3 centro, float raioAtual)
    {
        if (dono.TemChaoDefinido) return dono.Superficies.Tocou(centro, raioAtual);
        // Busca superficies sob a esfera; confirma a normal real para ignorar
        // laterais e tetos, inclusive nas quinas de MeshColliders.
        float elevacao = raioAtual * 2 + .1f;
        foreach (var contato in Physics.SphereCastAll(centro + Vector3.up * elevacao,
            raioAtual, Vector3.down, elevacao + .02f, dono.camadasChao, QueryTriggerInteraction.Ignore))
        {
            var colisor = contato.collider;
            if (contato.distance <= 0 || contato.point.y > centro.y + .02f) continue;
            if (colisor.GetComponentInParent<VidaJogadorKraken>()
                || colisor.transform.IsChildOf(dono.transform)
                || (dono.batalha && colisor.transform.IsChildOf(dono.batalha.transform))) continue;
            if (colisor.Raycast(new Ray(contato.point + Vector3.up * .1f, Vector3.down), out var superficie, .2f)
                && superficie.normal.y >= .5f) return true;
        }
        return false;
    }
    public void AtingirJogador()
    {
        if (estourou || atingiuJogador || !jogador || jogador.Morto || !dono || !dono.PodeManterBolhas) return;
        atingiuJogador = true;
        var camera = Camera.main;
        if (camera)
        {
            var efeito = camera.GetComponent<VisaoEnsaboada>();
            if (!efeito) efeito = camera.gameObject.AddComponent<VisaoEnsaboada>();
            efeito.Aplicar(jogador, dono.duracaoEmbacamento);
        }
        Estourar();
    }
    public void Estourar()
    {
        if (estourou) return;
        estourou = true; gameObject.SetActive(false); Destroy(gameObject);
    }
}
