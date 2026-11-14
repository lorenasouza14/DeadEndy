using UnityEngine;
// O nado resolve em 100, a captura em 150 e o IK em 200.
// Prende o personagem a pele ja resolvida, evitando escorregar da ponta.
[DefaultExecutionOrder(250),DisallowMultipleComponent]
public class AcompanharPontaCapturaKraken : MonoBehaviour
{
 CapturaJogadorKraken captura;
 void Awake(){captura=GetComponent<CapturaJogadorKraken>();}
 void LateUpdate(){if(captura)captura.AcompanharPontaResolvida();}
}
