using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(CaranguejoChefe))]
public class PassarinhosAtordoamento : MonoBehaviour
{
    [SerializeField] Transform cabeca;
    [SerializeField] Texture2D atlasAsas;

    readonly SpriteRenderer[] aves = new SpriteRenderer[3];
    readonly Sprite[] quadros = new Sprite[8];
    CaranguejoChefe chefe;
    Transform bando;
    float tempo;

    void Awake() { chefe = GetComponent<CaranguejoChefe>(); }

    void LateUpdate()
    {
        if (!chefe.isActiveAndEnabled || chefe.estadoAtual != CaranguejoChefe.EstadoChefe.Atordoado)
        {
            Ocultar();
            return;
        }
        if (cabeca == null || atlasAsas == null) return;
        if (bando == null) CriarBando();
        if (!bando.gameObject.activeSelf) bando.gameObject.SetActive(true);

        tempo += Time.deltaTime;
        var centro = transform.InverseTransformPoint(cabeca.position);
        // A concha fica acima dos olhos quando o caranguejo cai de lado.
        centro.y = Mathf.Max(centro.y + 0.85f, 1.95f);
        bando.localPosition = centro;
        var camera = Camera.main;
        var olhar = camera != null ? camera.transform.rotation : transform.rotation;
        for (int i = 0; i < aves.Length; i++)
        {
            float angulo = 2f * Mathf.PI * (tempo * 0.70f + (float)i / aves.Length);
            var ave = aves[i];
            ave.transform.localPosition = new Vector3(Mathf.Cos(angulo) * 0.78f,
                Mathf.Sin(angulo * 2f) * 0.10f, Mathf.Sin(angulo) * 0.78f);
            ave.transform.rotation = olhar * Quaternion.Euler(0, 0, 12f * Mathf.Sin(angulo));
            int quadro = Mathf.FloorToInt(tempo * (12f + i) + 2 * i) % quadros.Length;
            ave.sprite = quadros[quadro];
        }
    }

    void CriarBando()
    {
        // A textura da ilha tem quatro colunas e duas linhas, começando no alto.
        int largura = atlasAsas.width / 4, altura = atlasAsas.height / 2;
        for (int i = 0; i < quadros.Length; i++)
        {
            var recorte = new Rect((i % 4) * largura, (1 - i / 4) * altura, largura, altura);
            quadros[i] = Sprite.Create(atlasAsas, recorte, Vector2.one * 0.5f, largura / 0.54f,
                0, SpriteMeshType.FullRect);
        }
        bando = new GameObject("Passarinhos do atordoamento").transform;
        bando.SetParent(transform, false);
        for (int i = 0; i < aves.Length; i++)
        {
            var ave = new GameObject("Passarinho " + (i + 1), typeof(SpriteRenderer));
            ave.layer = gameObject.layer;
            ave.transform.SetParent(bando, false);
            aves[i] = ave.GetComponent<SpriteRenderer>();
            aves[i].color = Color.Lerp(new Color(1f, 0.65f, 0.10f), new Color(1f, 0.94f, 0.40f), i / 2f);
        }
    }

    void Ocultar()
    {
        tempo = 0;
        if (bando != null && bando.gameObject.activeSelf) bando.gameObject.SetActive(false);
    }

    void OnDisable() { Ocultar(); }
    void OnDestroy()
    {
        if (bando != null) Destroy(bando.gameObject);
        foreach (var quadro in quadros)
            if (quadro != null) Destroy(quadro);
    }
}
