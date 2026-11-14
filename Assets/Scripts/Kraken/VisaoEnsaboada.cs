using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public class VisaoEnsaboada : MonoBehaviour
{
    public bool Ativa => volume && volume.weight > .01f;
    public float IntensidadeAtual => volume ? volume.weight : 0;
    Volume volume;
    VolumeProfile perfil;
    UniversalAdditionalCameraData dados;
    VidaJogadorKraken vida;
    bool posProcessamentoAnterior;
    float ate;

    void Awake()
    {
        dados = GetComponent<UniversalAdditionalCameraData>();
        if (!dados) dados = gameObject.AddComponent<UniversalAdditionalCameraData>();
        posProcessamentoAnterior = dados.renderPostProcessing;
        var objeto = new GameObject("Embacamento temporario de sabao");
        objeto.transform.SetParent(transform, false);
        int mascara = dados.volumeLayerMask.value;
        for (int camada = 0; camada < 32; camada++)
            if ((mascara & (1 << camada)) != 0) { objeto.layer = camada; break; }
        volume = objeto.AddComponent<Volume>();
        volume.isGlobal = true; volume.priority = 5000; volume.weight = 0;
        perfil = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.sharedProfile = perfil;
        var foco = perfil.Add<DepthOfField>(true);
        foco.mode.Override(DepthOfFieldMode.Bokeh);
        foco.focusDistance.Override(.3f);
        foco.aperture.Override(1f);
        foco.focalLength.Override(150f);
        foco.gaussianStart.Override(0);
        foco.gaussianEnd.Override(2);
        foco.gaussianMaxRadius.Override(.7f);
        foco.highQualitySampling.Override(true);
    }
    public void Aplicar(VidaJogadorKraken jogador, float duracao)
    {
        if (!jogador || jogador.Morto) return;
        vida = jogador;
        ate = Mathf.Max(ate, Time.time + Mathf.Max(.1f, duracao));
        dados.renderPostProcessing = true;
    }
    void Update()
    {
        if (!vida || vida.Morto) { Limpar(); return; }
        float restante = ate - Time.time;
        float alvo = restante > 0 ? Mathf.Clamp01(restante / .8f) : 0;
        volume.weight = Mathf.MoveTowards(volume.weight, alvo, Time.deltaTime * 4);
        if (restante <= 0 && volume.weight <= 0) dados.renderPostProcessing = posProcessamentoAnterior;
    }
    void OnGUI()
    {
        if (!Ativa) return;
        Color corAnterior = GUI.color;
        int profundidadeAnterior = GUI.depth;
        GUI.depth = -50;
        GUI.color = new Color(.88f, .96f, 1f, .4f * IntensidadeAtual);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = corAnterior;
        GUI.depth = profundidadeAnterior;
    }
    public void Limpar()
    {
        ate = 0;
        if (volume) volume.weight = 0;
        if (dados) dados.renderPostProcessing = posProcessamentoAnterior;
    }
    void OnDisable() { Limpar(); }
    void OnDestroy()
    {
        Limpar();
        if (volume) Destroy(volume.gameObject);
        if (perfil) Destroy(perfil);
    }
}
