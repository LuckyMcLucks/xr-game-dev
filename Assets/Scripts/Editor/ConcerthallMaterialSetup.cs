using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace XRGameDev.Concerthall
{
    /// <summary>
    /// The Blender theatre (匹诺康尼大剧院 .fbx) imports with every material embedded inside the
    /// model prefab ("In Prefab" location). Embedded materials cannot be inspected or edited
    /// from the Materials tab, which is why the model looks fine on screen but shows no mapping.
    ///
    /// On top of that, Blender authored all 82 materials as double sided, but the FBX importer
    /// drops that flag and URP/Lit defaults to Cull Back. The thin curtain sheets therefore lose
    /// their back faces and read as hollow / flat instead of full fabric.
    ///
    /// This tool extracts every embedded material to a real .mat asset, rebinds the model to
    /// those assets, and switches each one to Render Face = Both / Surface Type = Opaque, then
    /// enables lightmap UV generation so the model can receive baked lighting later.
    ///
    /// Run: Tools > Concerthall > Extract and Fix Theatre Materials
    /// </summary>
    public static class ConcerthallMaterialSetup
    {
        const string FbxPath = "Assets/Scenes/concerthall/匹诺康尼大剧院 .fbx";
        const string MaterialsFolder = "Assets/Scenes/concerthall/Materials";

        // Renders every back face. This is what makes the curtains look full. It doubles the
        // shading cost for the affected geometry, so it is traded off against VR overdraw.
        const bool RenderFaceBoth = true;

        // Generate a second (lightmap) UV set. Required before baking GI/AO, which is what
        // restores the crease / fold depth the Blender viewport shows. Costs extra import time
        // and memory, so set this to false if you are not going to bake.
        const bool GenerateLightmapUVs = true;

        [MenuItem("Tools/Concerthall/Extract and Fix Theatre Materials")]
        public static void ExtractAndFixMaterials()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Concerthall Materials", "Exit Play Mode first.", "OK");
                return;
            }

            var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null)
            {
                EditorUtility.DisplayDialog(
                    "Concerthall Materials",
                    $"No model importer found at:\n{FbxPath}",
                    "OK");
                return;
            }

            EnsureFolder(MaterialsFolder);

            // Materials Unity embedded inside the model prefab have the model itself as their
            // asset path. They are gathered from both the asset sub-list and the prefab's
            // renderers, then de-duplicated, so the tool does not depend on how Unity chooses
            // to expose embedded material descriptions. Anything already living in the Materials
            // folder is skipped so the tool can be run repeatedly without creating duplicates.
            var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            var rendererMaterials = prefabRoot != null
                ? prefabRoot.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
                : Enumerable.Empty<Material>();

            var embedded = AssetDatabase
                .LoadAllAssetsAtPath(FbxPath)
                .OfType<Material>()
                .Concat(rendererMaterials)
                .Where(m => m != null && AssetDatabase.GetAssetPath(m) == FbxPath)
                .Distinct()
                .ToList();

            var sources = new List<(string name, Material asset)>(embedded.Count);
            var usedPaths = new HashSet<string>();
            int extractedCount = 0;

            foreach (var source in embedded)
            {
                string assetPath = UniquePath(MaterialsFolder, source.name, usedPaths);
                var target = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

                if (target == null)
                {
                    // Preferred path: let Unity do the extraction, which also registers the
                    // remap so the model starts using the standalone asset.
                    try
                    {
                        AssetDatabase.ExtractAsset(source, assetPath);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[Concerthall] ExtractAsset failed for '{source.name}': {e.Message}");
                    }

                    target = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

                    // Fallback: clone the embedded material by hand.
                    if (target == null)
                    {
                        target = new Material(source.shader);
                        target.CopyPropertiesFromMaterial(source);
                        target.name = Path.GetFileNameWithoutExtension(assetPath);
                        AssetDatabase.CreateAsset(target, assetPath);
                    }

                    extractedCount++;
                }

                sources.Add((source.name, target));
            }

            // Rebind the model so the renderers reference the standalone assets. ExtractAsset may
            // already have done this, so only add what is genuinely missing.
            var remap = importer.GetExternalObjectMap();
            int remappedCount = 0;

            foreach (var (name, asset) in sources)
            {
                if (asset == null)
                    continue;

                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), name);
                if (remap.ContainsKey(id))
                    continue;

                importer.AddRemap(id, asset);
                remappedCount++;
            }

            // Apply the render state to every material in the folder, including any that were
            // extracted by a previous run.
            int fixedCount = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialsFolder }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat == null)
                    continue;

                if (ApplyRenderState(mat))
                    fixedCount++;
                EditorUtility.SetDirty(mat);
            }

            bool uvChanged = false;
            if (GenerateLightmapUVs && !importer.generateSecondaryUV)
            {
                importer.generateSecondaryUV = true;
                uvChanged = true;
            }

            importer.SaveAndReimport();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SceneView.RepaintAll();

            EditorUtility.DisplayDialog(
                "Concerthall Materials",
                $"Extracted {extractedCount} material(s), rebound {remappedCount}, " +
                $"set {fixedCount} to double-sided + opaque.\n" +
                $"Lightmap UVs: {(uvChanged ? "enabled" : "already on")}.\n\n" +
                "The theatre instance in the scene updates automatically.",
                "OK");
        }

        /// <summary>
        /// Forces the URP/Lit render state to double sided and fully opaque: Render Face = Both,
        /// Surface Type = Opaque, Alpha Clipping off. Returns true when something actually changed.
        /// </summary>
        static bool ApplyRenderState(Material mat)
        {
            bool changed = false;

            changed |= SetFloat(mat, "_Surface", 0f);                    // 0 = Opaque, 1 = Transparent
            changed |= SetFloat(mat, "_AlphaClip", 0f);                  // Alpha Clipping off
            changed |= SetFloat(mat, "_Blend", 0f);                       // Alpha blend mode
            changed |= SetFloat(mat, "_SrcBlend", (float)BlendMode.One);
            changed |= SetFloat(mat, "_DstBlend", (float)BlendMode.Zero);
            changed |= SetFloat(mat, "_ZWrite", 1f);

            if (RenderFaceBoth)
                changed |= SetFloat(mat, "_Cull", (float)CullMode.Off);   // 0 = Both, 1 = Front, 2 = Back

            changed |= DisableKeyword(mat, "_ALPHATEST_ON");
            changed |= DisableKeyword(mat, "_ALPHAPREMULTIPLY_ON");
            changed |= DisableKeyword(mat, "_ALPHAMODULATE_ON");
            changed |= DisableKeyword(mat, "_SURFACE_TYPE_TRANSPARENT");

            if (mat.renderQueue != (int)RenderQueue.Geometry)
            {
                mat.renderQueue = (int)RenderQueue.Geometry;
                changed = true;
            }

            if (mat.GetTag("RenderType", false) != "Opaque")
            {
                mat.SetOverrideTag("RenderType", "Opaque");
                changed = true;
            }

            return changed;
        }

        static bool SetFloat(Material mat, string property, float value)
        {
            if (!mat.HasProperty(property) || Mathf.Approximately(mat.GetFloat(property), value))
                return false;

            mat.SetFloat(property, value);
            return true;
        }

        static bool DisableKeyword(Material mat, string keyword)
        {
            if (!mat.IsKeywordEnabled(keyword))
                return false;

            mat.DisableKeyword(keyword);
            return true;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = Path.GetFileName(folder);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>Builds a safe, unique .mat path for the given material name.</summary>
        static string UniquePath(string folder, string materialName, HashSet<string> used)
        {
            string clean = Sanitize(materialName);
            if (clean.Length == 0)
                clean = "Material";

            string path = $"{folder}/{clean}.mat";
            int suffix = 1;
            while (!used.Add(path))
            {
                path = $"{folder}/{clean}_{suffix}.mat";
                suffix++;
            }

            return path;
        }

        static string Sanitize(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
                sb.Append(invalid.Contains(c) ? '_' : c);

            return sb.ToString().Trim().TrimEnd('.');
        }
    }
}
