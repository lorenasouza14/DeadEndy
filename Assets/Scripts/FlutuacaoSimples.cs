using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FlutuacaoSimples : MonoBehaviour
{
    [Header("Configura��es da �gua")]
    [Tooltip("Arraste o objeto/plano da �gua aqui para rastrear a altura (Y) dinamicamente.")]
    public Transform planoDaAgua;

    [Header("F�sica de Flutua��o")]
    public float forcaFlutuacao = 15f;
    public float atritoNaAgua = 3f; // Desacelera o objeto debaixo d'�gua para n�o quicar infinitamente
    public float atritoAngularNaAgua = 1f;

    private Rigidbody rb;
    private float atritoOriginal;
    private float atritoAngularOriginal;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        atritoOriginal = rb.linearDamping;
        atritoAngularOriginal = rb.angularDamping;
    }

    void FixedUpdate()
    {
        if (planoDaAgua == null) return;

        // Verifica se o objeto est� abaixo do n�vel da �gua
        if (transform.position.y < planoDaAgua.position.y)
        {
            // Calcula a profundidade para aplicar uma for�a proporcional
            float profundidade = planoDaAgua.position.y - transform.position.y;

            // Empurra o objeto para cima (Vector3.up)
            rb.AddForce(Vector3.up * forcaFlutuacao * profundidade, ForceMode.Acceleration);

            // Aplica o atrito da �gua para estabilizar a flutua��o e evitar que a t�bua saia voando
            rb.linearDamping = atritoNaAgua;
            rb.angularDamping = atritoAngularNaAgua;
        }
        else
        {
            // Retorna ao atrito normal do ar quando a madeira estiver fora da �gua
            rb.linearDamping = atritoOriginal;
            rb.angularDamping = atritoAngularOriginal;
        }
    }
}