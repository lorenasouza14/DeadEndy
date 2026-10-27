using UnityEngine;

// Movimento sobre o Idle do modelo: não desloca o Rigidbody nem os colisores da arena.
[DefaultExecutionOrder(110)]
public class DancaCaranguejo : MonoBehaviour
{
    private CaranguejoChefe chefe;
    private Transform[] ossos;
    private Quaternion[] rotacoes;
    private bool aplicada;
    private float inicio;
    public bool Ativa { get; private set; }
    public int OssosEncontrados => ossos == null ? 0 : System.Array.FindAll(ossos, osso => osso != null).Length;

    public void Iniciar(CaranguejoChefe dono)
    {
        Encerrar();
        chefe = dono;
        string[] nomes = { "RigPelvis", "RigRibcage", "RigLArm1", "RigRArm1", "RigHead" };
        ossos = new Transform[nomes.Length];
        rotacoes = new Quaternion[nomes.Length];
        foreach (var filho in GetComponentsInChildren<Transform>(true))
            for (int i = 0; i < nomes.Length; i++) if (filho.name == nomes[i]) ossos[i] = filho;
        inicio = Time.time;
        Ativa = true;
    }

    void Update() { Restaurar(); }

    void LateUpdate()
    {
        if (!Ativa || chefe == null || !chefe.EstaDancando) { Encerrar(); return; }
        float ritmo = (Time.time - inicio) * Mathf.PI * 2f * 1.9f;
        float entrada = Mathf.Clamp01((Time.time - inicio) / 0.3f);
        for (int i = 0; i < ossos.Length; i++)
        {
            if (ossos[i] == null) continue;
            rotacoes[i] = ossos[i].localRotation;
            float angulo = i == 0 ? Mathf.Sin(ritmo) * 7 : i == 1 ? Mathf.Sin(ritmo * 0.5f) * 10 :
                i == 2 ? -20 + Mathf.Sin(ritmo) * 22 : i == 3 ? 20 + Mathf.Sin(ritmo + Mathf.PI) * 22 : Mathf.Sin(ritmo) * 9;
            Vector3 eixo = i == 1 ? transform.up : i == 4 ? transform.right : transform.forward;
            ossos[i].rotation = Quaternion.AngleAxis(angulo * entrada, eixo) * ossos[i].rotation;
        }
        aplicada = true;
    }

    void Restaurar()
    {
        if (!aplicada) return;
        for (int i = 0; i < ossos.Length; i++) if (ossos[i] != null) ossos[i].localRotation = rotacoes[i];
        aplicada = false;
    }

    public void Encerrar() { Restaurar(); Ativa = false; }
    void OnDisable() { Encerrar(); }
}
