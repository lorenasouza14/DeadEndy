using UnityEngine;

public class CarrinhoTrilho : MonoBehaviour
{
    public Transform[] pontosDoCaminho;
    [Min(0.1f)] public float velocidade = 20f;
    [Min(0.1f)] public float distanciaMaximaEntrePontos = 12f;
    [Min(0.1f)] public float distanciaMaximaParaEmbarcar = 2f;
    public Vector3 ajusteRotacao = new Vector3(0f, -90f, 0f);
    Vector3[] rota;
    int indiceAtual;
    int sentido = 1;
    bool aMover;
    public bool EmViagem => aMover;
    public bool NoFinal { get; private set; }

    void Update() { Avancar(Time.deltaTime); }

    bool PrepararRota()
    {
        if (pontosDoCaminho == null || pontosDoCaminho.Length < 2) return false;
        var pontos = new Vector3[pontosDoCaminho.Length];
        for (int i = 0; i < pontos.Length; i++)
        {
            var ponto = pontosDoCaminho[i];
            if (ponto == null || ponto.IsChildOf(transform)) return false;
            pontos[i] = ponto.position;
            if (!Finito(pontos[i])) return false;
            if (i > 0 && (Vector3.Distance(pontos[i - 1], pontos[i]) > distanciaMaximaEntrePontos ||
                Vector3.SqrMagnitude(pontos[i - 1] - pontos[i]) < 0.000001f)) return false;
        }
        // As referencias pertencem ao trilho fixo, nunca ao carrinho em movimento.
        rota = pontos;
        return true;
    }

    public bool IniciarViagem()
    {
        if (aMover) return false;
        if (!PrepararRota())
        {
            Debug.LogWarning("Carrinho: configure uma rota continua sobre os trilhos, com pelo menos dois pontos validos.", this);
            return false;
        }
        float menorDistancia = float.PositiveInfinity;
        int segmento = 0;
        for (int i = 0; i < rota.Length - 1; i++)
        {
            Vector3 trecho = rota[i + 1] - rota[i];
            float t = Mathf.Clamp01(Vector3.Dot(transform.position - rota[i], trecho) / trecho.sqrMagnitude);
            float distancia = (transform.position - (rota[i] + trecho * t)).sqrMagnitude;
            if (distancia < menorDistancia) { menorDistancia = distancia; segmento = i; }
        }
        if (menorDistancia > distanciaMaximaParaEmbarcar * distanciaMaximaParaEmbarcar)
        {
            Debug.LogWarning("Carrinho fora do trilho: a viagem foi bloqueada para nao levar o jogador para fora do caminho.", this);
            return false;
        }
        indiceAtual = sentido > 0 ? segmento + 1 : segmento;
        aMover = true;
        return true;
    }

    void Avancar(float delta)
    {
        if (!aMover || delta <= 0f) return;
        float restante = Mathf.Max(0f, velocidade) * delta;
        Vector3 direcao = Vector3.zero;
        while (aMover && restante > 0f)
        {
            Vector3 diferenca = rota[indiceAtual] - transform.position;
            float distancia = diferenca.magnitude;
            if (distancia > 0.0001f) direcao = diferenca / distancia;
            if (distancia > restante)
            {
                transform.position += direcao * restante;
                restante = 0f;
            }
            else
            {
                transform.position = rota[indiceAtual];
                restante -= distancia;
                if ((sentido > 0 && indiceAtual == rota.Length - 1) || (sentido < 0 && indiceAtual == 0))
                {
                    NoFinal = sentido > 0;
                    sentido = -sentido;
                    aMover = false;
                }
                else indiceAtual += sentido;
            }
        }
        if (direcao.sqrMagnitude > 0.0001f)
        {
            Quaternion alvo = Quaternion.LookRotation(direcao, Vector3.up) * Quaternion.Euler(ajusteRotacao);
            transform.rotation = Quaternion.Slerp(transform.rotation, alvo, 1f - Mathf.Exp(-15f * delta));
        }
    }

    public void PararViagem() { aMover = false; }
    static bool Finito(Vector3 p) => !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z) &&
        !float.IsInfinity(p.x) && !float.IsInfinity(p.y) && !float.IsInfinity(p.z);
}
