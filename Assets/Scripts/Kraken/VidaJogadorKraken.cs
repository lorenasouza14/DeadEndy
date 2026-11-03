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
    void Awake() { VidaAtual = vidaMaxima; }
    public bool ReceberDano(int dano)
    {
        if (Morto || dano <= 0 || Time.time < podeReceberEm) return false;
        VidaAtual = Mathf.Max(0, VidaAtual - dano);
        podeReceberEm = Time.time + invulnerabilidade;
        if (VidaAtual == 0) StartCoroutine(Renascer());
        return true;
    }
    IEnumerator Renascer()
    {
        Morto = true;
        var controle = GetComponent<ThirdPersonController>();
        bool estavaAtivo = controle && controle.enabled;
        if (controle) controle.enabled = false;
        yield return new WaitForSeconds(1.5f);
        Transform entrada = null;
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "PontoEntrada") entrada = t;
        if (entrada)
        {
            var cc = GetComponent<CharacterController>();
            bool estavaLigado = cc && cc.enabled;
            if (cc) cc.enabled = false;
            transform.SetPositionAndRotation(entrada.position, Quaternion.Euler(0, entrada.eulerAngles.y, 0));
            Physics.SyncTransforms();
            if (cc) cc.enabled = estavaLigado;
            if (controle) controle.PrepararTrocaDeMundo(Camera.main);
            foreach (var cam in FindObjectsByType<Unity.Cinemachine.CinemachineCamera>()) cam.PreviousStateIsValid = false;
        }
        VidaAtual = vidaMaxima;
        podeReceberEm = Time.time + invulnerabilidade;
        Morto = false;
        if (controle) controle.enabled = estavaAtivo;
    }
}
