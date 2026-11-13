using UnityEngine;

// O controlador do Endy se move em Update; esses contatos não dependem de OnCollisionEnter.
[RequireComponent(typeof(CharacterController))]
public class ContatoChefeEndy : MonoBehaviour
{
    void OnControllerColliderHit(ControllerColliderHit contato)
    {
        var chefe = contato.collider.GetComponentInParent<CaranguejoChefe>();
        if (chefe != null) chefe.RegistrarContatoEndy(gameObject);
    }
}
