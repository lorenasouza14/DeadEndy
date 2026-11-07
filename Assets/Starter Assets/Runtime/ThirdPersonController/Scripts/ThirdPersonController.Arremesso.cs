using UnityEngine;

namespace StarterAssets
{
    public partial class ThirdPersonController
    {
        public bool EstaArremessado => _knockbackTimeLeft > 0f;
        private Vector3 _knockbackVelocity;
        private float _knockbackTimeLeft;
        private float _knockbackDrag;
        private bool _catapultActive;
        private float _catapultGravity;

        // Usa a mesma capsula e gravidade do movimento normal; nao precisa de Rigidbody.
        public void ReceberArremesso(Vector3 direcao, float velocidade, float impulsoVertical,
            float duracao, float desaceleracao)
        {
            if (!isActiveAndEnabled) return;
            if (_controller == null) _controller = GetComponent<CharacterController>();
            if (_controller == null || !_controller.enabled) return;

            CancelDodge();
            _catapultActive = false;
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

        // Calcula um arco que termina no destino, mantendo as colisoes da capsula.
        public bool ReceberCatapulta(Vector3 destino, float alturaDoArco)
        {
            if (!isActiveAndEnabled || EstaArremessado) return false;
            if (_controller == null) _controller = GetComponent<CharacterController>();
            if (_controller == null || !_controller.enabled) return false;

            float gravidade = Mathf.Max(1f, Mathf.Abs(Gravity));
            float topo = Mathf.Max(transform.position.y, destino.y) + Mathf.Max(1f, alturaDoArco);
            float velocidadeVertical = Mathf.Sqrt(2f * gravidade * (topo - transform.position.y));
            float duracao = velocidadeVertical / gravidade + Mathf.Sqrt(2f * (topo - destino.y) / gravidade);
            Vector3 distancia = Vector3.ProjectOnPlane(destino - transform.position, Vector3.up);
            ReceberArremesso(distancia, distancia.magnitude / duracao, velocidadeVertical, duracao, 0f);
            _catapultActive = true;
            _catapultGravity = -gravidade;
            return true;
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
            if (_catapultActive) return MoveCatapult(delta);
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

        private bool MoveCatapult(float delta)
        {
            float step = Mathf.Min(delta, _knockbackTimeLeft);
            // Integracao exata do arco para o destino ser igual em diferentes taxas de quadros.
            Vector3 displacement = _knockbackVelocity * step + Vector3.up *
                (_verticalVelocity * step + 0.5f * _catapultGravity * step * step);
            CollisionFlags flags = _controller.Move(displacement);
            _verticalVelocity += _catapultGravity * step;
            _knockbackTimeLeft = Mathf.Max(0f, _knockbackTimeLeft - step);
            _dodgeResetSpeed = true;
            Grounded = (flags & CollisionFlags.Below) != 0 && _verticalVelocity < 0f;
            if ((flags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f) _verticalVelocity = 0f;
            if (Grounded || _knockbackTimeLeft <= 0f) CancelKnockback();

            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, 0f);
                _animator.SetFloat(_animIDMotionSpeed, 0f);
                _animator.SetBool(_animIDGrounded, Grounded);
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
            _catapultActive = false;
        }
    }
}
