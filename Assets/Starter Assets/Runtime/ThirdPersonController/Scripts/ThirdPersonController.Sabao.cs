using UnityEngine;
using UnityEngine.SceneManagement;

namespace StarterAssets
{
    public partial class ThirdPersonController
    {
        float ensaboadoAte;
        Scene cenaDoSabao;
        GameObject espumaSabao;
        Material materialEspumaSabao;
        Transform[] bolinhasSabao, apoiosSabao;
        Vector3[] offsetsSabao;
        float[] tamanhosSabao;

        public bool Escorregadio => Time.time < ensaboadoAte
            && SceneManager.GetActiveScene() == cenaDoSabao;
        public float TempoRestanteSabao => Escorregadio ? ensaboadoAte - Time.time : 0f;

        public void AplicarSabao(float duracao)
        {
            if (!Escorregadio) ensaboadoAte = 0f;
            cenaDoSabao = SceneManager.GetActiveScene();
            ensaboadoAte = Mathf.Max(ensaboadoAte, Time.time + Mathf.Max(0f, duracao));
            if (Application.isPlaying && isActiveAndEnabled && Escorregadio)
                AtualizarEspumaSabao();
        }

        public void LimparSabao()
        {
            ensaboadoAte = 0f;
            RemoverEspumaSabao();
        }

        // Pouca aderencia para frear e virar; arrancar continua sendo possivel.
        // A velocidade real da capsula respeita paredes, sem alterar a gravidade.
        internal static Vector3 AplicarInerciaSabao(Vector3 atual, Vector3 desejada, float deltaTime)
        {
            atual.y = 0f; desejada.y = 0f;
            float aderencia = desejada.sqrMagnitude < .001f ? .22f
                : atual.sqrMagnitude > .01f && Vector3.Dot(atual.normalized, desejada.normalized) < .8f
                    ? .45f : 1.5f;
            return Vector3.Lerp(atual, desejada, 1f - Mathf.Exp(-aderencia * Mathf.Max(0f, deltaTime)));
        }

        void AtualizarEspumaSabao()
        {
            if (!Escorregadio) { RemoverEspumaSabao(); return; }
            if (!espumaSabao) CriarEspumaSabao();
            if (!espumaSabao) return;
            for (int i = 0; i < bolinhasSabao.Length; i++)
            {
                if (!bolinhasSabao[i]) continue;
                Transform apoio = apoiosSabao[i] ? apoiosSabao[i] : transform;
                Vector3 offset = offsetsSabao[i];
                offset.y += Mathf.Sin(Time.time * 2.2f + i * 1.7f) * .008f;
                bolinhasSabao[i].position = apoio.position + transform.TransformVector(offset);
                bolinhasSabao[i].localScale = Vector3.one * tamanhosSabao[i]
                    * (1f + .06f * Mathf.Sin(Time.time * 3f + i));
            }
        }

        void CriarEspumaSabao()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) shader = Shader.Find("Standard");
            if (!shader) return;
            materialEspumaSabao = new Material(shader) { name = "Espuma perolada do jogador" };
            materialEspumaSabao.SetColor("_BaseColor", new Color(.84f, .97f, 1f));
            materialEspumaSabao.SetColor("_Color", new Color(.84f, .97f, 1f));
            materialEspumaSabao.SetFloat("_Smoothness", .95f);
            materialEspumaSabao.SetFloat("_Glossiness", .95f);
            materialEspumaSabao.SetFloat("_Metallic", .12f);
            materialEspumaSabao.EnableKeyword("_EMISSION");
            materialEspumaSabao.SetColor("_EmissionColor", new Color(.12f, .18f, .2f));
            espumaSabao = new GameObject("Bolinhas de sabao - cabeca e ombros");
            espumaSabao.transform.SetParent(transform, false);
            bolinhasSabao = new Transform[15]; apoiosSabao = new Transform[15];
            offsetsSabao = new Vector3[15]; tamanhosSabao = new float[15];
            var animator = CharacterAnimator ? CharacterAnimator : GetComponentInChildren<Animator>();
            var capsula = GetComponent<CharacterController>();
            float altura = capsula ? capsula.height : 1.8f;
            float topo = capsula ? capsula.center.y + altura * .5f : altura;
            var bones = new[] { HumanBodyBones.Head, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm };
            for (int grupo = 0, i = 0; grupo < 3; grupo++)
            {
                Transform apoio = animator && animator.isHuman ? animator.GetBoneTransform(bones[grupo]) : null;
                Vector3 centro = apoio ? Vector3.zero : new Vector3(grupo == 0 ? 0 : grupo == 1 ? -.25f : .25f,
                    topo - (grupo == 0 ? .15f : .38f), 0);
                if (!apoio) apoio = transform;
                // O osso Head fica na base da cabeca; usa a malha real para achar o topo.
                if (grupo == 0)
                {
                    foreach (var skin in GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        if (!skin.name.ToLowerInvariant().Contains("head")) continue;
                        var mesh = new Mesh(); skin.BakeMesh(mesh);
                        var vertices = mesh.vertices;
                        if (vertices.Length > 0)
                        {
                            Vector3 primeiro = transform.InverseTransformPoint(skin.transform.TransformPoint(vertices[0]));
                            var bounds = new Bounds(primeiro, Vector3.zero);
                            foreach (var v in vertices) bounds.Encapsulate(transform.InverseTransformPoint(skin.transform.TransformPoint(v)));
                            centro = new Vector3(bounds.center.x, bounds.max.y - .14f, bounds.center.z)
                                - transform.InverseTransformPoint(apoio.position);
                        }
                        Destroy(mesh); break;
                    }
                }
                else centro += new Vector3(grupo == 1 ? -.16f : .16f, .12f, 0f);
                int quantidade = 5;
                for (int j = 0; j < quantidade; j++, i++)
                {
                    float angulo = j * 2.399963f;
                    float raio = grupo == 0 ? .19f : .10f;
                    offsetsSabao[i] = centro + new Vector3(Mathf.Cos(angulo) * raio,
                        (grupo == 0 ? .16f : .07f) + (j % 3) * .035f, Mathf.Sin(angulo) * raio);
                    apoiosSabao[i] = apoio;
                    tamanhosSabao[i] = .105f + (i % 4) * .027f;
                    var bolinha = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    bolinha.name = grupo == 0 ? "Espuma cabeca" : grupo == 1 ? "Espuma ombro esquerdo" : "Espuma ombro direito";
                    bolinha.transform.SetParent(espumaSabao.transform, false);
                    var colisor = bolinha.GetComponent<Collider>(); colisor.enabled = false; Destroy(colisor);
                    var renderer = bolinha.GetComponent<Renderer>();
                    renderer.sharedMaterial = materialEspumaSabao;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    bolinhasSabao[i] = bolinha.transform;
                }
            }
        }

        void RemoverEspumaSabao()
        {
            if (espumaSabao) { espumaSabao.SetActive(false); Destroy(espumaSabao); }
            if (materialEspumaSabao) Destroy(materialEspumaSabao);
            espumaSabao = null; materialEspumaSabao = null;
            bolinhasSabao = null; apoiosSabao = null; offsetsSabao = null; tamanhosSabao = null;
        }
    }
}
