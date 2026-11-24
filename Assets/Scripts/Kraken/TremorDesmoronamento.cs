using UnityEngine;
using Unity.Cinemachine;

// Correcao aditiva na camera, sem deslocar o jogador nem acumular desvio.
[DisallowMultipleComponent]
public class TremorDesmoronamento : CinemachineExtension
{
    public bool EmExecucao { get; private set; }
    float inicio, amplitude, graus, frequencia, entrada, duracao;

    public void Iniciar(float deslocamento, float rotacao, float velocidade, float suavizacao, float tempo = 0f)
    {
        amplitude = Mathf.Max(0, deslocamento);
        graus = Mathf.Max(0, rotacao);
        frequencia = Mathf.Max(.1f, velocidade);
        entrada = Mathf.Max(0, suavizacao);
        duracao = Mathf.Max(0, tempo);
        inicio = Time.time;
        EmExecucao = true;
    }
    public void Parar() { EmExecucao = false; }
    float Ruido(float tempo, float semente) => (Mathf.PerlinNoise(semente, tempo) - .5f) * 2f;

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage,
        ref CameraState state, float deltaTime)
    {
        if (!Application.isPlaying || !EmExecucao || stage != CinemachineCore.Stage.Noise) return;
        float decorrido = Time.time - inicio;
        if (duracao > 0f && decorrido >= duracao) { Parar(); return; }
        float peso = entrada > 0 ? Mathf.SmoothStep(0, 1, Mathf.Clamp01(decorrido / entrada)) : 1;
        if (duracao > 0f) peso *= 1f - Mathf.SmoothStep(0, 1, Mathf.Clamp01(decorrido / duracao));
        float t = decorrido * frequencia;
        Vector3 deslocamento = new Vector3(Ruido(t, 11.3f), Ruido(t, 37.7f), 0) * (amplitude * peso);
        Vector3 rotacao = new Vector3(Ruido(t, 63.1f), Ruido(t, 89.9f), Ruido(t, 117.2f) * .5f) * (graus * peso);
        state.PositionCorrection += state.RawOrientation * deslocamento;
        state.OrientationCorrection *= Quaternion.Euler(rotacao);
    }
}
