using UnityEngine;

[RequireComponent(typeof(Light))]
public class LuzDaTocha : MonoBehaviour
{
    public float intensidade = 2.3f;
    [Range(0, 0.5f)] public float oscilacao = 0.16f;
    private Light luz;
    private float fase;

    void Awake()
    {
        luz = GetComponent<Light>();
        fase = Mathf.Abs(transform.position.x * 1.37f + transform.position.z * 0.73f);
    }

    void Update()
    {
        float ruido = Mathf.PerlinNoise(fase, Time.time * 7f) * 2f - 1f;
        luz.intensity = intensidade * (1f + ruido * oscilacao);
    }
}
