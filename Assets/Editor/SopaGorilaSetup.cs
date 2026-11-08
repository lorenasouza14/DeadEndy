using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SopaGorilaSetup
{
    [MenuItem("Tools/Gorila/Sopa/Preparar banana selecionada")]
    public static void PrepararBanana()
    {
        var obj = Selection.activeGameObject;
        if (EditorApplication.isPlayingOrWillChangePlaymode || obj == null || !obj.scene.IsValid()) return;
        Undo.IncrementCurrentGroup();
        int grupo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Preparar banana da sopa");
        var banana = obj.GetComponent<BananaSopa>();
        if (banana == null) banana = Undo.AddComponent<BananaSopa>(obj);
        if (obj.GetComponent<Rigidbody>() == null)
        {
            var corpo = Undo.AddComponent<Rigidbody>(obj);
            corpo.isKinematic = true;
            corpo.useGravity = false;
        }
        // O corpo e os colliders existentes da banana do monolito ficam intactos.
        var sensor = obj.transform.Find("InteracaoBanana");
        if (sensor == null)
        {
            var filho = new GameObject("InteracaoBanana");
            Undo.RegisterCreatedObjectUndo(filho, "Criar alcance de coleta");
            sensor = filho.transform;
            sensor.SetParent(obj.transform, false);
        }
        Undo.RecordObject(sensor, "Ajustar alcance em metros");
        Vector3 escala = obj.transform.lossyScale;
        sensor.localScale = new Vector3(1f / Mathf.Max(0.001f, Mathf.Abs(escala.x)),
            1f / Mathf.Max(0.001f, Mathf.Abs(escala.y)), 1f / Mathf.Max(0.001f, Mathf.Abs(escala.z)));
        sensor.position = banana.Centro;
        var col = sensor.GetComponent<SphereCollider>();
        if (col == null) col = Undo.AddComponent<SphereCollider>(sensor.gameObject);
        Undo.RecordObject(col, "Configurar trigger da banana");
        col.isTrigger = true;
        col.radius = banana.alcanceColeta;
        if (sensor.GetComponent<ItemColetavel>() == null) Undo.AddComponent<ItemColetavel>(sensor.gameObject);
        if (sensor.GetComponent<DicaInteracao>() == null) Undo.AddComponent<DicaInteracao>(sensor.gameObject);
        foreach (var t in obj.GetComponentsInChildren<Transform>(true))
        {
            Undo.RecordObject(t.gameObject, "Permitir transporte");
            t.gameObject.isStatic = false;
        }
        Undo.CollapseUndoOperations(grupo);
        EditorSceneManager.MarkSceneDirty(obj.scene);
        Debug.Log("Banana preparada. Associe Gorila Que Protege ou Monolito conforme o desafio. Depois inclua esta banana na receita do caldeirao.", banana);
    }
}
