using System.Collections.Generic;
using NagaRace.Domain.Race;
using UnityEngine;

namespace NagaRace.Presentation.Race
{
    /// <summary>
    /// RaceCourseGeometry から川面・岸・スタート/ゴール線のメッシュを生成する（仮アート）。
    /// 水の見た目（flow map / foam 等, 06 §13）は M9 で差し替える。
    /// </summary>
    public static class RiverBuilder
    {
        private const float SampleSpacing = 4f;
        private const float BankWidth = 18f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public static Transform Build(RaceCourseGeometry geometry, Material waterMaterial, Material bankMaterial, Material lineMaterial)
        {
            var root = new GameObject("River").transform;
            CreateGround(geometry, bankMaterial, root);
            var segments = geometry.Course.Segments;
            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                // 分岐・合流付近で面が重なってちらつかないよう、Segment ごとに高さをわずかにずらす
                float layer = i * 0.004f;
                var bank = CreateStrip($"Bank {segment.Id}", geometry, segment, BankWidth, -0.2f + layer, bankMaterial, root);
                SetColor(bank, new Color(0.62f, 0.56f, 0.40f));
                var water = CreateStrip($"Water {segment.Id}", geometry, segment, 0f, layer, waterMaterial, root);
                SetColor(water, WaterColor(segment));
            }

            CreateRunOut(geometry, waterMaterial, bankMaterial, root);
            CreateLine("StartLine", geometry, geometry.Course.StartSegment, 0.0, lineMaterial, root);
            var goal = geometry.Course.GoalSegment;
            CreateLine("GoalLine", geometry, goal, goal.Length, lineMaterial, root);
            return root;
        }

        /// <summary>ゴールの先の流れ（ゴール後に流れていく先が途切れないように）。</summary>
        private static void CreateRunOut(RaceCourseGeometry geometry, Material waterMaterial, Material bankMaterial, Transform parent)
        {
            const float length = 45f;
            var goal = geometry.Course.GoalSegment;
            var start = geometry.Point(goal, goal.Length, 0f);
            var forward = geometry.Tangent(1.0);
            float width = (float)goal.Width;

            var bank = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(bank.GetComponent<Collider>());
            bank.name = "RunOutBank";
            bank.transform.SetParent(parent, false);
            bank.transform.SetPositionAndRotation(start + forward * (length / 2f) + Vector3.down * 0.3f, Quaternion.LookRotation(forward));
            bank.transform.localScale = new Vector3(width + BankWidth * 2f, 0.2f, length);
            bank.GetComponent<Renderer>().sharedMaterial = bankMaterial;
            SetColor(bank, new Color(0.62f, 0.56f, 0.40f));

            var water = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(water.GetComponent<Collider>());
            water.name = "RunOutWater";
            water.transform.SetParent(parent, false);
            water.transform.SetPositionAndRotation(start + forward * (length / 2f) + Vector3.down * 0.1f, Quaternion.LookRotation(forward));
            water.transform.localScale = new Vector3(width, 0.2f, length);
            water.GetComponent<Renderer>().sharedMaterial = waterMaterial;
            SetColor(water, WaterColor(goal));
        }

        /// <summary>コース全体の下に敷く草地。</summary>
        private static void CreateGround(RaceCourseGeometry geometry, Material material, Transform parent)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Object.Destroy(ground.GetComponent<Collider>());
            ground.name = "Ground";
            ground.transform.SetParent(parent, false);
            var center = geometry.Center(0.5);
            ground.transform.position = new Vector3(center.x, center.y - 0.4f, center.z);
            // Plane は 10m 四方
            ground.transform.localScale = new Vector3(60f, 1f, geometry.Length / 10f + 60f);
            SetColor(ground, new Color(0.42f, 0.66f, 0.36f));
        }

        public static Color WaterColor(FlowSegment segment)
        {
            if (segment.HasTag(SegmentTags.Waterfall)) return new Color(0.82f, 0.92f, 1f);
            if (segment.HasTag(SegmentTags.Whirlpool)) return new Color(0.38f, 0.36f, 0.70f);
            if (segment.HasTag(SegmentTags.Rapid)) return new Color(0.50f, 0.80f, 0.97f);
            if (segment.HasTag(SegmentTags.Narrow) || segment.HasTag(SegmentTags.Shortcut)) return new Color(0.35f, 0.58f, 0.62f);
            return new Color(0.26f, 0.55f, 0.85f);
        }

        private static GameObject CreateStrip(string name, RaceCourseGeometry geometry, FlowSegment segment, float extraWidth, float yOffset, Material material, Transform parent)
        {
            int samples = Mathf.Max(2, Mathf.CeilToInt((float)segment.Length / SampleSpacing) + 1);
            var vertices = new List<Vector3>(samples * 2);
            var uvs = new List<Vector2>(samples * 2);
            var triangles = new List<int>((samples - 1) * 6);

            for (int i = 0; i < samples; i++)
            {
                double offset = segment.Length * i / (samples - 1);
                double progress = segment.ProgressAt(offset);
                float half = geometry.WidthAt(segment, offset) / 2f + extraWidth;
                var center = geometry.Point(segment, offset, 0f) + Vector3.up * yOffset;
                var right = geometry.Right(progress);
                vertices.Add(center - right * half);
                vertices.Add(center + right * half);
                float v = (float)(offset / 10.0);
                uvs.Add(new Vector2(0f, v));
                uvs.Add(new Vector2(1f, v));

                if (i > 0)
                {
                    int a = (i - 1) * 2;
                    triangles.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
                }
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        private static void CreateLine(string name, RaceCourseGeometry geometry, FlowSegment segment, double offset, Material material, Transform parent)
        {
            var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(line.GetComponent<Collider>());
            line.name = name;
            line.transform.SetParent(parent, false);
            double progress = segment.ProgressAt(offset);
            line.transform.position = geometry.Point(segment, offset, 0f) + Vector3.up * 0.05f;
            line.transform.rotation = Quaternion.LookRotation(geometry.Tangent(progress));
            line.transform.localScale = new Vector3((float)segment.Width + 2f, 0.1f, 0.8f);
            line.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void SetColor(GameObject go, Color color)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            go.GetComponent<Renderer>().SetPropertyBlock(block);
        }
    }
}
