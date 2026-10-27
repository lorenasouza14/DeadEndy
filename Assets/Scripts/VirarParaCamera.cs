using UnityEngine;

public class VirarParaCamera : MonoBehaviour
{
    private Camera cam;

    void Start()
    {
        // Encontra a câmera principal do jogo automaticamente
        cam = Camera.main;
    }

    void LateUpdate()
    {
        if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
        if (cam != null)
        {
            // Truque matemático para a interface olhar para a câmera sem ficar espelhada
            transform.LookAt(transform.position + cam.transform.rotation * Vector3.forward,
                             cam.transform.rotation * Vector3.up);
        }
    }
}
