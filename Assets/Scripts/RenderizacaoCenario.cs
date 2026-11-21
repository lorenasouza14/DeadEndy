using UnityEngine;
using UnityEngine.SceneManagement;

// Ajusta apenas a geometria desenhada pelo Terrain. Nao desativa objetos,
// scripts, colisores, animadores ou os gerenciadores de spawn.
public static class RenderizacaoCenario
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Instalar()
    {
        SceneManager.sceneLoaded -= Aplicar;
        SceneManager.sceneLoaded += Aplicar;
    }

    static void Aplicar(Scene cena, LoadSceneMode modo)
    {
        foreach (var terreno in Terrain.activeTerrains)
        {
            if (terreno.gameObject.scene != cena) continue;
            terreno.treeDistance = Mathf.Min(terreno.treeDistance, 800f);
            terreno.detailObjectDistance = Mathf.Min(terreno.detailObjectDistance, 60f);
            terreno.heightmapPixelError = Mathf.Max(terreno.heightmapPixelError, 8f);
        }
    }
}
