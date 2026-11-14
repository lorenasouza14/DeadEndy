using UnityEngine;
namespace StarterAssets
{
    public partial class ThirdPersonController
    {
        public float MultiplicadorAgua { get; private set; } = 1f;
        public void DefinirMultiplicadorAgua(float valor) { MultiplicadorAgua = Mathf.Clamp(valor, .1f, 1f); }
    }
}
