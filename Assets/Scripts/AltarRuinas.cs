using UnityEngine;

[DisallowMultipleComponent]
public class AltarRuinas : MonoBehaviour
{
    public PuzzleAltaresRuinas puzzle;
    public Transform pivo;
    public Transform alavanca;
    public Transform caboAlavanca;
    public Transform emissor;
    public LineRenderer feixe;
    public Light luz;
    public Quaternion rotacaoAlinhadaLocal = Quaternion.identity;
    [Range(0, 3)] public int passosIniciais;
    [Range(0.1f, 10f)] public float toleranciaAlinhamento = 2f;
    public int Passos { get; private set; }
    public bool Alinhado { get; private set; }
    private Quaternion repousoAlavanca;

    public void Preparar()
    {
        Passos = passosIniciais;
        repousoAlavanca = caboAlavanca.localRotation;
        pivo.localRotation = RotacaoDestino;
        MostrarFeixe(false);
    }

    public Quaternion RotacaoDestino => rotacaoAlinhadaLocal * Quaternion.Euler(0f, Passos * 90f, 0f);
    public void Avancar() { Passos = (Passos + 1) % 4; }
    public void AnimarAlavanca(float progresso)
    {
        caboAlavanca.localRotation = repousoAlavanca * Quaternion.Euler(0f, 0f, -45f * Mathf.Sin(progresso * Mathf.PI));
    }

    public void AtualizarFeixe(Collider monolito, bool girando)
    {
        Vector3 destino = new Vector3(monolito.bounds.center.x, emissor.position.y, monolito.bounds.center.z);
        Vector3 direcao = destino - emissor.position;
        Vector3 frente = Vector3.ProjectOnPlane(emissor.forward, Vector3.up);
        bool alinhado = !girando && monolito.enabled && monolito.gameObject.activeInHierarchy &&
            Vector3.Angle(frente, direcao) <= toleranciaAlinhamento;
        if (alinhado)
        {
            alinhado = monolito.Raycast(new Ray(emissor.position, direcao.normalized), out var hit, direcao.magnitude + 5f);
            if (alinhado)
            {
                feixe.SetPosition(0, emissor.position);
                feixe.SetPosition(1, hit.point);
            }
        }
        MostrarFeixe(alinhado);
    }

    public void MostrarFeixe(bool visivel)
    {
        Alinhado = visivel;
        if (feixe != null) feixe.enabled = visivel;
        if (luz != null) luz.enabled = visivel;
    }

    public string ObterDica()
    {
        return puzzle != null && puzzle.AltarProximo == this && !puzzle.Ocupado && !puzzle.Resolvido
            ? "[E] Acionar alavanca" : null;
    }

    private void OnDisable() { MostrarFeixe(false); }
}
