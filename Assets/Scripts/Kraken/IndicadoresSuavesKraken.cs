using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using System.Collections.Generic;

public static class IndicadoresSuavesKraken
{
    static readonly HashSet<LineRenderer> aplicados=new HashSet<LineRenderer>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar(){aplicados.Clear();SceneManager.sceneLoaded-=AoCarregar;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar(){AplicarNaCena();SceneManager.sceneLoaded-=AoCarregar;SceneManager.sceneLoaded+=AoCarregar;}
    static void AoCarregar(Scene cena,LoadSceneMode modo){AplicarNaCena();}
    static void AplicarNaCena()
    {
        foreach(var linha in Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include))Aplicar(linha);
    }
    public static void Aplicar(LineRenderer linha)
    {
        if(!linha||!linha.sharedMaterial||linha.sharedMaterial.shader.name!="Kraken/IndicadorSuave"||!aplicados.Add(linha))return;
        // A largura adicional comporta a borda difusa, conservando o centro legivel.
        linha.widthMultiplier*=3.6f;
        linha.numCornerVertices=6;linha.numCapVertices=6;
        linha.textureMode=LineTextureMode.Stretch;
        linha.shadowCastingMode=ShadowCastingMode.Off;linha.receiveShadows=false;
    }
}
