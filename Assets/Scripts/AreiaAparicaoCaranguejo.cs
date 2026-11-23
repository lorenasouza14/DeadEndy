using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Efeito independente do chefe: a areia permanece no ponto onde ele emergiu.
public sealed class AreiaAparicaoCaranguejo : MonoBehaviour
{
    Material material;

    public static void Emitir(CaranguejoChefe chefe)
    {
        var shader = Resources.Load<Shader>("AreiaAparicao");
        if (!shader) return;
        var limites = new Bounds(chefe.transform.position, Vector3.one * 2f);
        bool encontrou = false;
        foreach (var visual in chefe.GetComponentsInChildren<Renderer>())
        {
            if (visual is ParticleSystemRenderer) continue;
            if (!encontrou) { limites = visual.bounds; encontrou = true; }
            else limites.Encapsulate(visual.bounds);
        }
        float raio = Mathf.Clamp(Mathf.Max(limites.extents.x, limites.extents.z) * .65f, .6f, 3f);
        Vector3 posicao = new Vector3(limites.center.x, limites.min.y + .08f, limites.center.z);
        Vector3 origem = new Vector3(posicao.x, limites.max.y + 2f, posicao.z);
        float distancia = float.PositiveInfinity;
        foreach (var hit in Physics.RaycastAll(origem, Vector3.down, limites.size.y + 8f,
            Physics.DefaultRaycastLayers & ~(1 << 4), QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(chefe.transform) || hit.normal.y < .5f ||
                hit.collider.GetComponentInParent<InventarioEndy>() || hit.distance >= distancia) continue;
            distancia = hit.distance;
            posicao = hit.point + Vector3.up * .08f;
        }
        var objeto = new GameObject("Areia da aparicao do caranguejo");
        SceneManager.MoveGameObjectToScene(objeto, chefe.gameObject.scene);
        objeto.transform.position = posicao;
        var efeito = objeto.AddComponent<AreiaAparicaoCaranguejo>();
        efeito.material = new Material(shader);
        efeito.CriarParticulas("Graos de areia", raio, false);
        efeito.CriarParticulas("Poeira de areia", raio, true);
        Destroy(objeto, 3.5f);
    }

    void CriarParticulas(string nome, float raio, bool poeira)
    {
        var objeto = new GameObject(nome);
        objeto.transform.SetParent(transform, false);
        // O cone do ParticleSystem emite no eixo Z local; aponta para cima.
        objeto.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        var particulas = objeto.AddComponent<ParticleSystem>();
        particulas.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var principal = particulas.main;
        principal.playOnAwake = false;
        principal.loop = false;
        principal.duration = .55f;
        principal.simulationSpace = ParticleSystemSimulationSpace.World;
        principal.maxParticles = poeira ? 50 : 120;
        principal.startLifetime = new ParticleSystem.MinMaxCurve(poeira ? 1.2f : .45f, poeira ? 2.2f : .9f);
        principal.startSpeed = new ParticleSystem.MinMaxCurve(poeira ? .6f : 2f, poeira ? 1.5f : 4f);
        principal.startSize = new ParticleSystem.MinMaxCurve(poeira ? raio * .4f : .035f, poeira ? raio * .9f : .10f);
        principal.startColor = new ParticleSystem.MinMaxGradient(
            new Color(.59f, .40f, .18f, poeira ? .28f : 1f),
            new Color(.88f, .70f, .40f, poeira ? .4f : 1f));
        principal.gravityModifier = poeira ? -.03f : .85f;
        var emissao = particulas.emission;
        emissao.rateOverTime = 0;
        emissao.SetBursts(new[] {
            new ParticleSystem.Burst(0, (short)(poeira ? 24 : 75)),
            new ParticleSystem.Burst(.18f, (short)(poeira ? 12 : 30))
        });
        var forma = particulas.shape;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.radius = raio;
        forma.angle = poeira ? 65f : 45f;
        var cor = particulas.colorOverLifetime;
        cor.enabled = true;
        var gradiente = new Gradient();
        gradiente.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .08f),
                new GradientAlphaKey(.7f, .45f), new GradientAlphaKey(0, 1) });
        cor.color = gradiente;
        var tamanho = particulas.sizeOverLifetime;
        tamanho.enabled = poeira;
        tamanho.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, .4f, 1, 1.5f));
        var visual = particulas.GetComponent<ParticleSystemRenderer>();
        visual.sharedMaterial = material;
        visual.shadowCastingMode = ShadowCastingMode.Off;
        visual.receiveShadows = false;
        particulas.Play();
    }

    void OnDestroy() { if (material) Destroy(material); }
}
