using System;
using System.Collections.Generic;
using UnityEngine;

// A caverna contém gatilhos no caminho e nos fantasmas, e colisores sólidos nas plantas.
[DefaultExecutionOrder(-1), DisallowMultipleComponent]
public sealed class PerigosContatoCaverna : MonoBehaviour
{
    [SerializeField, Min(1)] int danoPorContato = 10;
    readonly Collider[] contatos = new Collider[32];
    readonly RaycastHit[] apoios = new RaycastHit[16];
    readonly Dictionary<Collider, bool> plantasPorColisor = new Dictionary<Collider, bool>();
    CharacterController capsula;
    VidaJogadorKraken vida;
    int camadaFantasma, camadaCaminho, camadasDePiso;
    float ultimoContato = float.NegativeInfinity;
    float ultimoGolpe = float.NegativeInfinity;

    public bool LongeDePerigo => Time.time - ultimoContato > 1f;

    public void Preparar(CharacterController jogador, VidaJogadorKraken vidaJogador)
    {
        capsula = jogador;
        vida = vidaJogador;
        camadaFantasma = LayerMask.NameToLayer("Fantasma");
        camadaCaminho = LayerMask.NameToLayer("PonteFantasma");
        // Algumas pedras do cenário compartilham a camada Personagem.
        camadasDePiso = Physics.AllLayers & ~LayerMask.GetMask("PonteFantasma", "Fantasma", "Water");
    }

    void LateUpdate()
    {
        if (Time.timeScale <= 0 || capsula == null || !capsula.enabled || vida == null || vida.Morto) return;
        var alvo = capsula.transform;
        Vector3 escala = alvo.lossyScale;
        float raio = capsula.radius * Mathf.Max(Mathf.Abs(escala.x), Mathf.Abs(escala.z));
        float meiaAltura = capsula.height * Mathf.Abs(escala.y) * 0.5f;
        Vector3 centro = alvo.TransformPoint(capsula.center);
        Vector3 eixo = alvo.up * Mathf.Max(0, meiaAltura - raio);
        // Inclui a folga de contato do CharacterController ao encostar em uma planta sólida.
        raio += capsula.skinWidth;
        int quantidade = Physics.OverlapCapsuleNonAlloc(centro - eixo, centro + eixo, raio, contatos,
            Physics.AllLayers, QueryTriggerInteraction.Collide);
        var encontrados = contatos;
        if (quantidade == contatos.Length)
        {
            encontrados = Physics.OverlapCapsule(centro - eixo, centro + eixo, raio, Physics.AllLayers, QueryTriggerInteraction.Collide);
            quantidade = encontrados.Length;
        }
        for (int i = 0; i < quantidade; i++)
        {
            if (!EhPerigo(encontrados[i])) continue;
            ultimoContato = Time.time;
            if (vida.ReceberDano(danoPorContato)) ultimoGolpe = Time.time;
            break;
        }
    }

    bool EhPerigo(Collider contato)
    {
        if (contato.gameObject.scene != gameObject.scene) return false;
        if (contato.gameObject.layer == camadaCaminho)
            return contato.name.StartsWith("CaminhoFantasma", StringComparison.OrdinalIgnoreCase) && !TemPisoSobOsPes();
        if (contato.gameObject.layer == camadaFantasma)
        {
            var fantasma = contato.GetComponentInParent<FantasmaCaverna>();
            return fantasma != null && fantasma.isActiveAndEnabled && !fantasma.EmMorte;
        }
        if (plantasPorColisor.TryGetValue(contato, out bool planta)) return planta;
        string nome = contato.name;
        planta = nome.IndexOf("climbing_plant", StringComparison.OrdinalIgnoreCase) >= 0 ||
            nome.IndexOf("clinbing_plant", StringComparison.OrdinalIgnoreCase) >= 0;
        plantasPorColisor.Add(contato, planta);
        return planta;
    }

    bool TemPisoSobOsPes()
    {
        // As pontes invisíveis atravessam partes do cenário. O piso que sustenta
        // a cápsula tem prioridade, inclusive em degraus e nas bordas da malha.
        Bounds limites = capsula.bounds;
        float raio = Mathf.Min(limites.extents.x, limites.extents.z) * 0.9f;
        float folga = capsula.skinWidth * Mathf.Abs(capsula.transform.lossyScale.y);
        float degrau = capsula.stepOffset * Mathf.Abs(capsula.transform.lossyScale.y);
        Vector3 origem = new Vector3(limites.center.x, limites.min.y + raio + degrau + folga, limites.center.z);
        float alcance = degrau + 2f * folga + 0.05f;
        var pisos = apoios;
        int quantidade = Physics.SphereCastNonAlloc(origem, raio, Vector3.down, pisos, alcance, camadasDePiso, QueryTriggerInteraction.Ignore);
        if (quantidade == pisos.Length)
        {
            pisos = Physics.SphereCastAll(origem, raio, Vector3.down, alcance, camadasDePiso, QueryTriggerInteraction.Ignore);
            quantidade = pisos.Length;
        }
        for (int i = 0; i < quantidade; i++)
            if (!pisos[i].collider.transform.IsChildOf(capsula.transform) && Vector3.Angle(pisos[i].normal, Vector3.up) <= capsula.slopeLimit)
                return true;
        return false;
    }

    void OnGUI()
    {
        float intensidade = 1f - (Time.time - ultimoGolpe) / 0.25f;
        if (Time.timeScale <= 0f || intensidade <= 0f || Event.current.type != EventType.Repaint) return;
        Color anterior = GUI.color;
        GUI.color = new Color(0.8f, 0.05f, 0.04f, 0.25f * intensidade);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = anterior;
    }
}
