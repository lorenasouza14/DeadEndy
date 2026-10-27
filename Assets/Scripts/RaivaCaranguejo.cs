using UnityEngine;

// Pose sobre o Idle. Só move os ossos visuais, preservando chão e colisões.
[DefaultExecutionOrder(120)]
public class RaivaCaranguejo : MonoBehaviour
{
    private CaranguejoChefe chefe;
    private Transform[] ossos;
    private Quaternion[] rotacoes;
    private bool aplicada;
    private float inicio, duracao;
    public bool Ativa { get; private set; }
    public int OssosEncontrados => ossos == null ? 0 : System.Array.FindAll(ossos, osso => osso != null).Length;

    public void Iniciar(CaranguejoChefe dono, float tempo)
    {
        Encerrar();
        chefe = dono;
        if (ossos == null)
        {
            string[] nomes = { "RigPelvis", "RigRibcage", "RigLArm1", "RigRArm1", "RigHead" };
            ossos = new Transform[nomes.Length];
            rotacoes = new Quaternion[nomes.Length];
            foreach (var filho in GetComponentsInChildren<Transform>(true))
                for (int i = 0; i < nomes.Length; i++) if (filho.name == nomes[i]) ossos[i] = filho;
        }
        inicio = Time.time;
        duracao = Mathf.Max(0.3f, tempo);
        Ativa = true;
    }

    void Update() { Restaurar(); }

    void LateUpdate()
    {
        if (!Ativa || chefe == null || !chefe.EstaReagindoComRaiva) { Encerrar(); return; }
        float t = Mathf.Clamp01((Time.time - inicio) / duracao);
        float peso = Mathf.SmoothStep(0, 1, t / 0.22f) * (1 - Mathf.SmoothStep(0, 1, (t - 0.76f) / 0.24f));
        float sacudida = Mathf.Sin(Mathf.Clamp01((t - 0.22f) / 0.54f) * Mathf.PI * 6);
        for (int i = 0; i < ossos.Length; i++)
        {
            if (ossos[i] == null) continue;
            rotacoes[i] = ossos[i].localRotation;
            float angulo = i == 0 ? -5 : i == 1 ? 12 + sacudida * 3 :
                i == 2 ? -48 - sacudida * 7 : i == 3 ? 48 + sacudida * 7 : sacudida * 16;
            Vector3 eixo = i == 4 ? transform.up : i < 2 ? transform.right : transform.forward;
            ossos[i].rotation = Quaternion.AngleAxis(angulo * peso, eixo) * ossos[i].rotation;
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
