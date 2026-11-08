using UnityEngine;

public partial class GorilaGuardiao
{
    public enum EstadoSonoGorila { Acordado, Adormecendo, Dormindo, Acordando }

    [Header("Desafio furtivo - sono")]
    [Tooltip("Ative para alternar entre guarda e repouso. Dormir nao desliga os colliders da caverna.")]
    public bool usarCicloSono;
    public Vector2 intervaloAcordado = new Vector2(20f, 35f);
    public Vector2 intervaloDormindo = new Vector2(8f, 12f);
    [Min(0.1f)] public float tempoAdormecer = 0.8f;
    [Tooltip("Transicao visivel para Idle. O gorila so pode iniciar o ataque depois deste tempo.")]
    [Min(0.1f)] public float tempoAcordar = 1.5f;
    public string estadoDormindo = "Base Layer.Rest";
    public int valorDormindo = 5;

    [Header("Audicao durante o sono")]
    [Tooltip("Opcional. Sem referencia, usa o centro de deteccao do gorila.")]
    public Transform centroAudicao;
    [Min(0.1f)] public float raioAudicao = 7f;
    [Tooltip("Velocidade horizontal real em m/s. Deve ser maior que a velocidade de caminhada lenta do Endy.")]
    [Min(0.1f)] public float velocidadeQueAcorda = 1.6f;
    [Tooltip("Pequena tolerancia para oscilacoes da capsula. Pulos e esquivas acordam imediatamente.")]
    [Min(0f)] public float toleranciaRuido = 0.1f;

    public EstadoSonoGorila EstadoSono { get; private set; }
    public bool EstaDormindo => usarCicloSono && sonoPreparado && EstadoSono == EstadoSonoGorila.Dormindo;
    public bool SonoImpedeAtaque => usarCicloSono && sonoPreparado && EstadoSono != EstadoSonoGorila.Acordado;
    private bool sonoPreparado;
    private int hashDormindo;
    private float tempoEstadoSono;
    private float ruidoAcumulado;
    private Vector3 CentroAudicao => centroAudicao != null ? centroAudicao.position : Centro;

    private void PrepararSono()
    {
        sonoPreparado = false;
        EstadoSono = EstadoSonoGorila.Acordado;
        if (!usarCicloSono || !configurado) return;
        hashDormindo = Animator.StringToHash(estadoDormindo);
        if (!animGorila.HasState(0, hashDormindo))
        {
            Debug.LogError("GorilaGuardiao: estado de sono ausente. Use Base Layer.Rest no Gorilla.controller original.", this);
            usarCicloSono = false;
            return;
        }
        sonoPreparado = true;
        tempoEstadoSono = SortearDuracao(intervaloAcordado);
        ruidoAcumulado = 0f;
    }

    private static float SortearDuracao(Vector2 intervalo)
    {
        float minimo = Mathf.Max(0.1f, Mathf.Min(intervalo.x, intervalo.y));
        float maximo = Mathf.Max(minimo, Mathf.Max(intervalo.x, intervalo.y));
        return Random.Range(minimo, maximo);
    }

    // Retorna true enquanto o ataque precisa ficar bloqueado.
    private bool AtualizarSono(float delta)
    {
        if (!usarCicloSono)
        {
            if (sonoPreparado) InterromperSono();
            return false;
        }
        if (!sonoPreparado) PrepararSono();
        if (!sonoPreparado || delta <= 0f) return SonoImpedeAtaque;

        if (EstadoSono == EstadoSonoGorila.Dormindo || EstadoSono == EstadoSonoGorila.Adormecendo)
        {
            if (OuviuEndy(delta))
            {
                Acordar();
                return true;
            }
        }
        tempoEstadoSono -= delta;
        switch (EstadoSono)
        {
            case EstadoSonoGorila.Acordado:
                // Nunca corta um golpe ja iniciado para colocar o gorila para dormir.
                if (tempoEstadoSono <= 0f && !EstaAtacando)
                {
                    EstadoSono = EstadoSonoGorila.Adormecendo;
                    tempoEstadoSono = Mathf.Max(0.1f, tempoAdormecer);
                    animGorila.SetInteger(hashParametro, valorDormindo);
                    animGorila.CrossFadeInFixedTime(hashDormindo, tempoEstadoSono, 0, 0f);
                    return true;
                }
                return false;
            case EstadoSonoGorila.Adormecendo:
                if (tempoEstadoSono <= 0f)
                {
                    EstadoSono = EstadoSonoGorila.Dormindo;
                    tempoEstadoSono = SortearDuracao(intervaloDormindo);
                }
                return true;
            case EstadoSonoGorila.Dormindo:
                if (tempoEstadoSono <= 0f) Acordar();
                return true;
            case EstadoSonoGorila.Acordando:
                if (tempoEstadoSono <= 0f)
                {
                    EstadoSono = EstadoSonoGorila.Acordado;
                    tempoEstadoSono = SortearDuracao(intervaloAcordado);
                    // O atraso de despertar ja acabou; o golpe ainda respeita seu momento de impacto.
                    proximoAtaque = Mathf.Max(proximoAtaque, Time.time);
                }
                return true;
        }
        return false;
    }

    private bool OuviuEndy(float delta)
    {
        if (!AlvoDisponivel() || movimentoEndy.EstaArremessado ||
            (capsulaEndy.bounds.center - CentroAudicao).sqrMagnitude > raioAudicao * raioAudicao)
        {
            ruidoAcumulado = 0f;
            return false;
        }
        return AcumularRuido(movimentoEndy.VelocidadeHorizontalAtual,
            movimentoEndy.IsDodging || movimentoEndy.EstaSubindoNoPulo, delta);
    }

    private bool AcumularRuido(float velocidade, bool movimentoBrusco, float delta)
    {
        if (movimentoBrusco) return true;
        if (velocidade > velocidadeQueAcorda)
            ruidoAcumulado += delta;
        else ruidoAcumulado = 0f;
        return ruidoAcumulado > 0f && ruidoAcumulado >= toleranciaRuido;
    }

    private void Acordar()
    {
        EstadoSono = EstadoSonoGorila.Acordando;
        tempoEstadoSono = Mathf.Max(0.1f, tempoAcordar);
        ruidoAcumulado = 0f;
        animGorila.SetInteger(hashParametro, valorParado);
        animGorila.CrossFadeInFixedTime(hashParado, tempoEstadoSono, 0, 0f);
    }

    private void InterromperSono()
    {
        if (sonoPreparado && EstadoSono != EstadoSonoGorila.Acordado) VoltarAoRepouso();
        sonoPreparado = false;
        EstadoSono = EstadoSonoGorila.Acordado;
        ruidoAcumulado = 0f;
    }

    private void DesenharAudicao()
    {
        if (!usarCicloSono) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(CentroAudicao, raioAudicao);
    }
}
