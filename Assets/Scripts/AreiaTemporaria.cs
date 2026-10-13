using UnityEngine;

public class AreiaTemporaria : MonoBehaviour
{
    [Header("Ajuste no Chão")]
    public float tempoDeVida = 20f;
    public float alturaExtra = 0.08f; // Aumente se ainda ficar um pouco enterrada (ex: 0.15f)
    public bool acompanharInclinacaoDoChao = true;

    void Start()
    {
        AlinharComOChao();
        Destroy(gameObject, tempoDeVida);
    }

    void AlinharComOChao()
    {
        // Dispara um raio de 3 metros acima da poça apontando para baixo
        RaycastHit[] hits = Physics.RaycastAll(transform.position + Vector3.up * 3f, Vector3.down, 10f);

        foreach (RaycastHit hit in hits)
        {
            // Ignora outras poças (Triggers), o Endy e o próprio Caranguejo
            if (!hit.collider.isTrigger &&
                !hit.collider.CompareTag("Player") &&
                hit.collider.GetComponentInParent<CaranguejoChefe>() == null)
            {
                // Coloca a poça exatamente em cima da areia + a altura extra
                transform.position = hit.point + Vector3.up * alturaExtra;

                // Inclina a poça suavemente se o chão for uma rampa/duna
                if (acompanharInclinacaoDoChao)
                {
                    transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * transform.rotation;
                }
                break;
            }
        }
    }
}