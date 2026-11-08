using UnityEngine;

[RequireComponent(typeof(GorilaGuardiao))]
[DisallowMultipleComponent]
public class GorilaSopa : MonoBehaviour
{
    public enum EtapaSopa { Esperando, Levantando, Caminhando, Comendo }
    [Tooltip("Pontos no chao, fora da hierarquia do gorila, em ordem. O ultimo e onde ele fica comendo.")]
    public Transform[] percurso;
    [Tooltip("Ponto no caldeirao para o gorila olhar ao comer.")]
    public Transform olharParaSopa;
    [Min(0.1f)] public float velocidade = 1.5f;
    [Min(1f)] public float velocidadeGiro = 180f;
    public string estadoAndar = "Base Layer.Walk";
    public string estadoComer = "Base Layer.Eat";
    public int valorAndar = 1;
    public int valorComer = 4;
    [Tooltip("Somente os colliders extras que bloqueiam a passagem. Desativados quando o gorila chega a sopa.")]
    public Collider[] bloqueiosDaCaverna;
    public EtapaSopa Etapa { get; private set; }
    public bool PassagemLiberada => Etapa == EtapaSopa.Comendo;
    private GorilaGuardiao guarda;
    private float tempo;
    private int destino;
    private Animator Anim => Guarda != null ? Guarda.animGorila : null;
    private GorilaGuardiao Guarda => guarda != null ? guarda : (guarda = GetComponent<GorilaGuardiao>());

    public bool Configurado
    {
        get
        {
            if (!isActiveAndEnabled || Guarda == null || !Guarda.isActiveAndEnabled || percurso == null || percurso.Length == 0 || olharParaSopa == null) return false;
            foreach (var ponto in percurso) if (ponto == null || ponto.IsChildOf(transform)) return false;
            var anim = Anim != null ? Anim : GetComponentInChildren<Animator>();
            return anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null &&
                anim.HasState(0, Animator.StringToHash(estadoAndar)) && anim.HasState(0, Animator.StringToHash(estadoComer));
        }
    }

    public bool AtrairPelaSopa(CaldeiraoSopa sopa)
    {
        if (Etapa != EtapaSopa.Esperando || !Configurado || sopa == null || sopa.gorila != this || !sopa.SopaPronta) return false;
        bool levantando = Guarda.SonoImpedeAtaque;
        Guarda.TornarDocilPelaSopa();
        Etapa = EtapaSopa.Levantando;
        tempo = levantando ? Mathf.Max(0.1f, Guarda.tempoAcordar) : 0.25f;
        Anim.SetInteger(Guarda.parametroAnimacao, Guarda.valorParado);
        Anim.CrossFadeInFixedTime(Guarda.estadoParado, tempo, 0, 0f);
        destino = 0;
        return true;
    }

    private void Update() { Avancar(Time.deltaTime); }

    private void Avancar(float delta)
    {
        if (delta <= 0f || Etapa == EtapaSopa.Esperando || !Configurado) return;
        if (Etapa == EtapaSopa.Levantando)
        {
            tempo -= delta;
            if (tempo > 0f) return;
            Etapa = EtapaSopa.Caminhando;
            Anim.SetInteger(Guarda.parametroAnimacao, valorAndar);
            Anim.CrossFadeInFixedTime(estadoAndar, 0.2f, 0, 0f);
        }
        if (Etapa == EtapaSopa.Caminhando)
        {
            Vector3 alvo = percurso[destino].position;
            GirarPara(alvo, delta);
            // Percurso curto e autorado: os pontos devem acompanhar o solo e contornar pedras/calderao.
            transform.position = Vector3.MoveTowards(transform.position, alvo, velocidade * delta);
            if ((transform.position - alvo).sqrMagnitude <= 0.0001f)
            {
                destino++;
                if (destino == percurso.Length)
                {
                    Etapa = EtapaSopa.Comendo;
                    Anim.SetInteger(Guarda.parametroAnimacao, valorComer);
                    Anim.CrossFadeInFixedTime(estadoComer, 0.25f, 0, 0f);
                    if (bloqueiosDaCaverna != null)
                        foreach (var col in bloqueiosDaCaverna) if (col != null) col.enabled = false;
                }
            }
        }
        if (Etapa == EtapaSopa.Comendo) GirarPara(olharParaSopa.position, delta);
    }

    private void GirarPara(Vector3 ponto, float delta)
    {
        Vector3 frente = Vector3.ProjectOnPlane(ponto - transform.position, Vector3.up);
        if (frente.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(frente), velocidadeGiro * delta);
    }

    private void OnDrawGizmosSelected()
    {
        if (percurso == null) return;
        Gizmos.color = Color.magenta;
        Vector3 anterior = transform.position;
        foreach (var ponto in percurso)
        {
            if (ponto == null) continue;
            Gizmos.DrawLine(anterior, ponto.position);
            Gizmos.DrawWireSphere(ponto.position, 0.15f);
            anterior = ponto.position;
        }
    }
}
