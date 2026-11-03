using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    public partial class ThirdPersonController
    {
        [Header("Esquiva lateral - AA / DD")]
        [Tooltip("Tempo maximo, em segundos reais, entre os dois toques.")]
        [Min(0.01f)] public float DodgeDoubleTapWindow = 0.25f;

        [Tooltip("Distancia da esquiva em unidades Unity, em terreno livre.")]
        [Min(0.01f)] public float DodgeDistance = 3f;

        [Tooltip("Duracao da esquiva, em segundos de jogo.")]
        [Min(0.01f)] public float DodgeDuration = 0.22f;

        [Tooltip("Espera depois do fim da esquiva, em segundos de jogo.")]
        [Min(0f)] public float DodgeCooldown = 0.5f;

        public bool IsDodging => _dodgeTimeLeft > 0f;

        private int _dodgeLastSide;
        private float _dodgeLastTapAt = float.NegativeInfinity;
        private float _dodgeTimeLeft;
        private float _dodgeSpeed;
        private float _dodgeNextAllowedAt;
        private Vector3 _dodgeDirection;
        private bool _dodgeResetSpeed;

        // Chamar depois de GroundedCheck e antes de JumpAndGravity.
        private void ReadDodgeInput()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            var moveAction = _playerInput != null && _playerInput.currentActionMap != null
                ? _playerInput.currentActionMap.FindAction("Move", false)
                : null;

            if (!Application.isFocused || keyboard == null ||
                _playerInput == null || !_playerInput.isActiveAndEnabled ||
                !_playerInput.inputIsActive || moveAction == null || !moveAction.enabled)
            {
                CancelDodge();
                return;
            }

            // Uma pausa congela a esquiva e descarta toques feitos no menu.
            if (Time.timeScale <= 0f)
            {
                ClearDodgeTaps();
                if (IsDodging) _input.jump = false;
                return;
            }

            if (IsDodging)
            {
                _input.jump = false;
                ClearDodgeTaps();
                return;
            }

            // Esquiva comeca no chao. A gravidade continua se sair de uma borda.
            if (!Grounded || _verticalVelocity > 0f || Time.time < _dodgeNextAllowedAt)
            {
                ClearDodgeTaps();
                return;
            }

            bool left = keyboard.aKey.wasPressedThisFrame;
            bool right = keyboard.dKey.wasPressedThisFrame;
            if (left && right)
            {
                ClearDodgeTaps();
                return;
            }
            if (!left && !right) return;

            int side = left ? -1 : 1;
            float now = Time.unscaledTime;
            if (_dodgeLastSide == side &&
                now - _dodgeLastTapAt <= Mathf.Max(0.01f, DodgeDoubleTapWindow))
            {
                ClearDodgeTaps();
                if (_mainCamera == null) return;

                Vector3 lateral = Vector3.ProjectOnPlane(_mainCamera.transform.right, Vector3.up);
                if (lateral.sqrMagnitude < 0.0001f) return;

                // Captura a direcao uma vez: girar a camera nao curva a esquiva.
                _dodgeDirection = lateral.normalized * side;
                _dodgeTimeLeft = Mathf.Max(0.01f, DodgeDuration);
                _dodgeSpeed = Mathf.Max(0.01f, DodgeDistance) / _dodgeTimeLeft;
                _dodgeNextAllowedAt = Time.time + _dodgeTimeLeft + Mathf.Max(0f, DodgeCooldown);
                _speed = 0f;
                _animationBlend = 0f;
                _rotationVelocity = 0f;
                _input.jump = false;
            }
            else
            {
                // A-D-A nao conta como AA; o lado mais recente substitui o anterior.
                _dodgeLastSide = side;
                _dodgeLastTapAt = now;
            }
#endif
        }

        // Chamar no INICIO de Move. Retorna true quando ja moveu neste quadro.
        private bool MoveDodge()
        {
            if (!IsDodging) return false;
            if (Time.deltaTime <= 0f) return true;

            float step = Mathf.Min(Time.deltaTime, _dodgeTimeLeft);
            Vector3 motion = _dodgeDirection * (_dodgeSpeed * step);
            motion += Vector3.up * (_verticalVelocity * Time.deltaTime);
            _controller.Move(motion);

            _dodgeTimeLeft = Mathf.Max(0f, _dodgeTimeLeft - step);
            if (!IsDodging) _dodgeResetSpeed = true;

            // Mantem os parametros existentes; animacao de esquiva pode ser ligada depois.
            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, 0f);
                _animator.SetFloat(_animIDMotionSpeed, 0f);
            }
            return true;
        }

        // Evita que a aceleracao normal reutilize a alta velocidade da esquiva.
        private float GetHorizontalSpeedAfterDodge()
        {
            if (_dodgeResetSpeed)
            {
                _dodgeResetSpeed = false;
                _speed = 0f;
                _animationBlend = 0f;
                return 0f;
            }
            Vector3 velocity = _controller.velocity;
            return new Vector3(velocity.x, 0f, velocity.z).magnitude;
        }

        private void ClearDodgeTaps()
        {
            _dodgeLastSide = 0;
            _dodgeLastTapAt = float.NegativeInfinity;
        }

        private void CancelDodge()
        {
            _dodgeResetSpeed |= IsDodging;
            _dodgeTimeLeft = 0f;
            ClearDodgeTaps();
        }

        private void OnDisable()
        {
            CancelDodge();
        }
    }
}
