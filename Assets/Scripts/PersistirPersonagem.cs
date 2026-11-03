
using UnityEngine;

public class PersistirPersonagem : MonoBehaviour
{
    private static PersistirPersonagem instancia;

    void Awake()
    {
        if (instancia != null && instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        instancia = this;
        DontDestroyOnLoad(gameObject);
    }
}
