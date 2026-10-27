using UnityEngine;

namespace StarterAssets
{
    public partial class ThirdPersonController
    {
        [Header("Animacao do deslize")]
        [Tooltip("Quanto o quadril abaixa, como fracao do comprimento da perna.")]
        [Range(0f, 0.5f)] public float DodgeCrouch = 0.32f;
        [Tooltip("Inclinacao do corpo na direcao do deslize, em graus.")]
        [Range(0f, 35f)] public float DodgeLean = 18f;

        private Animator _dodgePoseAnimator;
        private Transform[] _dodgeBones;
        private Quaternion[] _dodgeBoneRotations;
        private Vector3 _dodgeHipsPosition;
        private bool _dodgePoseApplied;
        private float _dodgeAnimationDuration;

        private void PrepareDodgeAnimation()
        {
            _dodgeAnimationDuration = _dodgeTimeLeft;
            if (_animator == null) return;
            if (_dodgePoseAnimator == _animator && _dodgeBones != null) return;
            _dodgePoseAnimator = _animator;
            // Humanoid e o esqueleto KayKit generico usam a mesma pose.
            _dodgeBones = new[]
            {
                DodgeBone(HumanBodyBones.Hips, "pelvis"),
                DodgeBone(HumanBodyBones.LeftUpperLeg, "upperleg.l"),
                DodgeBone(HumanBodyBones.LeftLowerLeg, "lowerleg.l"),
                DodgeBone(HumanBodyBones.LeftFoot, "foot.l"),
                DodgeBone(HumanBodyBones.RightUpperLeg, "upperleg.r"),
                DodgeBone(HumanBodyBones.RightLowerLeg, "lowerleg.r"),
                DodgeBone(HumanBodyBones.RightFoot, "foot.r")
            };
            _dodgeBoneRotations = new Quaternion[_dodgeBones.Length];
        }

        private Transform DodgeBone(HumanBodyBones humanoid, string boneName)
        {
            if (_animator.isHuman)
            {
                Transform bone = _animator.GetBoneTransform(humanoid);
                if (bone != null) return bone;
            }
            foreach (Transform bone in _animator.GetComponentsInChildren<Transform>(true))
                if (bone.name == boneName) return bone;
            return null;
        }

        private void AnimateDodge()
        {
            if (!IsDodging || _dodgeAnimationDuration <= 0f) return;
            float progress = 1f - _dodgeTimeLeft / _dodgeAnimationDuration;
            float enter = Mathf.SmoothStep(0f, 1f, progress / 0.2f);
            float exit = Mathf.SmoothStep(0f, 1f, (1f - progress) / 0.35f);
            ApplyDodgePose(enter * exit);
        }

        private void ApplyDodgePose(float weight)
        {
            // Restaura antes de reaplicar, inclusive durante pausa e Animator culling.
            RestoreDodgePose();
            if (weight <= 0f || _dodgeBones == null || _animator == null || !_animator.isActiveAndEnabled) return;
            foreach (Transform bone in _dodgeBones) if (bone == null) return;

            Transform hips = _dodgeBones[0];
            _dodgeHipsPosition = hips.localPosition;
            for (int i = 0; i < _dodgeBones.Length; i++)
                _dodgeBoneRotations[i] = _dodgeBones[i].localRotation;
            _dodgePoseApplied = true;

            Vector3 leftFoot = _dodgeBones[3].position;
            Vector3 rightFoot = _dodgeBones[6].position;
            Quaternion leftRotation = _dodgeBones[3].rotation;
            Quaternion rightRotation = _dodgeBones[6].rotation;
            float legLength = Vector3.Distance(_dodgeBones[1].position, _dodgeBones[2].position) +
                              Vector3.Distance(_dodgeBones[2].position, leftFoot);
            Vector3 up = transform.up;
            Vector3 direction = Vector3.ProjectOnPlane(_dodgeDirection, up).normalized;
            Vector3 leanAxis = Vector3.Cross(up, direction);
            hips.position -= up * (legLength * DodgeCrouch * weight);
            hips.rotation = Quaternion.AngleAxis(DodgeLean * weight, leanAxis) * hips.rotation;

            // Os pes deslizam com o jogador, mas permanecem na altura original do chao.
            // A base abre para o lado da esquiva e os joelhos dobram para a frente.
            Vector3 stance = transform.right * (legLength * 0.12f * weight);
            SolveDodgeLeg(1, leftFoot - stance, leftRotation);
            SolveDodgeLeg(4, rightFoot + stance, rightRotation);
        }

        private void SolveDodgeLeg(int index, Vector3 target, Quaternion footRotation)
        {
            Transform thigh = _dodgeBones[index];
            Transform shin = _dodgeBones[index + 1];
            Transform foot = _dodgeBones[index + 2];
            float a = Vector3.Distance(thigh.position, shin.position);
            float b = Vector3.Distance(shin.position, foot.position);
            Vector3 direction = target - thigh.position;
            if (a < 0.0001f || b < 0.0001f || direction.sqrMagnitude < 0.000001f) return;
            float distance = Mathf.Clamp(direction.magnitude, Mathf.Abs(a - b) + 0.0001f, a + b - 0.0001f);
            direction.Normalize();
            Vector3 bend = Vector3.ProjectOnPlane(transform.forward, direction).normalized;
            if (bend.sqrMagnitude < 0.001f) bend = Vector3.ProjectOnPlane(transform.up, direction).normalized;
            float x = (a * a - b * b + distance * distance) / (2f * distance);
            Vector3 knee = thigh.position + direction * x + bend * Mathf.Sqrt(Mathf.Max(0f, a * a - x * x));
            Vector3 ankle = thigh.position + direction * distance;
            thigh.rotation = Quaternion.FromToRotation(shin.position - thigh.position, knee - thigh.position) * thigh.rotation;
            shin.rotation = Quaternion.FromToRotation(foot.position - shin.position, ankle - shin.position) * shin.rotation;
            foot.rotation = footRotation;
        }

        private void RestoreDodgePose()
        {
            if (!_dodgePoseApplied) return;
            if (_dodgeBones[0] != null) _dodgeBones[0].localPosition = _dodgeHipsPosition;
            for (int i = 0; i < _dodgeBones.Length; i++)
                if (_dodgeBones[i] != null) _dodgeBones[i].localRotation = _dodgeBoneRotations[i];
            _dodgePoseApplied = false;
        }
    }
}
