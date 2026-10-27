using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Collider))]
public class ItemColetavel : MonoBehaviour
{
    public bool ehPocao;
    public bool ehDiario;
    public bool ehPirulito;
    public bool ehTrompete;
    public bool ehRadio;
    private InventarioEndy inventario;
    private readonly HashSet<Collider> contatos = new HashSet<Collider>();

    void Update()
    {
        if (inventario == null || Time.timeScale == 0) return;
#if ENABLE_INPUT_SYSTEM
        bool pegar = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        bool pegar = Input.GetKeyDown(KeyCode.E);
#endif
        if (pegar) TentarColetar(inventario);
        if (ehRadio && inventario != null)
        {
            var radio = GetComponent<EfeitoTocaDiscos>();
#if ENABLE_INPUT_SYSTEM
            bool recolher = Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;
#else
            bool recolher = Input.GetKeyDown(KeyCode.Q);
#endif
            if (recolher && radio != null && radio.Colocado) inventario.ColetarRadio(radio);
        }
    }

    public bool TentarColetar(InventarioEndy destino)
    {
        if (destino == null || !enabled) return false;
        var vida = destino.GetComponent<VidaEndy>();
        if (vida != null && vida.EstaMorto) return false;
        if (ehRadio)
        {
            var radio = GetComponent<EfeitoTocaDiscos>();
            return radio != null && (radio.Colocado ? radio.AlternarSom() : destino.ColetarRadio(radio));
        }
        if (ehTrompete) return destino.ColetarTrompete(GetComponent<EfeitoTrompete>());
        if (ehPirulito) return destino.ColetarPirulito(GetComponent<EfeitoPirulito>());
        if (ehPocao)
        {
            PocaoMagica pocao = GetComponent<PocaoMagica>();
            if (pocao != null) return destino.ColetarPocao(pocao);
            if (!destino.PodeColetarPocao) return false;
            destino.ColetarItem(true, ehDiario);
            if (!destino.temPocao) return false;
        }
        else destino.ColetarItem(false, ehDiario);
        enabled = false;
        Destroy(gameObject);
        return true;
    }

    void OnTriggerEnter(Collider outro) { RegistrarContato(outro); }
    void OnTriggerStay(Collider outro) { RegistrarContato(outro); }

    void RegistrarContato(Collider outro)
    {
        if (!enabled) return;
        InventarioEndy encontrado = outro.GetComponentInParent<InventarioEndy>();
        if (encontrado == null || (inventario != null && inventario != encontrado)) return;
        inventario = encontrado;
        contatos.Add(outro);
    }

    void OnTriggerExit(Collider outro)
    {
        contatos.Remove(outro);
        if (contatos.Count == 0) inventario = null;
    }

    void OnDisable() { contatos.Clear(); inventario = null; }

    public string ObterDica()
    {
        if (!enabled || inventario == null || !inventario.mostrarControles) return null;
        var vida = inventario.GetComponent<VidaEndy>();
        if (vida != null && vida.EstaMorto) return null;
        if (ehRadio)
        {
            var radio = GetComponent<EfeitoTocaDiscos>();
            if (radio == null || radio.NaMao) return null;
            if (!radio.Colocado && !inventario.PodeColetarPocao) return null;
            string texto = !radio.Colocado ? "[E] Pegar rádio" : radio.Ligado ? "[E] Desligar rádio" : "[E] Ligar rádio";
            if (radio.Colocado && !radio.Ligado && inventario.PodeColetarPocao) texto += "    [Q] Recolher";
            if (!string.IsNullOrEmpty(radio.Aviso)) texto += "\n" + radio.Aviso;
            return texto;
        }
        if ((ehPocao || ehPirulito || ehTrompete) && !inventario.PodeColetarPocao) return null;
        return ehTrompete ? "[E] Pegar trompete" : (ehPirulito ? "[E] Pegar pirulito" : (ehPocao ? "[E] Pegar poção" : "[E] Pegar item"));
    }
}
