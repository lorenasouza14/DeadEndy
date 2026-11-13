using UnityEngine;

namespace StarterAssets
{
    public partial class ThirdPersonController
    {
        [Header("Dano de queda")]
        public bool receberDanoDeQueda = true;
        [Tooltip("Distancia vertical segura, medida do ponto mais alto ate o pouso. Em unidades do mundo.")]
        [Min(0f)] public float alturaSeguraQueda = 12f;
        [Tooltip("Pontos de vida por unidade alem da altura segura. Fracoes tambem contam.")]
        [Min(0f)] public float danoPorUnidadeQueda = 0.25f;

        public float UltimaAlturaQueda { get; private set; }
        public float UltimoDanoQueda { get; private set; }
        private bool quedaPreparada;
        private float maiorAlturaQueda;
        private Vector3 ultimaPosicaoQueda;
        public event System.Action<float> AoSofrerDanoDeQueda;

        // Chamar depois de teletransportar/reaparecer para nao herdar a queda anterior.
        public void ReiniciarMedicaoQueda()
        {
            quedaPreparada = false;
            UltimaAlturaQueda = UltimoDanoQueda = 0f;
        }

        public float CalcularDanoQueda(float altura)
        {
            return receberDanoDeQueda ? Mathf.Max(0f, altura - Mathf.Max(0f, alturaSeguraQueda)) *
                Mathf.Max(0f, danoPorUnidadeQueda) : 0f;
        }

        private void PrepararMedicaoQueda()
        {
            if (_controller == null || !_controller.enabled || !receberDanoDeQueda)
            { ReiniciarMedicaoQueda(); return; }
            // Deslocamentos externos entre Updates (teleporte) nao sao uma queda fisica.
            if (!quedaPreparada || (transform.position - ultimaPosicaoQueda).sqrMagnitude > 0.01f)
            {
                maiorAlturaQueda = transform.position.y;
                quedaPreparada = true;
            }
            maiorAlturaQueda = Mathf.Max(maiorAlturaQueda, transform.position.y);
        }

        private void ConcluirMedicaoQueda()
        {
            if (!quedaPreparada || _controller == null || !_controller.enabled) return;
            maiorAlturaQueda = Mathf.Max(maiorAlturaQueda, transform.position.y);
            ultimaPosicaoQueda = transform.position;
            // Usa o contato real da capsula apos Move; a esfera Grounded antecipa o chao.
            // Vale tambem para esquiva, arremesso e arco das spring traps.
            if (!_controller.isGrounded || _verticalVelocity > 0f) return;
            float altura = Mathf.Max(0f, maiorAlturaQueda - transform.position.y);
            maiorAlturaQueda = transform.position.y; // Consome o pouso antes de aplicar dano/morte.
            if (altura <= 0.1f) return; // Pequenas acomodacoes da capsula no piso.
            UltimaAlturaQueda = altura;
            UltimoDanoQueda = CalcularDanoQueda(altura);
            if (UltimoDanoQueda > 0f) AoSofrerDanoDeQueda?.Invoke(UltimoDanoQueda);
        }
    }
}
