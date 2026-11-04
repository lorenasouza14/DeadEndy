using UnityEngine;

namespace StarterAssets
{
    public partial class ThirdPersonController
    {
        public bool EstaArremessado => _knockbackTimeLeft > 0f;
        private Vector3 _knockbackVelocity;
        private float _knockbackTimeLeft;
        private float _knockbackDrag;

        // Usa a mesma capsula e gravidade do movimento normal; nao precisa de Rigidbody.
        public void ReceberArremesso(Vector3 direcao, float velocidade, float impulsoVertical,
            float duracao, float desaceleracao)
        {
            if (!isActiveAndEnabled) return;
            if (_controller == null) _controller = GetComponent<CharacterController>();
            if (_controller == null || !_controller.enabled) return;

            CancelDodge();
            direcao = Vector3.ProjectOnPlane(direcao, Vector3.up);
            if (direcao.sqrMagnitude < 0.0001f) direcao = -transform.forward;
            _knockbackVelocity = direcao.normalized * Mathf.Max(0f, velocidade);
            _knockbackTimeLeft = Mathf.Max(0.01f, duracao);
            _knockbackDrag = Mathf.Max(0f, desaceleracao);
            _verticalVelocity = Mathf.Max(0f, impulsoVertical);
            _speed = _animationBlend = 0f;
            _rotationVelocity = 0f;
            Grounded = false;
            if (_input != null) _input.jump = false;
        }

        private bool MoveKnockback(float delta)
        {
            if (!EstaArremessado) return false;
            if (_controller == null || !_controller.enabled)
            {
                CancelKnockback();
                return false;
            }
            if (delta <= 0f) return true;

            if (_input != null) _input.jump = false;
            ClearDodgeTaps();
            if (Grounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
            _verticalVelocity = Mathf.Max(-_terminalVelocity, _verticalVelocity + Gravity * delta);

            float step = Mathf.Min(delta, _knockbackTimeLeft);
            Vector3 nextVelocity = Vector3.MoveTowards(_knockbackVelocity, Vector3.zero, _knockbackDrag * step);
            Vector3 displacement = (_knockbackVelocity + nextVelocity) * (0.5f * step);
            CollisionFlags flags = _controller.Move(displacement + Vector3.up * (_verticalVelocity * delta));
            if ((flags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f) _verticalVelocity = 0f;
            _knockbackVelocity = nextVelocity;
            _knockbackTimeLeft = Mathf.Max(0f, _knockbackTimeLeft - delta);
            // Nao deixa a aceleracao de caminhada herdar a velocidade do arremesso.
            _dodgeResetSpeed = true;

            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, 0f);
                _animator.SetFloat(_animIDMotionSpeed, 0f);
                _animator.SetBool(_animIDJump, false);
                _animator.SetBool(_animIDFreeFall, !Grounded && _verticalVelocity < 0f);
            }
            return true;
        }

        private void CancelKnockback()
        {
            if (EstaArremessado) _verticalVelocity = 0f;
            _knockbackTimeLeft = 0f;
            _knockbackVelocity = Vector3.zero;
        }
    }
}
