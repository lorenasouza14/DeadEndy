using UnityEngine;
namespace StarterAssets {
 public class PlayerAnimationEvents : MonoBehaviour {
  public ThirdPersonController controller;
  public void OnFootstep(AnimationEvent e){if(controller)controller.SendMessage("OnFootstep",e,SendMessageOptions.DontRequireReceiver);}
  public void OnLand(AnimationEvent e){if(controller)controller.SendMessage("OnLand",e,SendMessageOptions.DontRequireReceiver);}
 }
}
