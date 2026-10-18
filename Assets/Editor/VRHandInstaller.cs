#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Installs a reliable pose driver on both hands, reusing the actual FBX rotation curves.
/// Existing gameplay and XR interactors are left untouched.
/// </summary>
public static class VRHandInstaller
{
    private const string ModelPath = "Assets/HandModel/Models/VR_hand.fbx";
    private const string AnimationActionsPath =
        "Assets/Samples/XR Interaction Toolkit/3.3.2/Starter Assets/XRI Default Input Actions.inputactions";
    private const string LeatherTexturePath =
        "Assets/HandModel/Textures/FL_Old-English-Dark-Brown-600x600.jpg";
    private const string MaterialPath = "Assets/HandModel/HandLeather_Repair.mat";

    [MenuItem("Tools/VR Hands/Install or Repair in Current Scene")]
    public static void InstallOrRepair()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[VR Hands] Stop Play Mode before installing hands.");
            return;
        }

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[VR Hands] Open Assets/Scenes/SampleScene.unity first.");
            return;
        }

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AnimationActionsPath);
        var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().ToArray();
        var grab = clips.FirstOrDefault(c => c.name.EndsWith("Grab", StringComparison.OrdinalIgnoreCase));
        var trigger = clips.FirstOrDefault(c => c.name.EndsWith("Trigger", StringComparison.OrdinalIgnoreCase));

        if (model == null || grab == null || trigger == null)
        {
            Debug.LogError("[VR Hands] FBX or Grab/Trigger clips not found. Expected: " + ModelPath);
            return;
        }
        if (actions == null)
        {
            Debug.LogError("[VR Hands] XRI default input actions missing at: " + AnimationActionsPath);
            return;
        }

        // The archive contains "XR Origin (VR)", whereas later screenshots used
        // "XR Origin (XR Rig)". Support either without modifying the rig itself.
        var origins = scene.GetRootGameObjects()
            .SelectMany(x => x.GetComponentsInChildren<Transform>(true))
            .Where(t => t.name.StartsWith("XR Origin", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(t => t.name == "XR Origin (XR Rig)")
            .ToArray();
        Transform xrOrigin = origins.FirstOrDefault(origin =>
            origin.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Left Controller") &&
            origin.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Right Controller"));
        if (xrOrigin == null)
        {
            Debug.LogError("[VR Hands] Could not find an XR Origin with both Left and Right Controllers.");
            return;
        }
        Transform leftController = xrOrigin.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t.name == "Left Controller");
        Transform rightController = xrOrigin.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t.name == "Right Controller");
        if (leftController == null || rightController == null)
        {
            Debug.LogError("[VR Hands] Could not find both controllers beneath XR Origin (XR Rig).");
            return;
        }

        var material = EnsureLeatherMaterial();
        try
        {
            InstallOne(model, rightController, false, grab, trigger, actions, material);
            InstallOne(model, leftController, true, grab, trigger, actions, material);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[VR Hands] INSTALLED: both rigged hands, baked Grab/Trigger poses, XR grip/trigger inputs, " +
                      "keyboard G/T testing. White controller visuals hidden. Scene saved: " + scene.path);
            EditorUtility.DisplayDialog("VR Hands installed",
                "Both hands have been configured and the scene saved.\n\n" +
                "Press Play, click the Game window, then hold G (Grab) or T (Trigger).\n" +
                "The finger bones should bend without relying on Animator transitions.", "OK");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("VR Hands: installation incomplete",
                "Look in Console for [VR Hands] errors. The original ZIP remains unchanged.", "OK");
        }
    }

    private static void InstallOne(GameObject source, Transform controller, bool left,
        AnimationClip grab, AnimationClip trigger, InputActionAsset actions, Material material)
    {
        string name = left ? "LeftHand_Model" : "RightHand_Model";
        Transform existing = controller.Find(name);
        GameObject instance;
        if (existing != null)
        {
            instance = existing.gameObject;
        }
        else
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(source, controller);
            Undo.RegisterCreatedObjectUndo(instance, "Install VR hand");
            instance.name = name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            instance.transform.localScale = left ? new Vector3(-1f, 1f, 1f) : Vector3.one;
        }
        instance.SetActive(true);

        var skin = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (skin.Length == 0)
        {
            throw new InvalidOperationException("[VR Hands] " + name +
                " has no SkinnedMeshRenderer. Check FBX import; do NOT use a static MeshRenderer as the hand.");
        }
        foreach (var renderer in skin)
        {
            renderer.enabled = true;
            renderer.updateWhenOffscreen = true;
            if (renderer.bones == null || renderer.bones.Length == 0)
                throw new InvalidOperationException("[VR Hands] " + name + " skin has no bones.");
            if (material != null) renderer.sharedMaterial = material;
        }

        // The imported FBX can include an Animator on the inner Hand_Rig.
        // We deliberately disable all of them: the pose driver applies the exact imported
        // bone rotations after animation update, without looping back to Open.
        foreach (var animator in instance.GetComponentsInChildren<Animator>(true))
        {
            Undo.RecordObject(animator, "Disable competing hand Animator");
            animator.enabled = false;
        }

        var driver = instance.GetComponent<VRHandPoseDriver>();
        if (driver == null) driver = Undo.AddComponent<VRHandPoseDriver>(instance);
        Undo.RecordObject(driver, "Configure VR hand poses");
        driver.leftHand = left;
        driver.actions = actions;
        driver.blendSpeed = 8f;
        driver.allowKeyboardTest = true;
        driver.grabFingers = BuildPoses(instance.transform, grab,
            new[] { "Middle_", "Ring_", "Pinky_" });
        driver.triggerFingers = BuildPoses(instance.transform, trigger,
            new[] { "Index_" });

        int grabMoving = driver.grabFingers.Count(p => Quaternion.Angle(p.open, p.closed) > 1f);
        int triggerMoving = driver.triggerFingers.Count(p => Quaternion.Angle(p.open, p.closed) > 1f);
        if (grabMoving == 0 || triggerMoving == 0)
        {
            throw new InvalidOperationException("[VR Hands] Could not bake finger rotations from the FBX clips " +
                "(Grab=" + grabMoving + ", Trigger=" + triggerMoving + "). " +
                "Please send the [VR Hands] Console log; FBX has not been modified.");
        }
        EditorUtility.SetDirty(driver);
        VerifySkinDeformation(skin, driver.grabFingers, name);
        Debug.Log("[VR Hands] " + name + ": " + driver.grabFingers.Length + " Grab bones (" +
                  grabMoving + " rotate); " + driver.triggerFingers.Length + " Trigger bones (" +
                  triggerMoving + " rotate); SkinnedMeshRenderers=" + skin.Length);

        // Hide only visual-only controller meshes, never the tracked controller object.
        string[] possibleVisualNames = left
            ? new[] { "Left Controller Visual", "XR Controller Left" }
            : new[] { "Right Controller Visual", "XR Controller Right" };
        int hidden = 0;
        foreach (var visual in controller.GetComponentsInChildren<Transform>(true))
        {
            if (visual == controller || visual.IsChildOf(instance.transform)) continue;
            if (!possibleVisualNames.Contains(visual.name)) continue;
            Undo.RecordObject(visual.gameObject, "Hide default XR controller model");
            visual.gameObject.SetActive(false);
            hidden++;
        }
        if (hidden == 0)
            Debug.LogWarning("[VR Hands] No default controller visual found under " + controller.name +
                ". If a white plastic controller remains visible, disable its mesh visual child only.");

    }

    /// <summary>
    /// Reads FBX rotation keyframe curves and bakes the first frame + last frame as Quaternion poses.
    /// No fragile Animator Controller transitions or runtime clip evaluation are necessary.
    /// </summary>
    private static VRHandPoseDriver.FingerPose[] BuildPoses(Transform root, AnimationClip clip, string[] names)
    {
        var bindings = AnimationUtility.GetCurveBindings(clip)
            .Where(b => b.type == typeof(Transform) &&
                        names.Any(prefix => b.path.Split('/').Last().StartsWith(prefix, StringComparison.Ordinal)))
            .ToArray();
        var paths = bindings.Select(b => b.path).Distinct().OrderBy(p => p).ToArray();
        var output = new List<VRHandPoseDriver.FingerPose>();
        foreach (string path in paths)
        {
            Transform t = root.Find(path);
            if (t == null)
            {
                Debug.LogWarning("[VR Hands] Bone path from clip not found: " + path);
                continue;
            }
            var curves = bindings.Where(b => b.path == path)
                .ToDictionary(b => b.propertyName, b => AnimationUtility.GetEditorCurve(clip, b));
            output.Add(new VRHandPoseDriver.FingerPose
            {
                bone = t,
                open = ReadRotation(curves, 0f, t.localRotation),
                closed = ReadRotation(curves, Mathf.Max(0f, clip.length - .0005f), t.localRotation)
            });
        }
        return output.ToArray();
    }

    private static Quaternion ReadRotation(Dictionary<string, AnimationCurve> curves,
        float time, Quaternion fallback)
    {
        // FBX/Generic clips in Unity normally use m_LocalRotation.x/y/z/w.
        bool Get(string token, string suffix, out float value)
        {
            foreach (var entry in curves)
            {
                if (entry.Key.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0 &&
                    entry.Key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && entry.Value != null)
                {
                    value = entry.Value.Evaluate(time);
                    return true;
                }
            }
            value = 0;
            return false;
        }
        if (Get("Rotation", ".x", out float x) && Get("Rotation", ".y", out float y) &&
            Get("Rotation", ".z", out float z) && Get("Rotation", ".w", out float w))
        {
            var q = new Quaternion(x, y, z, w);
            float sq = q.x*q.x + q.y*q.y + q.z*q.z + q.w*q.w;
            return sq > 0.0001f ? Quaternion.Normalize(q) : fallback;
        }
        // Also supports Blender clips imported with raw Euler rotation curves.
        if (Get("Euler", ".x", out x) && Get("Euler", ".y", out y) && Get("Euler", ".z", out z))
            return Quaternion.Euler(x, y, z);
        return fallback;
    }

    private static void VerifySkinDeformation(SkinnedMeshRenderer[] renderers,
        VRHandPoseDriver.FingerPose[] poses, string name)
    {
        if (poses.Length == 0 || renderers.Length == 0) return;
        var saved = poses.Select(p => p.bone != null ? p.bone.localRotation : Quaternion.identity).ToArray();
        try
        {
            foreach (var p in poses)
                if (p.bone != null) p.bone.localRotation = p.open;
            // BakeMesh works without a renderer being on-screen; helps identify a static hand mesh.
            var before = new Mesh();
            var after = new Mesh();
            try
            {
                var renderer = renderers[0];
                renderer.BakeMesh(before);
                foreach (var p in poses)
                    if (p.bone != null) p.bone.localRotation = p.closed;
                renderer.BakeMesh(after);
                var first = before.vertices;
                var last = after.vertices;
                float maxDelta = 0f;
                int moved = 0;
                if (first.Length == last.Length)
                {
                    for (int i = 0; i < first.Length; i++)
                    {
                        float displacement = Vector3.Distance(first[i], last[i]);
                        maxDelta = Mathf.Max(maxDelta, displacement);
                        if (displacement > 0.0001f) moved++;
                    }
                }
                Debug.Log("[VR Hands SKIN CHECK] " + name + ": " + moved + "/" +
                          first.Length + " vertices move with Grab; max displacement=" +
                          maxDelta.ToString("F5") + " model units.");
                if (moved == 0)
                    Debug.LogWarning("[VR Hands SKIN CHECK] Bones animate but this mesh does not deform! " +
                        "Check the imported Skinned Mesh Renderer skin binding.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(before);
                UnityEngine.Object.DestroyImmediate(after);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[VR Hands SKIN CHECK] Could not bake mesh in Edit Mode: " + ex.Message);
        }
        finally
        {
            for (int i = 0; i < poses.Length; i++)
                if (poses[i].bone != null) poses[i].bone.localRotation = saved[i];
        }
    }

    private static Material EnsureLeatherMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (existing != null) return existing;
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(LeatherTexturePath);
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) return null;
        var mat = new Material(shader) { name = "HandLeather_Repair" };
        if (texture != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", texture);
        }
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        AssetDatabase.CreateAsset(mat, MaterialPath);
        return mat;
    }
}
#endif
