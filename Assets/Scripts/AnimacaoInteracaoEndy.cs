using UnityEngine;

// Usa o gesto que já foi escolhido no Animator, sem substituir clipes ou poses.
public static class AnimacaoInteracaoEndy
{
    public static bool Tocar(InventarioEndy endy, Animator alternativa = null)
    {
        if (Time.timeScale == 0) return false;
        if (endy != null)
        {
            var vida = endy.GetComponent<VidaEndy>();
            if (!endy.isActiveAndEnabled || endy.ExecutandoAcao || (vida != null && vida.EstaMorto)) return false;
        }
        var anim = endy != null ? endy.animEndy : alternativa;
        if (anim == null && endy != null)
        {
            var movimento = endy.GetComponent<StarterAssets.ThirdPersonController>();
            anim = movimento != null ? movimento.CharacterAnimator : null;
            if (anim == null) anim = endy.GetComponentInChildren<Animator>();
        }
        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return false;
        foreach (var parametro in anim.parameters)
        {
            if (parametro.name != "Interagir" || parametro.type != AnimatorControllerParameterType.Trigger) continue;
            anim.ResetTrigger("Interagir");
            anim.SetTrigger("Interagir");
            return true;
        }
        return false;
    }
}
