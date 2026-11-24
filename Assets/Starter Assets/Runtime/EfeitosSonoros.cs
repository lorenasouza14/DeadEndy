using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Compartilhado com o controlador do Starter Assets, sem dependencia das cenas.
public static class EfeitosSonoros
{
    static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    static readonly Dictionary<(MonoBehaviour, string), ReproducaoEfeitoEndy> continuos = new Dictionary<(MonoBehaviour, string), ReproducaoEfeitoEndy>();
    static float proximoDano;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() { clips.Clear(); continuos.Clear(); proximoDano = 0f; }
    public static AudioClip Carregar(string nome)
    {
        if (!clips.TryGetValue(nome, out var clip))
        {
            clip = Resources.Load<AudioClip>("EfeitosSonoros/" + nome);
            if (clip && nome.StartsWith("Passos")) clip = RecortarPasso(clip);
            clips[nome] = clip;
            if (!clip) Debug.LogWarning("Efeito sonoro ausente: " + nome);
        }
        return clip;
    }
    public static bool Suspenso => Time.timeScale <= 0f || SceneManager.GetActiveScene().name == "MenuPrincipal";
    public static void Tocar(string nome, Vector3 posicao, float volume = .65f, bool interfaceUI = false, float duracao = 0f, bool espacial = true)
    {
        if (!Application.isPlaying || (!interfaceUI && Suspenso)) return;
        if (nome == "DanoEndy")
        {
            if (Time.unscaledTime < proximoDano) return;
            proximoDano = Time.unscaledTime + .25f;
            volume *= .25f;
        }
        var clip = Carregar(nome);
        if (!clip) return;
        var go = new GameObject("Som: " + nome);
        go.transform.position = posicao;
        var som = go.AddComponent<ReproducaoEfeitoEndy>();
        som.Configurar(clip, volume, interfaceUI, false, null, duracao, espacial);
    }
    public static void Repetir(MonoBehaviour dono, string nome, bool tocar, float volume = .3f, Transform origem = null)
    {
        if (!Application.isPlaying || !dono) return;
        var chave = (dono, nome);
        if (continuos.TryGetValue(chave, out var existente) && existente)
        {
            existente.Solicitado = tocar;
            return;
        }
        if (!tocar) return;
        var clip = Carregar(nome);
        if (!clip) return;
        var go = new GameObject("Som continuo: " + nome);
        go.transform.SetParent(origem ? origem : dono.transform, false);
        var novo = go.AddComponent<ReproducaoEfeitoEndy>();
        continuos[chave] = novo;
        novo.Configurar(clip, volume, false, true, dono, 0f);
    }
    // As gravacoes de passos podem conter varias pisadas. Recorta uma so por evento da animacao.
    static AudioClip RecortarPasso(AudioClip original)
    {
        if (original.length <= .32f) return original;
        var dados = new float[original.samples * original.channels];
        if (!original.GetData(dados, 0)) return original;
        float pico = 0f;
        foreach (float amostra in dados) pico = Mathf.Max(pico, Mathf.Abs(amostra));
        int inicio = 0;
        while (inicio < original.samples - 1 && Mathf.Abs(dados[inicio * original.channels]) < pico * .18f) inicio++;
        inicio = Mathf.Max(0, inicio - (int)(original.frequency * .015f));
        int quantidade = Mathf.Min((int)(original.frequency * .3f), original.samples - inicio);
        var trecho = new float[quantidade * original.channels];
        System.Array.Copy(dados, inicio * original.channels, trecho, 0, trecho.Length);
        int rampa = Mathf.Max(1, (int)(original.frequency * .01f));
        for (int i = 0; i < quantidade; i++)
        {
            float ganho = Mathf.Min(1f, Mathf.Min(i, quantidade - 1 - i) / (float)rampa);
            for (int canal = 0; canal < original.channels; canal++) trecho[i * original.channels + canal] *= ganho;
        }
        var clip = AudioClip.Create(original.name, quantidade, original.channels, original.frequency, false);
        clip.SetData(trecho, 0);
        return clip;
    }

}

