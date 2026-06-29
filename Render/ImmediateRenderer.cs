using SkyDome.Entity;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkyDome.Render
{
    public static class ImmediateRenderer
    {
        private static Material _glMaterial;
        private static GUIStyle _cachedLabelStyle;

        private static void EnsureMaterialInitialized()
        {
            if (_glMaterial != null) return;

            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader is null)
            {
                #if Debug_Log
                global::System.Console.WriteLine("[渲染器] 无法找到所需的着色器");
                #endif
                return;
            }

            _glMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _glMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _glMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _glMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _glMaterial.SetInt("_ZWrite", 0);
        }

        public static void DrawBoxOutline(Rect rect, Color color, float thickness = 1f)
        {
            Vector2 topLeft = new Vector2(rect.x, rect.y);
            Vector2 topRight = new Vector2(rect.x + rect.width, rect.y);
            Vector2 bottomLeft = new Vector2(rect.x, rect.y + rect.height);
            Vector2 bottomRight = new Vector2(rect.x + rect.width, rect.y + rect.height);

            DrawLine(topLeft, topRight, color, thickness);
            DrawLine(bottomLeft, bottomRight, color, thickness);
            DrawLine(topLeft, bottomLeft, color, thickness);
            DrawLine(topRight, bottomRight, color, thickness);
        }

        public static void DrawBoxFilled(Rect rect, Color color)
        {
            EnsureMaterialInitialized();
            if (_glMaterial is null) return;

            Vector2 topLeft = new Vector2(rect.x, rect.y);
            Vector2 topRight = new Vector2(rect.x + rect.width, rect.y);
            Vector2 bottomLeft = new Vector2(rect.x, rect.y + rect.height);
            Vector2 bottomRight = new Vector2(rect.x + rect.width, rect.y + rect.height);

            _glMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();

            GL.Begin(GL.QUADS);
            GL.Color(color);
            GL.Vertex3(topLeft.x, topLeft.y, 0f);
            GL.Vertex3(topRight.x, topRight.y, 0f);
            GL.Vertex3(bottomRight.x, bottomRight.y, 0f);
            GL.Vertex3(bottomLeft.x, bottomLeft.y, 0f);
            GL.End();

            GL.PopMatrix();
        }

        public static void DrawLine(Vector2 start, Vector2 end, Color color, float width = 1f)
        {
            EnsureMaterialInitialized();
            if (_glMaterial is null) return;

            _glMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();

            if (Mathf.Approximately(width, 1f))
            {
                GL.Begin(GL.LINES);
                GL.Color(color);
                GL.Vertex3(start.x, start.y, 0f);
                GL.Vertex3(end.x, end.y, 0f);
                GL.End();
            }
            else
            {
                Vector2 direction = (end - start).normalized;
                Vector2 perpendicular = new Vector2(-direction.y, direction.x) * width / 2f;

                GL.Begin(GL.QUADS);
                GL.Color(color);
                GL.Vertex3(start.x + perpendicular.x, start.y + perpendicular.y, 0f);
                GL.Vertex3(start.x - perpendicular.x, start.y - perpendicular.y, 0f);
                GL.Vertex3(end.x - perpendicular.x, end.y - perpendicular.y, 0f);
                GL.Vertex3(end.x + perpendicular.x, end.y + perpendicular.y, 0f);
                GL.End();
            }

            GL.PopMatrix();
        }

        public static void DrawString(Vector2 pos, string text, Color color,
                                     bool center = false, int fontSize = 12)
        {
            if (string.IsNullOrEmpty(text)) return;

            if (_cachedLabelStyle is null || _cachedLabelStyle.fontSize != fontSize)
            {
                _cachedLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = fontSize,
                    fontStyle = FontStyle.Bold
                };
            }

            GUIContent content = new GUIContent(text);
            Vector2 size = _cachedLabelStyle.CalcSize(content);
            Rect rect = new Rect(pos, size);

            if (center)
            {
                rect.x -= size.x / 2f;
            }

            _cachedLabelStyle.normal.textColor = Color.black;
            GUI.Label(new Rect(rect.x - 1f, rect.y, size.x, size.y), content, _cachedLabelStyle);
            GUI.Label(new Rect(rect.x + 1f, rect.y, size.x, size.y), content, _cachedLabelStyle);
            GUI.Label(new Rect(rect.x, rect.y - 1f, size.x, size.y), content, _cachedLabelStyle);
            GUI.Label(new Rect(rect.x, rect.y + 1f, size.x, size.y), content, _cachedLabelStyle);

            _cachedLabelStyle.normal.textColor = color;
            GUI.Label(rect, content, _cachedLabelStyle);
        }
        public static void DrawCircleOutline(Vector2 position, float radius, int numSides, Color color, bool centered = true)
        {
            EnsureMaterialInitialized();
            if (_glMaterial is null || radius <= 0) return;

            GL.PushMatrix();
            _glMaterial.SetPass(0);
            GL.LoadOrtho();
            GL.Begin(1);
            GL.Color(color);

            float angleStep = 360f / numSides;
            Vector2 centerPoint = (centered ? position : (position + Vector2.one * radius));

            for (int i = 0; i < numSides; i++)
            {
                float startAngle = 0.017453292f * (i * angleStep);
                float endAngle = 0.017453292f * ((i + 1) * angleStep);

                Vector2 startPoint = centerPoint + new Vector2(Mathf.Cos(startAngle), Mathf.Sin(startAngle)) * radius;
                Vector2 endPoint = centerPoint + new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle)) * radius;

                GL.Vertex(new Vector3(startPoint.x / Screen.width, startPoint.y / Screen.height, 0f));
                GL.Vertex(new Vector3(endPoint.x / Screen.width, endPoint.y / Screen.height, 0f));
            }

            GL.End();
            GL.PopMatrix();
        }
        public static void DrawCircleFilled(Vector2 center, float radius, Color color, int segments = 32)
        {
            EnsureMaterialInitialized();
            if (_glMaterial is null || radius <= 0) return;

            _glMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();

            GL.Begin(GL.TRIANGLES);
            GL.Color(color);

            float angleStep = 2f * Mathf.PI / segments;
            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * angleStep;
                float angle2 = (i + 1) * angleStep;

                Vector2 point1 = center + new Vector2(
                    Mathf.Cos(angle1) * radius,
                    Mathf.Sin(angle1) * radius
                );

                Vector2 point2 = center + new Vector2(
                    Mathf.Cos(angle2) * radius,
                    Mathf.Sin(angle2) * radius
                );

                GL.Vertex3(center.x, center.y, 0f);
                GL.Vertex3(point1.x, point1.y, 0f);
                GL.Vertex3(point2.x, point2.y, 0f);
            }

            GL.End();
            GL.PopMatrix();
        }
        public static void DrawFilledTriangle(Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            EnsureMaterialInitialized();
            if (_glMaterial is null) return;

            _glMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();

            GL.Begin(GL.TRIANGLES);
            GL.Color(color);
            GL.Vertex3(a.x, a.y, 0f);
            GL.Vertex3(b.x, b.y, 0f);
            GL.Vertex3(c.x, c.y, 0f);
            GL.End();

            GL.PopMatrix();
        }

        public static void DrawTriangleOutline(Vector2 a, Vector2 b, Vector2 c, Color color, float thickness = 1f)
        {
            DrawLine(a, b, color, thickness);
            DrawLine(b, c, color, thickness);
            DrawLine(c, a, color, thickness);
        }

        public static void DrawPolygon(Vector2[] points, Color color, bool filled = true)
        {
            if (points is null || points.Length < 3) return;

            EnsureMaterialInitialized();
            if (_glMaterial is null) return;

            _glMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();

            if (filled)
            {
                GL.Begin(GL.TRIANGLES);
                GL.Color(color);

                for (int i = 1; i < points.Length - 1; i++)
                {
                    GL.Vertex3(points[0].x, points[0].y, 0f);
                    GL.Vertex3(points[i].x, points[i].y, 0f);
                    GL.Vertex3(points[i + 1].x, points[i + 1].y, 0f);
                }
            }
            else
            {
                GL.Begin(GL.LINES);
                GL.Color(color);

                for (int i = 0; i < points.Length; i++)
                {
                    int next = (i + 1) % points.Length;
                    GL.Vertex3(points[i].x, points[i].y, 0f);
                    GL.Vertex3(points[next].x, points[next].y, 0f);
                }
            }

            GL.End();
            GL.PopMatrix();
        }
        public static void DrawArrow(Vector2 start, Vector2 end, Color color, float headSize = 10f, float shaftWidth = 2f)
        {
            DrawLine(start, end, color, shaftWidth);

            Vector2 direction = (end - start).normalized;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);

            Vector2 arrowHead1 = end - direction * headSize + perpendicular * headSize * 0.5f;
            Vector2 arrowHead2 = end - direction * headSize - perpendicular * headSize * 0.5f;

            DrawFilledTriangle(end, arrowHead1, arrowHead2, color);
        }
        public static void DrawCornerBox(Rect rect, Color color, float thickness = 1f, float cornerLength = 10f, bool autoScale = false)
        {
            float x = rect.x;
            float y = rect.y;
            float width = rect.width;
            float height = rect.height;

            if (width < 0)
            {
                x += width;
                width = -width;
            }

            if (height < 0)
            {
                y += height;
                height = -height;
            }

            float maxCornerLength;
            float actualCornerLength = cornerLength;

            if (autoScale)
            {
                maxCornerLength = Mathf.Min(width / 2, height / 2);
                actualCornerLength = Mathf.Min(cornerLength, maxCornerLength);
            }

            float horizontalLength = Mathf.Min(actualCornerLength, width / 2 - 4f);
            float verticalLength = Mathf.Min(actualCornerLength, height / 2);

            Vector2 topLeft = new Vector2(x, y);
            Vector2 topRight = new Vector2(x + width, y);
            Vector2 bottomLeft = new Vector2(x, y + height);
            Vector2 bottomRight = new Vector2(x + width, y + height);

            DrawLine(topLeft, topLeft + new Vector2(horizontalLength, 0), color, thickness);
            DrawLine(topLeft, topLeft + new Vector2(0, verticalLength), color, thickness);

            DrawLine(topRight, topRight - new Vector2(horizontalLength, 0), color, thickness);
            DrawLine(topRight, topRight + new Vector2(0, verticalLength), color, thickness);

            DrawLine(bottomLeft, bottomLeft + new Vector2(horizontalLength, 0), color, thickness);
            DrawLine(bottomLeft, bottomLeft - new Vector2(0, verticalLength), color, thickness);

            DrawLine(bottomRight, bottomRight - new Vector2(horizontalLength, 0), color, thickness);
            DrawLine(bottomRight, bottomRight - new Vector2(0, verticalLength), color, thickness);
        }
        public static void DrawSectorOutline(Vector2 center, float radius, float startAngleDeg, float endAngleDeg,
                                     int segments, Color color, float thickness = 1f)
        {
            if (radius <= 0 || segments < 3) return;

            startAngleDeg = Mathf.Repeat(startAngleDeg, 360f);
            endAngleDeg = Mathf.Repeat(endAngleDeg, 360f);

            if (endAngleDeg < startAngleDeg)
                endAngleDeg += 360f;

            float totalAngle = endAngleDeg - startAngleDeg;
            float angleStep = totalAngle / segments;

            Vector2 startDir = new Vector2(
                Mathf.Cos(startAngleDeg * Mathf.Deg2Rad),
                Mathf.Sin(startAngleDeg * Mathf.Deg2Rad)
            );

            Vector2 endDir = new Vector2(
                Mathf.Cos(endAngleDeg * Mathf.Deg2Rad),
                Mathf.Sin(endAngleDeg * Mathf.Deg2Rad)
            );

            DrawLine(center, center + startDir * radius, color, thickness);
            DrawLine(center, center + endDir * radius, color, thickness);

            Vector2 prevPoint = center + startDir * radius;

            for (int i = 1; i <= segments; i++)
            {
                float currentAngle = startAngleDeg + i * angleStep;
                Vector2 currentDir = new Vector2(
                    Mathf.Cos(currentAngle * Mathf.Deg2Rad),
                    Mathf.Sin(currentAngle * Mathf.Deg2Rad)
                );

                Vector2 currentPoint = center + currentDir * radius;
                DrawLine(prevPoint, currentPoint, color, thickness);
                prevPoint = currentPoint;
            }
        }

        public static void DrawSectorFilled(Vector2 center, float radius, float startAngleDeg, float endAngleDeg,
                                            int segments, Color color)
        {
            EnsureMaterialInitialized();
            if (_glMaterial is null || radius <= 0 || segments < 3) return;

            startAngleDeg = Mathf.Repeat(startAngleDeg, 360f);
            endAngleDeg = Mathf.Repeat(endAngleDeg, 360f);

            if (endAngleDeg < startAngleDeg)
                endAngleDeg += 360f;

            float totalAngle = endAngleDeg - startAngleDeg;
            float angleStep = totalAngle / segments;

            _glMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();

            GL.Begin(GL.TRIANGLES);
            GL.Color(color);

            Vector2 startDir = new Vector2(
                Mathf.Cos(startAngleDeg * Mathf.Deg2Rad),
                Mathf.Sin(startAngleDeg * Mathf.Deg2Rad)
            );

            Vector2 prevPoint = center + startDir * radius;

            for (int i = 1; i <= segments; i++)
            {
                float currentAngle = startAngleDeg + i * angleStep;
                Vector2 currentDir = new Vector2(
                    Mathf.Cos(currentAngle * Mathf.Deg2Rad),
                    Mathf.Sin(currentAngle * Mathf.Deg2Rad)
                );

                Vector2 currentPoint = center + currentDir * radius;

                GL.Vertex3(center.x, center.y, 0f);
                GL.Vertex3(prevPoint.x, prevPoint.y, 0f);
                GL.Vertex3(currentPoint.x, currentPoint.y, 0f);

                prevPoint = currentPoint;
            }

            GL.End();
            GL.PopMatrix();
        }
        public static void DrawCrosshair(Vector2 center, Color color, float size = 20f,
                                      float thickness = 2f, float gap = 5f,
                                      bool dot = true, bool outline = true)
        {
            Vector2 topStart = new Vector2(center.x, center.y - gap);
            Vector2 topEnd = new Vector2(center.x, center.y - gap - size);
            DrawLine(topStart, topEnd, color, thickness);

            Vector2 bottomStart = new Vector2(center.x, center.y + gap);
            Vector2 bottomEnd = new Vector2(center.x, center.y + gap + size);
            DrawLine(bottomStart, bottomEnd, color, thickness);

            Vector2 leftStart = new Vector2(center.x - gap, center.y);
            Vector2 leftEnd = new Vector2(center.x - gap - size, center.y);
            DrawLine(leftStart, leftEnd, color, thickness);

            Vector2 rightStart = new Vector2(center.x + gap, center.y);
            Vector2 rightEnd = new Vector2(center.x + gap + size, center.y);
            DrawLine(rightStart, rightEnd, color, thickness);

            if (dot)
            {
                DrawCircleFilled(center, thickness * 1.5f, color, 12);
            }

            if (outline)
            {
                Color outlineColor = new Color(0, 0, 0, color.a);
                DrawLine(topStart, topEnd, outlineColor, thickness + 2);
                DrawLine(bottomStart, bottomEnd, outlineColor, thickness + 2);
                DrawLine(leftStart, leftEnd, outlineColor, thickness + 2);
                DrawLine(rightStart, rightEnd, outlineColor, thickness + 2);

                if (dot)
                {
                    DrawCircleOutline(center, thickness * 1.5f + 1, 12, outlineColor, true);
                }
            }
        }
        public static void DrawImpactPoint(Vector2 center, Color color, float size = 4f, bool outline = true)
        {
            DrawCircleFilled(center, size, color, 12);

            if (outline)
            {
                Color outlineColor = new Color(0, 0, 0, color.a);
                DrawCircleOutline(center, size + 1, 12, outlineColor, true);
            }
        }

        public static void DrawLinearTracer(Vector3 start, Vector3 end, Color color)
        {
            if (PlayerUpdate.MainCamera is null) return;

            EnsureMaterialInitialized();

            _glMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadProjectionMatrix(PlayerUpdate.MainCamera.projectionMatrix);
            GL.modelview = PlayerUpdate.MainCamera.worldToCameraMatrix;
            GL.Begin(1);
            GL.Color(color);
            GL.Vertex(start);
            GL.Vertex(end);
            GL.End();
            GL.PopMatrix();
        }
    }
}