using UnityEngine;
namespace StarterAssets
{
    public partial class ThirdPersonController
    {
        // O personagem persiste; cada fase possui uma nova camera.
        public void PrepararTrocaDeMundo(Camera cameraPrincipal)
        {
            _mainCamera = cameraPrincipal != null ? cameraPrincipal.gameObject : null;
            _verticalVelocity = 0f;
            _speed = 0f;
            _animationBlend = 0f;
            _rotationVelocity = 0f;
            _targetRotation = transform.eulerAngles.y;
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;
            Grounded = false;
            CancelDodge();
            _dodgeResetSpeed = true;
            _cinemachineTargetYaw = transform.eulerAngles.y;
            _cinemachineTargetPitch = 0f;
            var entrada = GetComponent<StarterAssetsInputs>();
            if (entrada != null)
            {
                entrada.move = Vector2.zero;
                entrada.look = Vector2.zero;
                entrada.jump = false;
                entrada.sprint = false;
            }
            if (CinemachineCameraTarget != null)
                CinemachineCameraTarget.transform.rotation = Quaternion.Euler(0f, _cinemachineTargetYaw, 0f);
        }
    }
}
