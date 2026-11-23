using UnityEngine;

public sealed class ReproducaoEfeitoEndy : MonoBehaviour
{
    public MonoBehaviour Dono { get; private set; }
    public string Nome { get; private set; }
    public bool Solicitado = true;
    AudioSource fonte;
    bool interfaceUI, continuo, pausado;
    float restante;
    public void Configurar(AudioClip clip, float volume, bool ui, bool loop, MonoBehaviour dono, float duracao)
    {
        Dono = dono; Nome = clip.name; interfaceUI = ui; continuo = loop;
        restante = duracao > 0f ? Mathf.Min(duracao, clip.length) : clip.length;
        fonte = gameObject.AddComponent<AudioSource>();
        fonte.playOnAwake = false; fonte.clip = clip; fonte.loop = loop;
        fonte.volume = volume; fonte.spatialBlend = ui ? 0f : 1f;
        fonte.rolloffMode = AudioRolloffMode.Linear; fonte.minDistance = 3f; fonte.maxDistance = 22f;
        fonte.dopplerLevel = 0f; fonte.ignoreListenerPause = ui;
        if (!EfeitosSonoros.Suspenso || ui) fonte.Play();
        else pausado = true;
    }
    void Update()
    {
        if (!fonte) return;
        if (continuo && (!Dono || !Dono.isActiveAndEnabled || !Solicitado))
        {
            fonte.Stop(); Destroy(gameObject); return;
        }
        bool suspenso = !interfaceUI && EfeitosSonoros.Suspenso;
        if (suspenso && !pausado) { fonte.Pause(); pausado = true; }
        if (suspenso) return;
        if (pausado) { fonte.UnPause(); if (!fonte.isPlaying) fonte.Play(); pausado = false; }
        if (continuo && !fonte.isPlaying) fonte.Play();
        if (!continuo)
        {
            restante -= Time.unscaledDeltaTime;
            if (restante <= 0f) { fonte.Stop(); Destroy(gameObject); }
        }
    }
    void OnDisable() { if (fonte) fonte.Stop(); }
}
