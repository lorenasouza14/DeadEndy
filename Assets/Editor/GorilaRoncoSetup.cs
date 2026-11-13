using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GorilaRoncoSetup
{
    [MenuItem("Tools/Gorila/Afastar sono da pedra")]
    public static void ConfigurarDeslocamento()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var cena = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var g = UnityEngine.Object.FindObjectsByType<GorilaGuardiao>(FindObjectsInactive.Include).Single(x => x.gameObject.scene == cena);
        Undo.RecordObject(g, "Afastar sono da pedra");
        // A direita do gorila acordado corresponde a esquerda de quem olha para a entrada.
        g.deslocamentoSono = Vector3.ProjectOnPlane(g.transform.right, Vector3.up).normalized * 0.8f;
        PrefabUtility.RecordPrefabInstancePropertyModifications(g);
        EditorUtility.SetDirty(g);
        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        Selection.activeGameObject = g.gameObject;
        Debug.Log("Sono deslocado 0,8 metro para a esquerda de quem olha a caverna. Retorna ao acordar. Cena salva.", g);
    }

    [MenuItem("Tools/Gorila/Colocar sono de lado %#&e")]
    public static void ConfigurarLado()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var cena = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var g = UnityEngine.Object.FindObjectsByType<GorilaGuardiao>(FindObjectsInactive.Include).Single(x => x.gameObject.scene == cena);
        Undo.RecordObject(g,"Virar de lado ao dormir");
        g.giroLateralSono = 90f;
        PrefabUtility.RecordPrefabInstancePropertyModifications(g);
        EditorUtility.SetDirty(g); EditorSceneManager.MarkSceneDirty(cena); EditorSceneManager.SaveScene(cena);
        Selection.activeGameObject = g.gameObject;
        Debug.Log("Sono lateral configurado: giro suave de 90 graus ao deitar e retorno ao acordar. Cena salva.", g);
    }

    [MenuItem("Tools/Gorila/Configurar sono Die e bolhas %#&r")]
    public static void Configurar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var cena = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (cena.name != "Demo_day_fase_gorilla" && cena.name != "juncao_fase1-2")
            throw new InvalidOperationException("Abra a fase do gorila ou a cena de juncao.");
        var g = UnityEngine.Object.FindObjectsByType<GorilaGuardiao>(FindObjectsInactive.Include).Single(x => x.gameObject.scene == cena);
        var cabeca = g.GetComponentsInChildren<Transform>(true).Single(t => t.name == "head");
        Undo.IncrementCurrentGroup(); int grupo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Sono Die e bolhas do gorila");
        Undo.RecordObject(g,"Trocar pose de sono");
        g.usarCicloSono = true;
        g.estadoDormindo = "Base Layer.Animal_Gorilla@Die"; g.valorDormindo = 8; g.manterPoseFinalSono = true;
        var t = cabeca.Find("Bolhas de ronco");
        if (t == null)
        {
            var go = new GameObject("Bolhas de ronco"); Undo.RegisterCreatedObjectUndo(go,"Criar bolhas de ronco");
            t = go.transform; t.SetParent(cabeca,false);
        }
        Undo.RecordObject(t,"Posicionar bolhas junto ao focinho");
        t.position = cabeca.position + g.transform.forward * 0.48f + Vector3.up * 0.08f;
        t.rotation = Quaternion.identity;
        Vector3 escala = cabeca.lossyScale;
        t.localScale = new Vector3(1f/Mathf.Abs(escala.x),1f/Mathf.Abs(escala.y),1f/Mathf.Abs(escala.z));
        t.gameObject.isStatic=false;
        var ps = t.GetComponent<ParticleSystem>();
        if(ps==null) ps=Undo.AddComponent<ParticleSystem>(t.gameObject);
        Undo.RecordObject(ps,"Configurar bolhas leves");
        ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main; main.playOnAwake=false; main.loop=true; main.duration=3;
        main.startLifetime=new ParticleSystem.MinMaxCurve(1.6f,2.4f);
        main.startSpeed=0; main.startSize=new ParticleSystem.MinMaxCurve(0.16f,0.3f);
        main.startColor=Color.white; main.maxParticles=12; main.gravityModifier=0;
        main.simulationSpace=ParticleSystemSimulationSpace.World; main.scalingMode=ParticleSystemScalingMode.Shape; main.useUnscaledTime=false;
        var emission=ps.emission; emission.enabled=true; emission.rateOverTime=1.4f;
        var shape=ps.shape; shape.enabled=true; shape.shapeType=ParticleSystemShapeType.Sphere; shape.radius=0.045f;
        var velocidade=ps.velocityOverLifetime; velocidade.enabled=true; velocidade.space=ParticleSystemSimulationSpace.World;
        velocidade.x=0.06f; velocidade.y=0.5f; velocidade.z=0.02f;
        var size=ps.sizeOverLifetime; size.enabled=true; size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,0.45f,1,1.6f));
        var cor=ps.colorOverLifetime; cor.enabled=true;
        var gradiente=new Gradient(); gradiente.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(0.85f,0.15f),new GradientAlphaKey(0.7f,0.65f),new GradientAlphaKey(0,1)}); cor.color=gradiente;
        const string caminho="Assets/fase_gorila/materials/BolhasRonco.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if(material==null) { material=new Material(Shader.Find("DeadEndy/BolhaRonco")); AssetDatabase.CreateAsset(material,caminho); }
        var renderer=ps.GetComponent<ParticleSystemRenderer>(); Undo.RecordObject(renderer,"Associar material das bolhas");
        renderer.renderMode=ParticleSystemRenderMode.Billboard; renderer.sharedMaterial=material;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows=false;
        g.bolhasRonco=ps;
        PrefabUtility.RecordPrefabInstancePropertyModifications(g); EditorUtility.SetDirty(g); EditorUtility.SetDirty(ps); EditorUtility.SetDirty(renderer);
        Undo.CollapseUndoOperations(grupo);
        EditorSceneManager.MarkSceneDirty(cena); EditorSceneManager.SaveScene(cena); AssetDatabase.SaveAssets();
        Selection.activeGameObject=g.gameObject;
        Debug.Log("Sono atualizado e salvo: Die (8), pose final deitada, bolhas de ronco ligadas ao osso head. Sem executar Play Mode.",g);
    }
}
