using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace XRGameDev.Concerthall
{
    /// <summary>
    /// Temporary diagnostic probe. Dumps every material actually used by the concerthall scene,
    /// whether it resolves to a real .mat asset, and whether its base colour texture is bound.
    /// Also dumps per-renderer world bounds and mesh UV channel counts so the floor can be found.
    /// </summary>
    public static class ConcerthallDiag
    {
        const string ScenePath = "Assets/Scenes/concerthall.unity";
        const string OutPath = "c:/Users/cheri/.trae/work/unity_mat_diag.log";

        [MenuItem("Tools/Concerthall/Diagnose Materials")]
        public static void Diagnose()
        {
            var sb = new StringBuilder();

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            sb.AppendLine("SCENE=" + scene.name + " roots=" + scene.rootCount + " dirty=" + scene.isDirty);

            var roots = scene.GetRootGameObjects();
            var seen = new Dictionary<Material, int>();
            var renderers = new List<Renderer>();

            foreach (var root in roots)
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    sb.AppendLine("GO|" + Path(t) + "|static=" + ((int)GameObjectUtility.GetStaticEditorFlags(t.gameObject)));

                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    renderers.Add(r);
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        seen.TryGetValue(m, out int c);
                        seen[m] = c + 1;
                    }
                }
            }

            sb.AppendLine();
            sb.AppendLine("RENDERERS=" + renderers.Count + " DISTINCT_MATERIALS=" + seen.Count);

            sb.AppendLine();
            sb.AppendLine("=== distinct materials actually in the scene ===");
            foreach (var kv in seen.OrderByDescending(k => k.Value))
            {
                var m = kv.Key;
                string assetPath = AssetDatabase.GetAssetPath(m);
                sb.AppendLine(string.Format(
                    "MAT|{0}|uses={1}|asset={2}|shader={3}|_BaseMap={4}|_MainTex={5}|_Surface={6}|_Cull={7}|_BaseColor={8}",
                    m.name,
                    kv.Value,
                    string.IsNullOrEmpty(assetPath) ? "<embedded/none>" : assetPath,
                    m.shader != null ? m.shader.name : "<null>",
                    Describe(m, "_BaseMap"),
                    Describe(m, "_MainTex"),
                    F(m, "_Surface"),
                    F(m, "_Cull"),
                    m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString() : "n/a"));
            }

            sb.AppendLine();
            sb.AppendLine("=== renderers: world bounds, mesh UV channels, material ===");
            foreach (var r in renderers.OrderByDescending(r => Footprint(r)))
            {
                var mf = r.GetComponent<MeshFilter>();
                var mesh = mf != null ? mf.sharedMesh : null;
                int uv = mesh != null ? mesh.uv.Length : -1;
                int uv2 = mesh != null && mesh.uv2.Length > 0 ? mesh.uv2.Length : 0;
                var b = r.bounds;
                sb.AppendLine(string.Format(
                    "REN|{0}|mesh={1}|verts={2}|uv={3}|uv2={4}|size={5:F1},{6:F1},{7:F1}|min={8:F0},{9:F0},{10:F0}|mats={11}",
                    Path(r.transform),
                    mesh != null ? mesh.name : "<none>",
                    mesh != null ? mesh.vertexCount : 0,
                    uv,
                    uv2,
                    b.size.x, b.size.y, b.size.z,
                    b.min.x, b.min.y, b.min.z,
                    string.Join(",", r.sharedMaterials.Select(x => x == null ? "<null>" : x.name))));
            }

            File.WriteAllText(OutPath, sb.ToString());
            Debug.Log("[ConcerthallDiag] wrote " + OutPath + " (" + sb.Length + " chars)");
        }

        static float Footprint(Renderer r)
        {
            var s = r.bounds.size;
            return s.x * s.z;
        }

        static string F(Material m, string p)
        {
            return m.HasProperty(p) ? m.GetFloat(p).ToString("0.##") : "n/a";
        }

        static string Describe(Material m, string p)
        {
            if (!m.HasProperty(p))
                return "<no prop>";

            var t = m.GetTexture(p);
            if (t == null)
                return "<null>";

            var t2 = t as Texture2D;
            return t2 != null
                ? t2.name + "(" + t2.width + "x" + t2.height + ",fmt=" + t2.format + ")"
                : t.name + "(non-2d)";
        }

        static string Path(Transform t)
        {
            var sb = new StringBuilder(t.name);
            var p = t.parent;
            while (p != null)
            {
                sb.Insert(0, p.name + "/");
                p = p.parent;
            }

            return sb.ToString();
        }
    }
}
