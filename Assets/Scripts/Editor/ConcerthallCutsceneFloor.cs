using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace XRGameDev.Concerthall
{
    /// <summary>
    /// The cutscene area uses a hand-placed "CutsceneFloor" box collider, but the theatre
    /// FBX floor is not flat - it steps, ramps and dips. A single flat box therefore leaves
    /// the player floating above it in some places and sinking into it in others.
    ///
    /// This tool rebuilds that collider from the model's own floor geometry instead: it walks
    /// every mesh under the theatre model, keeps the near-horizontal triangles that fall
    /// inside the box footprint and inside a vertical band around the box, writes them into a
    /// small mesh asset and assigns that as a MeshCollider on CutsceneFloor. The result
    /// follows the shape of the FBX floor exactly, and the .fbx file itself is never written.
    ///
    /// Run: Tools > Concerthall > Build Cutscene Floor Collider
    /// </summary>
    public static class ConcerthallCutsceneFloor
    {
        const string FloorName = "CutsceneFloor";
        const string ModelName = "匹诺康尼大剧院";
        const string MeshPath = "Assets/Scenes/concerthall/CutsceneFloorCollider.asset";

        // Grow the search footprint past the box so the new floor meets the surrounding
        // walkable surface instead of stopping dead at the old box edge.
        const float Margin = 4f;

        // Vertical window measured from the box collider. Anything higher than BandAbove (a
        // seat back, a balcony) or lower than BandBelow (a basement, the 300x300 ground sheet
        // "Plane.001") is not the surface we are standing on. Widen these if the tool reports
        // that it found no floor triangles.
        const float BandAbove = 4f;
        const float BandBelow = 8f;

        // A triangle only counts as floor when its normal is within this angle of straight
        // up. 55 degrees still lets genuine ramps and stair treads through while rejecting
        // walls and ceilings.
        const float MaxSlopeDegrees = 55f;

        [MenuItem("Tools/Concerthall/Build Cutscene Floor Collider")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Cutscene Floor", "Exit Play Mode first.", "OK");
                return;
            }

            var scene = SceneManager.GetActiveScene();

            var floorT = FindByName(FloorName);
            var box = floorT != null ? floorT.GetComponent<BoxCollider>() : null;
            if (box == null)
            {
                EditorUtility.DisplayDialog(
                    "Cutscene Floor",
                    $"Could not find a \"{FloorName}\" object with a BoxCollider in this scene.",
                    "OK");
                return;
            }

            var model = FindModelRoot(scene);
            if (model == null)
            {
                EditorUtility.DisplayDialog(
                    "Cutscene Floor",
                    $"Could not find the \"{ModelName}\" model in this scene.",
                    "OK");
                return;
            }

            var filters = model.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Cutscene Floor", $"\"{ModelName}\" has no meshes.", "OK");
                return;
            }

            // Mesh vertex data is stripped unless the model importer has Read/Write on, and
            // it is off by default. Borrow it for one import and hand it straight back, so the
            // .fbx file and its import settings end up exactly as they were.
            bool needReadable = false;
            string modelPath = null;
            foreach (var mf in filters)
            {
                if (mf.sharedMesh == null) continue;
                if (modelPath == null) modelPath = AssetDatabase.GetAssetPath(mf.sharedMesh);
                if (!mf.sharedMesh.isReadable) needReadable = true;
            }

            var importer = string.IsNullOrEmpty(modelPath)
                ? null
                : AssetImporter.GetAtPath(modelPath) as ModelImporter;

            var verts = new List<Vector3>();
            var tris = new List<int>();
            bool flipped = false;

            try
            {
                if (needReadable && importer != null && !importer.isReadable)
                {
                    EditorUtility.DisplayProgressBar(
                        "Cutscene Floor",
                        "Enabling Read/Write on the model for one import...", 0f);
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                    flipped = true;

                    // Reimporting the model rebuilds the scene's prefab instance, so the
                    // Transform and MeshFilter[] captured above are now dead objects. Look
                    // the model up again before reading anything from it.
                    model = FindModelRoot(scene);
                    filters = model != null
                        ? model.GetComponentsInChildren<MeshFilter>(true)
                        : System.Array.Empty<MeshFilter>();
                }

                var w2l = floorT.worldToLocalMatrix;

                float top = box.center.y + box.size.y * 0.5f;
                float minY = top - BandBelow;
                float maxY = top + BandAbove;
                float cosSlope = Mathf.Cos(MaxSlopeDegrees * Mathf.Deg2Rad);
                float halfX = box.size.x * 0.5f + Margin;
                float halfZ = box.size.z * 0.5f + Margin;
                float cx = box.center.x;
                float cz = box.center.z;

                for (int m = 0; m < filters.Length; m++)
                {
                    if ((m & 15) == 0)
                    {
                        EditorUtility.DisplayProgressBar(
                            "Cutscene Floor", "Collecting model floor triangles...",
                            (float)m / Mathf.Max(1, filters.Length));
                    }

                    var mf = filters[m];
                    var src = mf != null ? mf.sharedMesh : null;
                    if (src == null || !src.isReadable) continue;

                    var l2l = w2l * mf.transform.localToWorldMatrix;
                    var vs = src.vertices;
                    var ts = src.triangles;

                    for (int i = 0; i + 2 < ts.Length; i += 3)
                    {
                        var p0 = l2l.MultiplyPoint3x4(vs[ts[i]]);
                        var p1 = l2l.MultiplyPoint3x4(vs[ts[i + 1]]);
                        var p2 = l2l.MultiplyPoint3x4(vs[ts[i + 2]]);

                        float cy = (p0.y + p1.y + p2.y) / 3f;
                        if (cy < minY || cy > maxY) continue;

                        var n = Vector3.Cross(p1 - p0, p2 - p0);
                        float len = n.magnitude;
                        if (len < 1e-8f) continue;
                        if (n.y / len < cosSlope) continue;

                        float mx = (p0.x + p1.x + p2.x) / 3f;
                        float mz = (p0.z + p1.z + p2.z) / 3f;
                        if (Mathf.Abs(mx - cx) > halfX || Mathf.Abs(mz - cz) > halfZ) continue;

                        int b0 = verts.Count;
                        verts.Add(p0);
                        verts.Add(p1);
                        verts.Add(p2);
                        tris.Add(b0);
                        tris.Add(b0 + 1);
                        tris.Add(b0 + 2);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();

                if (flipped && importer != null)
                {
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }
            }

            if (tris.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Cutscene Floor",
                    "No floor triangles were found under the box.\n\n" +
                    "Either the box is not sitting on the model floor, or the search band in " +
                    "ConcerthallCutsceneFloor.cs is too tight. Move the box onto the floor, or " +
                    "increase BandAbove / BandBelow, then run the tool again.",
                    "OK");
                return;
            }

            var mesh = new Mesh { name = "CutsceneFloorCollider" };
            if (verts.Count > 65000)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            if (AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath) != null)
                AssetDatabase.DeleteAsset(MeshPath);
            AssetDatabase.CreateAsset(mesh, MeshPath);

            var col = floorT.GetComponent<MeshCollider>();
            if (col == null)
            {
                col = Undo.AddComponent<MeshCollider>(floorT.gameObject);
            }
            else
            {
                Undo.RecordObject(col, "Build Cutscene Floor Collider");
            }

            col.sharedMesh = mesh;
            col.convex = false;

            // The flat box sits on top of the real floor in places and below it in others, so
            // it has to go. It is only disabled - re-tick it in the Inspector to go back.
            if (box.enabled)
            {
                Undo.RecordObject(box, "Build Cutscene Floor Collider");
                box.enabled = false;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            var b = mesh.bounds;
            Debug.Log(
                $"[Concerthall] Built \"{FloorName}\" collider from the model floor: " +
                $"{verts.Count} verts / {tris.Count / 3} tris, " +
                $"local bounds {b.min:F2}..{b.max:F2}. The flat BoxCollider is now disabled.",
                floorT);

            EditorUtility.DisplayDialog(
                "Cutscene Floor",
                $"Built the cutscene floor from {tris.Count / 3} model triangles.\n\n" +
                "The old flat BoxCollider has been disabled (not deleted) - re-tick it in the " +
                "Inspector if you want to go back.\n\n" +
                "Tune Margin / BandAbove / BandBelow / MaxSlopeDegrees in " +
                "ConcerthallCutsceneFloor.cs and run it again to widen or tighten the patch.",
                "OK");
        }

        static Transform FindByName(string name)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == name) return t;
            }
            return null;
        }

        /// <summary>
        /// Locates the imported theatre model. Unity names the root after the file, so
        /// "匹诺康尼大剧院 .fbx" becomes "匹诺康尼大剧院 " - hence the Trim().
        /// </summary>
        static Transform FindModelRoot(Scene scene)
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name.Trim() == ModelName) return go.transform;
            }

            foreach (var t in Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name.Trim() == ModelName) return t;
            }

            return null;
        }
    }
}
