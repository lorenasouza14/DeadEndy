using UnityEngine;
using StarterAssets;

public static class SonsPassosEndy
{
    public static void Tocar(ThirdPersonController jogador, float volume)
    {
        if (!jogador || !jogador.Grounded || EfeitosSonoros.Suspenso) return;
        var capsula = jogador.GetComponent<CharacterController>();
        if (!capsula || !capsula.enabled || new Vector2(capsula.velocity.x, capsula.velocity.z).sqrMagnitude < .01f) return;
        Vector3 pe = new Vector3(capsula.bounds.center.x, capsula.bounds.min.y, capsula.bounds.center.z);
        if (!Physics.Raycast(pe + Vector3.up * .3f, Vector3.down, out var hit, .9f,
            jogador.GroundLayers, QueryTriggerInteraction.Ignore)) return;
        string superficie = Identificar(hit);
        EfeitosSonoros.Tocar("Passos" + superficie, pe, volume, false, .32f);
    }
    public static string Classificar(string nome)
    {
        nome = nome.ToLowerInvariant();
        if (nome.Contains("wood") || nome.Contains("madeira") || nome.Contains("plank") || nome.Contains("bridge") || nome.Contains("ponte") || nome.Contains("dock")) return "Madeira";
        if (nome.Contains("sand") || nome.Contains("areia") || nome.Contains("beach") || nome.Contains("praia")) return "Areia";
        if (nome.Contains("grass") || nome.Contains("grama") || nome.Contains("foliage") || nome.Contains("moss")) return "Grama";
        if (nome.Contains("rock") || nome.Contains("stone") || nome.Contains("pedra") || nome.Contains("ruin") || nome.Contains("cave") || nome.Contains("caverna") || nome.Contains("tile")) return "Pedra";
        return null;
    }
    static string Identificar(RaycastHit hit)
    {
        var terreno = hit.collider.GetComponent<Terrain>();
        if (terreno && terreno.terrainData)
        {
            var dados = terreno.terrainData;
            Vector3 local = hit.point - terreno.transform.position;
            int x = Mathf.Clamp(Mathf.FloorToInt(local.x / dados.size.x * dados.alphamapWidth), 0, dados.alphamapWidth - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(local.z / dados.size.z * dados.alphamapHeight), 0, dados.alphamapHeight - 1);
            var pesos = dados.GetAlphamaps(x, z, 1, 1);
            int maior = 0;
            for (int i = 1; i < pesos.GetLength(2); i++) if (pesos[0,0,i] > pesos[0,0,maior]) maior = i;
            var camadas = dados.terrainLayers;
            if (maior < camadas.Length && camadas[maior])
            {
                var tipo = Classificar(camadas[maior].name);
                if (tipo != null) return tipo;
            }
        }
        for (Transform t = hit.collider.transform; t != null; t = t.parent)
        {
            var tipo = Classificar(t.name);
            if (tipo != null) return tipo;
            var visual = t.GetComponent<Renderer>();
            if (!visual) continue;
            foreach (var material in visual.sharedMaterials)
            {
                if (!material) continue;
                tipo = Classificar(material.name);
                if (tipo != null) return tipo;
            }
        }
        return hit.collider.gameObject.scene.name.Contains("Cave") || hit.collider.gameObject.scene.name.Contains("Kraken") ? "Pedra" : "Grama";
    }
}
