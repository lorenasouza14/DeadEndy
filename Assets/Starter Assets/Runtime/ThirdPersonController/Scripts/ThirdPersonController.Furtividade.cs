using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    public partial class ThirdPersonController
    {
        [Header("Caminhada lenta - segure Ctrl")]
        public bool permitirCaminhadaLenta = true;
        [Tooltip("Limite em m/s ao segurar Ctrl ou o botao superior direito do controle. Nao recebe bonus da pocao.")]
        [Min(0.1f)] public float velocidadeCaminhadaLenta = 1f;
        public float VelocidadeHorizontalAtual => _controller != null && _controller.enabled
            ? Vector3.ProjectOnPlane(_controller.velocity, Vector3.up).magnitude : 0f;
        public bool EstaSubindoNoPulo => !Grounded && _verticalVelocity > 0.1f;

        public bool CaminhadaLentaSolicitada
        {
            get
            {
                if (!permitirCaminhadaLenta || Time.timeScale <= 0f) return false;
#if ENABLE_INPUT_SYSTEM
                if (_playerInput == null || !_playerInput.isActiveAndEnabled || !_playerInput.inputIsActive) return false;
                var mover = _playerInput.currentActionMap?.FindAction("Move", false);
                if (mover == null || !mover.enabled) return false;
                if (IsCurrentDeviceMouse)
                    return Keyboard.current != null && (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed);
                return Gamepad.current != null && Gamepad.current.rightShoulder.isPressed;
#else
                return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
#endif
            }
        }

        private float LimitarVelocidadeFurtiva(float velocidade)
        {
            return CaminhadaLentaSolicitada ? Mathf.Min(velocidade, velocidadeCaminhadaLenta) : velocidade;
        }
    }
}
