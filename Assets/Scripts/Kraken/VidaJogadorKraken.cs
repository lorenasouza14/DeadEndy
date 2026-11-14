using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using StarterAssets;

[DisallowMultipleComponent]
public class VidaJogadorKraken : MonoBehaviour
{
    [Min(1)] public int vidaMaxima = 100;
    [Min(0)] public float invulnerabilidade = .8f;
    public int VidaAtual { get; private set; }
    public bool Morto { get; private set; }
    float podeReceberEm;
    string cenaParaReiniciar;

    void Awake() { VidaAtual = vidaMaxima; }
    public bool ReceberDano(int dano)
    {
        if (Morto || dano <= 0 || Time.time < podeReceberEm) return false;
        VidaAtual = Mathf.Max(0, VidaAtual - dano);
        podeReceberEm = Time.time + invulnerabilidade;
        if (VidaAtual == 0) Morrer();
        return true;
    }
    public void Morrer()
    {
        if (Morto) return;
        Morto = true;
        VidaAtual = 0;
        cenaParaReiniciar = SceneManager.GetActiveScene().path;
        SceneManager.sceneLoaded += AoReiniciarCena;
        StartCoroutine(ReiniciarFase());
    }
    IEnumerator ReiniciarFase()
    {
        // A inundacao roda na coroutine da batalha: interromper apenas a agua
        // permite que o ataque seguinte continue durante a morte.
        foreach (var batalha in FindObjectsByType<BatalhaKraken>(FindObjectsSortMode.None))
        {
            batalha.StopAllCoroutines();
            batalha.enabled = false;
        }
        foreach (var captura in FindObjectsByType<CapturaJogadorKraken>(FindObjectsSortMode.None))
            captura.Cancelar();
        foreach (var agua in FindObjectsByType<InundacaoKraken>(FindObjectsSortMode.None))
            agua.CancelarERestaurar();
        var controle = GetComponent<ThirdPersonController>();
        if (controle)
        {
            controle.DefinirMultiplicadorAgua(1);
            controle.enabled = false;
        }
        yield return new WaitForSecondsRealtime(1.5f);
        Time.timeScale = 1f;
        // Recria tambem pilares, correntes, Kraken e todos os ataques pendentes.
        SceneManager.LoadScene(cenaParaReiniciar, LoadSceneMode.Single);
    }
    void AoReiniciarCena(Scene cena, LoadSceneMode modo)
    {
        if (cena.path != cenaParaReiniciar) return;
        SceneManager.sceneLoaded -= AoReiniciarCena;
        // TrocarMundo mantem o jogador vivo entre cenas; ele precisa de reset proprio.
        var controle = GetComponent<ThirdPersonController>();
        var capsula = GetComponent<CharacterController>();
        if (capsula) capsula.enabled = false;
        foreach (var raiz in cena.GetRootGameObjects())
            foreach (var entrada in raiz.GetComponentsInChildren<Transform>(true))
                if (entrada.name == "PontoEntrada" && entrada.gameObject.activeInHierarchy)
                    transform.SetPositionAndRotation(entrada.position, Quaternion.Euler(0, entrada.eulerAngles.y, 0));
        Physics.SyncTransforms();
        if (capsula) capsula.enabled = true;
        if (controle)
        {
            controle.DefinirMultiplicadorAgua(1);
            controle.PrepararTrocaDeMundo(Camera.main);
            controle.enabled = true;
        }
        foreach (var camera in FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsSortMode.None))
            camera.PreviousStateIsValid = false;
        VidaAtual = vidaMaxima;
        podeReceberEm = Time.time + invulnerabilidade;
        Morto = false;
    }
    void OnDestroy() { SceneManager.sceneLoaded -= AoReiniciarCena; }
}
