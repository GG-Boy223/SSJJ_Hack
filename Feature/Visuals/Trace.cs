using SkyDome.Cfg;
using SkyDome.Entity;
using SkyDome.Render;
using System.Collections.Generic;
using UnityEngine;

namespace SkyDome.Feature.Visuals
{
    public class Trace : MonoBehaviour
    {
        private class TracerData
        {
            public Vector3 Start;
            public Vector3 End;
            public Color Color;
            public float CreateTime;
            public float Duration;
            public int ShotIndex;

            public TracerData(Vector3 start, Vector3 end, Color color, float duration, int shotIndex)
            {
                Start = start;
                End = end;
                Color = color;
                CreateTime = Time.time;
                Duration = duration;
                ShotIndex = shotIndex;
            }

            public bool IsExpired()
            {
                return Time.time - CreateTime >= Duration;
            }
        }

        private static List<TracerData> _activeTracers = new List<TracerData>();
        private int _lastShotIndex = 0;

        private void Update()
        {
            _activeTracers.RemoveAll(tracer => tracer.IsExpired());

            CheckForNewShot();
        }

        private void OnGUI()
        {
            if (Camera.main is null || !Config.ShowTracers) return;

            foreach (var tracer in _activeTracers)
            {
                ImmediateRenderer.DrawLinearTracer(tracer.Start, tracer.End, tracer.Color);
            }
        }

        private void CheckForNewShot()
        {
            if (Contexts.sharedInstance?.weapon == null) return;

            var weaponEntity = Contexts.sharedInstance.weapon.currentWeaponEntity;
            if (weaponEntity == null ||
                weaponEntity.basicInfo == null ||
                weaponEntity.basicInfo.Data == null)
                return;

            int currentShotIndex = weaponEntity.basicInfo.Data.ShotsFired;

            if (currentShotIndex == 0)
            {
                _lastShotIndex = 0;
                return;
            }

            if (currentShotIndex > _lastShotIndex)
            {
                CreateTracer(currentShotIndex);

                _lastShotIndex = currentShotIndex;
            }
        }

        private void CreateTracer(int shotIndex)
        {
            if (PlayerUpdate.MainCamera == null && Contexts.sharedInstance.player.myPlayerEntity.currentWeapon.Weapon > 2)
            {
                return;
            }

            Vector3 start = PlayerUpdate.MainCamera.transform.position;
            Vector3 direction = PlayerUpdate.MainCamera.transform.forward;
            Ray ray = new Ray(start, direction);
            float maxDistance = 5000f;
            Vector3 end;
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
            {
                end = hit.point;
            }
            else
            {
                end = start + direction * maxDistance;
            }

            Trace.AddTracer(start, end, Color.black, 1f, shotIndex);
        }

        public static void AddTracer(Vector3 start, Vector3 end, Color color, float duration, int shotIndex)
        {
            _activeTracers.Add(new TracerData(start, end, color, duration, shotIndex));
        }

        public static void ClearAllTracers()
        {
            _activeTracers.Clear();
        }
    }
}