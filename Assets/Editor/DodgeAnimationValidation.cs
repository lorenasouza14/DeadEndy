using System;
using System.IO;
using System.Linq;
using System.Reflection;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class DodgeAnimationValidation
{
    const string ScenePath = "Assets/Cenario/PNB_Tropical_Jungle/Scenes/Demo_Day.unity";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void RunBatch()
    {
        try { Run(); EditorApplication.Exit(0); }
        catch (Exception error)
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/dodge-animation-validation.txt", "FAIL: " + error);
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    [MenuItem("Tools/Player/Validar animacao do deslize")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Execute fora do Play Mode.");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        GameObject clone = null;
        try
        {
            var source = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ThirdPersonController>(true)).First();
            clone = Object.Instantiate(source.gameObject);
            clone.name = "Dodge animation preview";
            clone.hideFlags = HideFlags.DontSave;
            clone.SetActive(true);
            clone.transform.SetPositionAndRotation(new Vector3(0, 1000, 0), Quaternion.identity);
            foreach (Transform child in clone.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            var movement = clone.GetComponent<ThirdPersonController>();
            var animator = movement.CharacterAnimator != null ? movement.CharacterAnimator : clone.GetComponent<Animator>();
            Check(animator != null, "Animator usado pelo movimento existe");
            Set(movement, "_animator", animator);
            animator.Rebind();
            animator.SetBool("Grounded", true);
            animator.SetFloat("Speed", 0);
            animator.Play("Idle Walk Run Blend", 0, 0);
            animator.Update(0.1f);
            Set(movement, "_dodgeTimeLeft", movement.DodgeDuration);
            Call(movement, "PrepareDodgeAnimation");
            var bones = (Transform[])Get(movement, "_dodgeBones");
            Check(bones.All(b => b != null), "Quadril, joelhos e pes encontrados no personagem real");
            var body = clone.GetComponentsInChildren<SkinnedMeshRenderer>().First(r => r.enabled && r.gameObject.activeInHierarchy);
            Check(body.bones.Any(b => b == bones[0] || b.IsChildOf(bones[0])), "Pose ligada aos ossos da malha visivel");
            Vector3 root = clone.transform.position;
            Vector3 hips = bones[0].position;
            Vector3 leftFoot = bones[3].position;
            Vector3 rightFoot = bones[6].position;
            var rotations = bones.Select(b => b.localRotation).ToArray();
            Directory.CreateDirectory("Logs");
            Capture(clone, "Logs/dodge-idle.png");
            foreach (int side in new[] { -1, 1 })
            {
                Set(movement, "_dodgeDirection", Vector3.right * side);
                Set(movement, "_dodgeTimeLeft", movement.DodgeDuration * 0.5f);
                Call(movement, "AnimateDodge");
                Check(bones[0].position.y < hips.y - 0.05f, "Quadril abaixa no deslize " + side);
                Check(Mathf.Abs(bones[3].position.y - leftFoot.y) < 0.025f && Mathf.Abs(bones[6].position.y - rightFoot.y) < 0.025f,
                    "Pes preservam altura do chao " + side);
                Check(clone.transform.position == root, "Animacao nao desloca collider/camera " + side);
                Vector3 posedHips = bones[0].position;
                var posedRotations = bones.Select(b => b.localRotation).ToArray();
                for (int frame = 0; frame < 20; frame++) Call(movement, "AnimateDodge");
                Check(Vector3.Distance(bones[0].position, posedHips) < 0.0001f &&
                    bones.Select((bone, i) => Quaternion.Angle(bone.localRotation, posedRotations[i])).Max() < 0.1f,
                    "Pausa/reaplicacao nao acumula deformacao " + side);
                Capture(clone, side < 0 ? "Logs/dodge-left.png" : "Logs/dodge-right.png");
                Call(movement, "CancelDodge");
                Check(Vector3.Distance(bones[0].position, hips) < 0.0001f &&
                    bones.Select((bone, i) => Quaternion.Angle(bone.localRotation, rotations[i])).Max() < 0.1f,
                    "Cancelamento restaura pose " + side);
            }
            // O ciclo normal restaura em Update antes de avaliar o Animator.
            Set(movement, "_dodgeTimeLeft", movement.DodgeDuration * 0.5f);
            Call(movement, "AnimateDodge");
            Call(movement, "RestoreDodgePose");
            Set(movement, "_dodgeTimeLeft", 0f);
            Call(movement, "AnimateDodge");
            Check(Vector3.Distance(bones[0].position, hips) < 0.0001f, "Fim da esquiva libera locomocao");
            File.WriteAllText("Logs/dodge-animation-validation.txt", "PASS: scene character bones, left/right crouch, foot height, unchanged root, pause stability, cancellation and normal completion.\nAnimator: " + animator.name);
        }
        finally
        {
            if (clone != null) Object.DestroyImmediate(clone);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }

    static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        Debug.Log("DODGE PASS: " + message);
    }
    static object Get(object target, string name) => typeof(ThirdPersonController).GetField(name, Private).GetValue(target);
    static void Set(object target, string name, object value) => typeof(ThirdPersonController).GetField(name, Private).SetValue(target, value);
    static void Call(object target, string name) => typeof(ThirdPersonController).GetMethod(name, Private).Invoke(target, null);

    static void Capture(GameObject player, string path)
    {
        var cameraObject = new GameObject("Dodge preview camera");
        var lightObject = new GameObject("Dodge preview light");
        var texture = new RenderTexture(640, 640, 24);
        var image = new Texture2D(640, 640, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        var bakedObjects = new System.Collections.Generic.List<GameObject>();
        var bakedMeshes = new System.Collections.Generic.List<Mesh>();
        var skins = player.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
        try
        {
            // BakeMesh atualiza a pele entre amostras no mesmo quadro do editor.
            foreach (var skin in skins)
            {
                var mesh = new Mesh();
                skin.BakeMesh(mesh);
                bakedMeshes.Add(mesh);
                var baked = new GameObject("Dodge preview mesh");
                bakedObjects.Add(baked);
                baked.layer = 31;
                baked.transform.SetPositionAndRotation(skin.transform.position, skin.transform.rotation);
                baked.transform.localScale = skin.transform.lossyScale;
                baked.AddComponent<MeshFilter>().sharedMesh = mesh;
                baked.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                skin.enabled = false;
            }
            var camera = cameraObject.AddComponent<Camera>();
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.12f, 0.18f);
            camera.orthographic = true;
            Bounds bounds = skins[0].bounds;
            foreach (var renderer in skins) bounds.Encapsulate(renderer.bounds);
            camera.orthographicSize = Mathf.Max(1.2f, bounds.size.y * 0.7f);
            Vector3 center = player.transform.position + Vector3.up * (bounds.size.y * 0.5f);
            camera.transform.position = center + new Vector3(2, 0.5f, 5);
            camera.transform.LookAt(center);
            camera.targetTexture = texture;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.cullingMask = 1 << 31;
            light.transform.rotation = Quaternion.Euler(35, -25, 0);
            camera.Render();
            RenderTexture.active = texture;
            image.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            foreach (var skin in skins) skin.enabled = true;
            foreach (var baked in bakedObjects) Object.DestroyImmediate(baked);
            foreach (var mesh in bakedMeshes) Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(lightObject);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(image);
        }
    }
}
