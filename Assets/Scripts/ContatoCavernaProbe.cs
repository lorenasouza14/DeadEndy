#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

// Instalado somente durante o teste no editor; nao faz parte do jogo publicado.
public class ContatoCavernaProbe : MonoBehaviour
{
    public Action<ControllerColliderHit> aoContato;
    readonly Dictionary<Collider, float> ultimoContato = new Dictionary<Collider, float>();
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.normal.y > 0.7f) return;
        if (ultimoContato.TryGetValue(hit.collider, out float tempo) && Time.time - tempo < 1f) return;
        ultimoContato[hit.collider] = Time.time;
        aoContato?.Invoke(hit);
    }
}
#endif
