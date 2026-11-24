using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace XRGameDev.Concerthall
{
    /// <summary>
    /// The Blender theatre model (匹诺康尼大剧院.fbx) ships with 7 point lights, but Unity
    /// imports them with the FBX fallback Range of 10 and the raw Blender intensity
    /// (1,000,000 .. 30,000,000). The result is lights that are effectively invisible.
    ///
    /// This tool recreates those 7 fixtures as first-class scene lights with a sensible
    /// range / intensity, hides the imported originals, and applies the environment
    /// recommendations (dark ambient + reflection probe for the glossy surfaces).
    ///
    /// Run: Tools > Concerthall > Recreate Lights From FBX
    /// </summary>
    public static class ConcerthallLightSetup
    {
        const string LightsRootName = "Concerthall Lights";
        const string ProbeName = "Concerthall Reflection Probe";

        // Scene intensity (URP Lumens) used for a fixture whose exported FBX
        // Intensity was 10,000,000. Tune this single value to taste.
        const float BaseIntensity = 3000f;

        // How far each fixture should reach. The hall is ~280 units across, so keep
        // this well below that to preserve the dark, moody look.
        const float LightRange = 60f;

        // Point light shadows need 6 shadow maps each. With 7 lights that is 42 extra
        // shadow renders per frame - expensive in VR. Enable once you are happy with
        // the look and have headroom.
        const bool EnableShadows = false;

        // Dark, slightly blue ambient so the hall reads as unlit where no fixture reaches.
        static readonly Color DarkAmbient = new Color(0.035f, 0.033f, 0.05f, 1f);

        // --- Glow plane -----------------------------------------------------------
        // The FBX ships a tilted 300x96 backdrop quad named "Plane". It has no emission
        // in the source, so it renders as flat grey. Give it a warm HDR emission that
        // matches the hall fixtures; the project's Bloom (threshold 1) then picks it up.
        const string GlowPlaneName = "Plane";
        const string GlowMaterialPath = "Assets/Materials/ConcerthallGlowPlane.mat";
        const string ModelName = "匹诺康尼大剧院";

        // Deep amber taken from the hall fixtures, darkened to sit with the moody
        // interior instead of reading as a plain warm white.
        static readonly Color GlowColor = new Color(0.85f, 0.38f, 0.1f, 1f);

        // Multiplied into GlowColor to push it past the Bloom threshold of 1.
        // Lower towards 1 for a subtle wash, raise for a hot panel.
        const float GlowIntensity = 2f;

        // FBX Intensity ratios extracted from the source model:
        // 10,000,000 -> 1.0   |   30,000,000 -> 3.0   |   1,000,000 -> 0.1
        static readonly Dictionary<string, float> IntensityMultiplier = new Dictionary<string, float>
        {
            { "Point",     1.0f },
            { "Point.001", 1.0f },
            { "Point.002", 1.0f },
            { "Point.003", 3.0f },
            { "Point.004", 1.0f },
            { "Point.005", 0.1f },
            { "Point.006", 0.1f },
        };

        [MenuItem("Tools/Concerthall/Recreate Lights From FBX")]
        public static void RecreateLights()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Concerthall Lights", "Exit Play Mode first.", "OK");
                return;
            }

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            // 1. Drop any previous run first so this stays idempotent and so the lights
            //    we generated last time are not mistaken for imported ones.
            var previousRoot = GameObject.Find(LightsRootName);
            if (previousRoot != null)
                Object.DestroyImmediate(previousRoot);
            var previousProbe = GameObject.Find(ProbeName);
            if (previousProbe != null)
                Object.DestroyImmediate(previousProbe);

            // 2. Find the lights Unity imported from the FBX.
            var imported = Object
                .FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(l => l.name.StartsWith("Point") && l.transform.root.name != LightsRootName)
                .OrderBy(l => l.name)
                .ToList();

            if (imported.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Concerthall Lights",
                    "No imported FBX lights found in the scene.\n\n" +
                    "Make sure the theatre model is loaded and that \"Import Lights\" is enabled " +
                    "on the model importer (.fbx > Materials/Model tab).",
                    "OK");
                return;
            }

            var modelRoot = imported[0].transform.root;

            // 3. Recreate each fixture as a real scene light.
            var lightsRoot = new GameObject(LightsRootName);
            Undo.RegisterCreatedObjectUndo(lightsRoot, "Create Concerthall Lights");
            lightsRoot.transform.position = Vector3.zero;

            foreach (var source in imported)
            {
                var go = new GameObject(source.name + " (Hall)");
                go.transform.SetParent(lightsRoot.transform, false);
                go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);

                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = source.color;
                light.intensity = BaseIntensity *
                    (IntensityMultiplier.TryGetValue(source.name, out var m) ? m : 1f);
                light.range = LightRange;
                light.shadows = EnableShadows ? LightShadows.Soft : LightShadows.None;
                light.lightmapBakeType = LightmapBakeType.Mixed;
                light.shadowBias = 0.05f;
                light.shadowNormalBias = 0.4f;

                // 4. Ignore the imported original - it would blow the hall out at 1e7.
                Undo.RecordObject(source, "Disable Imported FBX Light");
                source.enabled = false;
            }

            // 5. Dark environment: drop the bright procedural-skybox ambient so the
            //    fixtures actually define the lighting (skybox ambient is what washes
            //    the hall out right now).
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = DarkAmbient;
            RenderSettings.ambientIntensity = 1f;

            // 6. Reflection probe across the model so the gold / polished surfaces pick
            //    up the room instead of the default skybox.
            var bounds = new Bounds();
            var renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                foreach (var r in renderers)
                    bounds.Encapsulate(r.bounds);
            }
            else
            {
                bounds = new Bounds(modelRoot.position, Vector3.one * 200f);
            }

            var probeGo = new GameObject(ProbeName);
            Undo.RegisterCreatedObjectUndo(probeGo, "Create Reflection Probe");
            probeGo.transform.position = bounds.center;

            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Baked;
            probe.boxProjection = true;
            probe.resolution = 256;
            probe.size = Vector3.Max(bounds.size, Vector3.one * 10f);
            probe.center = Vector3.zero;
            probe.nearClipPlane = 0.3f;
            probe.blendDistance = 1f;
            probe.intensity = 1f;
            probe.importance = 1;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log(
                $"[Concerthall] Recreated {imported.Count} point lights under \"{LightsRootName}\" " +
                $"(range {LightRange}, base intensity {BaseIntensity}). " +
                "Imported FBX lights disabled, ambient set to dark, reflection probe added. " +
                "Run Tools > Concerthall > Bake Lighting to bake lightmaps + the probe.",
                lightsRoot);

            EditorUtility.DisplayDialog(
                "Concerthall Lights",
                $"Recreated {imported.Count} point lights.\n\n" +
                "The original FBX lights are now disabled, ambient is dark, and a baked " +
                "reflection probe was added.\n\n" +
                "Next: Tools > Concerthall > Bake Lighting.",
                "OK");
        }

        [MenuItem("Tools/Concerthall/Bake Lighting")]
        public static void BakeLighting()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Concerthall Lights", "Exit Play Mode first.", "OK");
                return;
            }

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            // Mark the theatre geometry static so it can receive baked GI. This is the
            // expensive part - a 200 MB model can take a long time to bake.
            int marked = 0;
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == LightsRootName || go.name == ProbeName) continue;
                if (go.name.Trim() != "匹诺康尼大剧院") continue;

                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                {
                    var flags = GameObjectUtility.GetStaticEditorFlags(t.gameObject);
                    var newFlags = flags | StaticEditorFlags.ContributeGI;
                    if (newFlags != flags)
                    {
                        GameObjectUtility.SetStaticEditorFlags(t.gameObject, newFlags);
                        marked++;
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Lightmapping.BakeAsync();
            Debug.Log($"[Concerthall] Marked {marked} objects Contribute GI and started a bake.");
        }

        [MenuItem("Tools/Concerthall/Apply Glow Plane")]
        public static void ApplyGlowPlane()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Concerthall Glow", "Exit Play Mode first.", "OK");
                return;
            }

            // 1. Find the "Plane" card inside the imported theatre model. Match the name
            //    exactly so "Plane.001" (the ground sheet) is left alone.
            Transform plane = null;
            foreach (var go in UnityEngine.SceneManagement.SceneManager
                         .GetActiveScene().GetRootGameObjects())
            {
                if (go.name.Trim() != ModelName) continue;
                plane = go.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name == GlowPlaneName);
                break;
            }

            var renderer = plane != null ? plane.GetComponent<Renderer>() : null;
            if (renderer == null)
            {
                EditorUtility.DisplayDialog(
                    "Concerthall Glow",
                    $"Could not find a renderable \"{GlowPlaneName}\" object under the " +
                    $"\"{ModelName}\" model in this scene.",
                    "OK");
                return;
            }

            // 2. Load (or create) the emissive material.
            var material = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    EditorUtility.DisplayDialog(
                        "Concerthall Glow", "URP/Lit shader not found in this project.", "OK");
                    return;
                }

                if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                    AssetDatabase.CreateFolder("Assets", "Materials");

                material = new Material(shader) { name = "ConcerthallGlowPlane" };
                AssetDatabase.CreateAsset(material, GlowMaterialPath);
            }

            // Dark base so the card reads as a lit panel rather than a grey sheet, plus a
            // warm HDR emission. The emission is above Bloom's threshold of 1, so the post
            // effect picks it up with no extra Volume setup.
            material.SetColor("_BaseColor", GlowColor * 0.2f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", GlowColor * GlowIntensity);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;

            // The FBX quad faces away from the audience, so a single-sided shader hides the
            // emission. Render both faces (URP/Lit "Render Face" = Both) so the glow shows
            // from either side of the card.
            material.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(material);

            // 3. Assign it to the "Plane" renderer only.
            Undo.RecordObject(renderer, "Apply Concerthall Glow Plane");
            var slots = renderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
                slots[i] = material;
            renderer.sharedMaterials = slots;

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[Concerthall] Applied emissive glow ({GlowColor * GlowIntensity}) to " +
                $"\"{GlowPlaneName}\" via {GlowMaterialPath}. Bloom (threshold 1) should pick it up.",
                renderer);

            EditorUtility.DisplayDialog(
                "Concerthall Glow",
                $"Applied the emissive glow to \"{GlowPlaneName}\" only (\"{GlowPlaneName}.001\" " +
                "untouched).\n\n" +
                "Tune the strength with GlowIntensity in ConcerthallLightSetup.cs.",
                "OK");
        }
    }
}
