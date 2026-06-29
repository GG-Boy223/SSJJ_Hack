using SkyDome.Cfg;
using SkyDome.Entity;
using SkyDome.Utilities;
using System.Collections.Generic;
using UnityEngine;

namespace SkyDome.Feature.Visuals
{
    public class DamageDisplay : MonoBehaviour
    {
        // 伤害数字类
        private class DamageNumber
        {
            public float Damage;
            public Vector3 WorldPosition;
            public float CreateTime;
            public float Duration = 1.5f;
            public float RiseSpeed = 50f;

            public float GetAlpha()
            {
                float elapsed = Time.time - CreateTime;
                return Mathf.Clamp01(1f - elapsed / Duration);
            }

            public Vector3 GetCurrentPosition()
            {
                float elapsed = Time.time - CreateTime;
                return WorldPosition + Vector3.up * elapsed * RiseSpeed;
            }

            public bool IsExpired()
            {
                return Time.time - CreateTime >= Duration;
            }
        }

        private Dictionary<int, float> _lastHpCache = new Dictionary<int, float>();
        private List<DamageNumber> _activeDamageNumbers = new List<DamageNumber>();
        private GUIStyle _damageStyle;

        private void Start()
        {
            _damageStyle = new GUIStyle();
            _damageStyle.fontSize = 20;
            _damageStyle.fontStyle = FontStyle.Bold;
            _damageStyle.alignment = TextAnchor.MiddleCenter;
        }

        private void Update()
        {
            if (!Config.ShowDamage || PlayerUpdate.EntityList == null)
            {
                _activeDamageNumbers.Clear();
                return;
            }

            foreach (var player in PlayerUpdate.EntityList)
            {
                if (player == null || player.Team == PlayerUpdate.LocalEntity?.Team)
                    continue;

                int id = player.Id;
                float currentHp = player.Hp;

                if (_lastHpCache.TryGetValue(id, out float lastHp))
                {
                    float hpChange = lastHp - currentHp;

                    if (hpChange > 0.5f)
                    {
                        CreateDamageNumber(player, hpChange);
                    }
                }

                _lastHpCache[id] = currentHp;
            }

            _activeDamageNumbers.RemoveAll(d => d.IsExpired());
        }

        private void OnGUI()
        {
            if (!Config.ShowDamage || PlayerUpdate.MainCamera == null || _damageStyle == null)
                return;

            foreach (var dmg in _activeDamageNumbers)
            {
                DrawDamageNumber(dmg);
            }
        }

        private void CreateDamageNumber(PlayerInfo target, float damage)
        {
            try
            {
                Transform head = target.GetPlayerTransform("Bip01_Head");
                if (head == null) return;

                Vector3 worldPos = head.position + Vector3.up * 0.5f;

                var damageNum = new DamageNumber
                {
                    Damage = damage,
                    WorldPosition = worldPos,
                    CreateTime = Time.time
                };

                _activeDamageNumbers.Add(damageNum);
            }
            catch (System.Exception ex)
            {
                #if Debug_Log
                global::System.Console.WriteLine($"[伤害显示] 创建失败: {ex.Message}");
                #endif
            }
        }

        private void DrawDamageNumber(DamageNumber dmg)
        {
            Vector3 currentWorldPos = dmg.GetCurrentPosition();
            Vector3 screenPos = ViewportUtility.WorldPointToScreenPoint(currentWorldPos);

            if (!ViewportUtility.IsScreenPointVisible(screenPos))
                return;

            float alpha = dmg.GetAlpha();
            string damageText = dmg.Damage.ToString("F0");

            Vector2 pos = new Vector2(screenPos.x, screenPos.y);
            GUIContent content = new GUIContent(damageText);
            Vector2 size = _damageStyle.CalcSize(content);

            Rect textRect = new Rect(
                pos.x - size.x / 2f,
                pos.y - size.y / 2f,
                size.x,
                size.y
            );

            // 描边（黑色半透明）
            Color outlineColor = new Color(0f, 0f, 0f, alpha * 0.5f);
            _damageStyle.normal.textColor = outlineColor;

            GUI.Label(new Rect(textRect.x - 1, textRect.y, size.x, size.y), content, _damageStyle);
            GUI.Label(new Rect(textRect.x + 1, textRect.y, size.x, size.y), content, _damageStyle);
            GUI.Label(new Rect(textRect.x, textRect.y - 1, size.x, size.y), content, _damageStyle);
            GUI.Label(new Rect(textRect.x, textRect.y + 1, size.x, size.y), content, _damageStyle);

            // 主文字
            Color mainColor = new Color(1f, 0.75f, 0.85f, alpha * 0.6f);
            _damageStyle.normal.textColor = mainColor;
            GUI.Label(textRect, content, _damageStyle);
        }
    }
}
