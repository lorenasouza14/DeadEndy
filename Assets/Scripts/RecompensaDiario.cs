using UnityEngine;

// Fica em um objeto ativo, pois o chefe e a recompensa começam ocultos.
public class RecompensaDiario : MonoBehaviour
{
    public CaranguejoChefe chefe;
    public GameObject pilar;
    public BauDiario bau;
    public bool Revelada { get; private set; }

    void Awake()
    {
        if (pilar != null) pilar.SetActive(false);
        if (bau != null) bau.gameObject.SetActive(false);
    }

    void Update()
    {
        if (Revelada || chefe == null || !chefe.EncolhimentoConcluido) return;
        Revelada = true;
        if (pilar != null) pilar.SetActive(true);
        if (bau != null) bau.gameObject.SetActive(true);
    }

    public void RestaurarRecompensa(bool aberto, bool coletado)
    {
        Revelada = true;
        if (pilar != null) pilar.SetActive(true);
        if (bau != null)
        {
            bau.gameObject.SetActive(true);
            bau.RestaurarEstado(aberto, coletado);
        }
    }
}
