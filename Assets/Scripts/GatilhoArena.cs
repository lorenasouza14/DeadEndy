using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class GatilhoArena : MonoBehaviour
{
    [Header("Parede que vai bloquear a saída")]
    public GameObject paredeEntrada;
    [SerializeField] CharacterController jogador;
    [SerializeField] GerenciadorPuzzleTriangulo tochas;
    [SerializeField] CaranguejoChefe chefe;
    BoxCollider area;

    void Awake()
    {
        area = GetComponent<BoxCollider>();
        if (jogador == null)
        {
            var endy = GameObject.FindGameObjectWithTag("Player");
            if (endy != null) jogador = endy.GetComponent<CharacterController>();
        }
    }

    public bool ContemOuEstaPerto(Vector3 ponto, float margem)
    {
        return isActiveAndEnabled && area.enabled &&
            (area.ClosestPoint(ponto) - ponto).sqrMagnitude <= margem * margem;
    }

    void LateUpdate()
    {
        if (chefe != null && chefe.estadoAtual == CaranguejoChefe.EstadoChefe.Derrotado)
        {
            RestaurarArenaConcluida();
            return;
        }
        if (paredeEntrada == null || jogador == null || !jogador.enabled || !jogador.gameObject.activeInHierarchy ||
            (tochas != null && !tochas.Concluido) || !ContemOuEstaPerto(jogador.bounds.center, 0.001f)) return;

        AutosaveEndy.Instancia?.EntrarNaArena();
        paredeEntrada.SetActive(true);
        gameObject.SetActive(false);
    }

    public void RestaurarArenaConcluida()
    {
        if (paredeEntrada != null) paredeEntrada.SetActive(false);
        gameObject.SetActive(false);
    }
}
