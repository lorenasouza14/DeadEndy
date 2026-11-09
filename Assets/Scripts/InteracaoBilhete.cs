using UnityEngine;
using System.Collections.Generic;
using System.Collections;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class InteracaoBilhete : MonoBehaviour
{
    [Header("Interface e Animação")]
    public GameObject painelUI;
    public Animator animEndy; // Arraste o personagem Endy para cá no Inspector
    [Tooltip("Tempo para mostrar o gesto do Endy antes de abrir a imagem do bilhete.")]
    [Min(0f)] public float atrasoAbrirBilhete = 0.7f;

    private bool pertoDoObjeto = false;
    private readonly HashSet<Collider> contatos = new HashSet<Collider>();
    private InventarioEndy inventario;
    private Coroutine abertura;
    private bool abrindo;
    public bool DisponivelParaLeitura => isActiveAndEnabled && !abrindo && painelUI != null && !painelUI.activeSelf;
    public bool MostrarDica => pertoDoObjeto && DisponivelParaLeitura;

    private BrilhoColetavel brilho;

    void Awake()
    {
        if (painelUI != null) painelUI.SetActive(false);
        brilho = new BrilhoColetavel(gameObject);
    }

    void LateUpdate() { if (DisponivelParaLeitura) brilho.Desenhar(Camera.main); }

    void Update()
    {
        if (Time.timeScale == 0 || painelUI == null) return;
#if ENABLE_INPUT_SYSTEM
        bool ler = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        bool ler = Input.GetKeyDown(KeyCode.E);
#endif
        if (pertoDoObjeto && ler) Interagir();
    }

    void OnTriggerEnter(Collider other)
    {
        if (isActiveAndEnabled && (other.CompareTag("Player") || other.GetComponentInParent<InventarioEndy>() != null))
        {
            contatos.Add(other);
            pertoDoObjeto = true;
            var encontrado = other.GetComponentInParent<InventarioEndy>();
            if (encontrado != null)
            {
                inventario = encontrado;
                if (encontrado.animEndy != null) animEndy = encontrado.animEndy;
            }
        }
    }

    void OnTriggerStay(Collider other) { OnTriggerEnter(other); }

    void OnTriggerExit(Collider other)
    {
        if (contatos.Remove(other) && contatos.Count == 0)
        {
            pertoDoObjeto = false;
            CancelarLeitura();
            inventario = null;
        }
    }

    public bool Interagir()
    {
        if (!isActiveAndEnabled || !pertoDoObjeto || painelUI == null || abrindo || Time.timeScale == 0) return false;
        var vida = inventario != null ? inventario.GetComponent<VidaEndy>() : null;
        if (vida != null && vida.EstaMorto) return false;
        if (painelUI.activeSelf)
        {
            painelUI.SetActive(false);
            AnimacaoInteracaoEndy.Tocar(inventario, animEndy);
        }
        else if (AnimacaoInteracaoEndy.Tocar(inventario, animEndy) && atrasoAbrirBilhete > 0)
            abertura = StartCoroutine(AbrirDepoisDoGesto());
        else painelUI.SetActive(true);
        return true;
    }

    IEnumerator AbrirDepoisDoGesto()
    {
        abrindo = true;
        yield return new WaitForSeconds(atrasoAbrirBilhete);
        var vida = inventario != null ? inventario.GetComponent<VidaEndy>() : null;
        if (pertoDoObjeto && painelUI != null && (vida == null || !vida.EstaMorto)) painelUI.SetActive(true);
        abrindo = false;
        abertura = null;
    }

    void CancelarLeitura()
    {
        if (abertura != null) StopCoroutine(abertura);
        abertura = null;
        abrindo = false;
        if (painelUI != null) painelUI.SetActive(false);
    }

    void OnDisable()
    {
        contatos.Clear();
        pertoDoObjeto = false;
        CancelarLeitura();
        inventario = null;
    }
}
