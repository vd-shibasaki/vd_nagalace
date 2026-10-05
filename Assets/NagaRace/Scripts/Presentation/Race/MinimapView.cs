using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NagaRace.Presentation.Race
{
    /// <summary>
    /// 簡易ミニマップ（03 §7 右上）。コースを左→右の模式図として描き、各出走者を点で表示する。
    /// 縦横の縮尺は別にしている（細長いコースでも分岐が読めるように）。
    /// </summary>
    public sealed class MinimapView
    {
        private const float Padding = 14f;

        private readonly RectTransform _root;
        private readonly RaceCourseGeometry _geometry;
        private readonly List<RectTransform> _dots = new List<RectTransform>();
        private float _minZ;
        private float _rangeZ;
        private float _minX;
        private float _rangeX;

        public MinimapView(RectTransform root, RaceCourseGeometry geometry, IReadOnlyList<Color> racerColors, int playerIndex)
        {
            _root = root;
            _geometry = geometry;
            ComputeBounds();
            DrawCourse();

            for (int i = 0; i < racerColors.Count; i++)
            {
                bool isPlayer = i == playerIndex;
                float size = isPlayer ? 18f : 10f;
                if (isPlayer)
                {
                    // 自キャラは黄色の縁取りで目立たせる
                    var outline = UiFactory.Panel($"Dot{i}Outline", root, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (size + 6f), new Color(1f, 0.85f, 0.1f));
                    var dot = UiFactory.Panel($"Dot{i}", outline.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * size, racerColors[i]);
                    _dots.Add(outline.rectTransform);
                }
                else
                {
                    _dots.Add(UiFactory.Panel($"Dot{i}", root, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * size, racerColors[i]).rectTransform);
                }
            }

            // 自キャラの点を最前面へ
            if (playerIndex >= 0 && playerIndex < _dots.Count) _dots[playerIndex].SetAsLastSibling();
        }

        public void UpdateDots(IReadOnlyList<Vector3> worldPositions)
        {
            for (int i = 0; i < _dots.Count && i < worldPositions.Count; i++)
            {
                _dots[i].anchoredPosition = ToMap(worldPositions[i]);
            }
        }

        private void ComputeBounds()
        {
            float minZ = float.MaxValue, maxZ = float.MinValue, minX = float.MaxValue, maxX = float.MinValue;
            foreach (var segment in _geometry.Course.Segments)
            {
                for (int i = 0; i <= 10; i++)
                {
                    var p = _geometry.Point(segment, segment.Length * i / 10.0, 0f);
                    minZ = Mathf.Min(minZ, p.z);
                    maxZ = Mathf.Max(maxZ, p.z);
                    minX = Mathf.Min(minX, p.x);
                    maxX = Mathf.Max(maxX, p.x);
                }
            }

            _minZ = minZ;
            _rangeZ = Mathf.Max(1f, maxZ - minZ);
            _minX = minX;
            _rangeX = Mathf.Max(1f, maxX - minX);
        }

        private void DrawCourse()
        {
            foreach (var segment in _geometry.Course.Segments)
            {
                const int samples = 12;
                var previous = ToMap(_geometry.Point(segment, 0, 0f));
                var color = RiverBuilder.WaterColor(segment);
                color.a = 0.95f;
                for (int i = 1; i <= samples; i++)
                {
                    var current = ToMap(_geometry.Point(segment, segment.Length * i / samples, 0f));
                    UiFactory.Line($"Course {segment.Id} {i}", _root, previous, current, 5f, color);
                    previous = current;
                }
            }

            var goal = _geometry.Course.GoalSegment;
            var goalPoint = ToMap(_geometry.Point(goal, goal.Length, 0f));
            UiFactory.Panel("Goal", _root, Vector2.zero, new Vector2(0.5f, 0.5f), goalPoint, new Vector2(6f, 22f), Color.white);
        }

        /// <summary>ワールド座標 → ミニマップ内座標（左下原点）。コースの進行方向（Z）を横軸にとる。</summary>
        private Vector2 ToMap(Vector3 world)
        {
            var size = _root.rect.size;
            float u = (world.z - _minZ) / _rangeZ;
            float v = (world.x - _minX) / _rangeX;
            return new Vector2(
                Padding + u * (size.x - Padding * 2f),
                Padding + (1f - v) * (size.y - Padding * 2f));
        }
    }
}
