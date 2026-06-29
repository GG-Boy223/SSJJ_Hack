using Assets.Sources.Constant;
using cakeslice;
using Entitas;
using SkyDome.Cfg;
using SkyDome.Entity;
using System.Collections.Generic;
using UnityEngine;

namespace SkyDome.Feature.Visuals
{
    public class Chams : MonoBehaviour
    {
        private OutlineEffect _outlineEffect;
        private Camera _mainCamera;

        private void Start()
        {
            InitializeOutlineEffect();
        }

        private void InitializeOutlineEffect()
        {
            _mainCamera = PlayerUpdate.MainCamera;
            if (_mainCamera == null) return;

            _outlineEffect = _mainCamera.GetComponent<OutlineEffect>();
            if (_outlineEffect == null)
            {
                _outlineEffect = _mainCamera.gameObject.AddComponent<OutlineEffect>();
            }
        }

        private void Update()
        {
            if (!ValidateComponents()) return;

            ConfigureOutlineEffect();
            UpdatePlayerOutlines();
        }

        private bool ValidateComponents()
        {
            if (_outlineEffect != null && _mainCamera != null) return true;

            InitializeOutlineEffect();
            return _outlineEffect != null && _mainCamera != null;
        }

        private void ConfigureOutlineEffect()
        {
            _outlineEffect.addLinesBetweenColors = false;
            _outlineEffect.lineColor0 = new Color(0.9f, 0.5f, 0.75f, 1.0f);
            _outlineEffect.lineColor1 = Color.clear;
            _outlineEffect.lineColor2 = Color.clear;
            _outlineEffect.additiveRendering = false;
            _outlineEffect.cornerOutlines = true;
            _outlineEffect.fillAmount = 0f;
            _outlineEffect.lineThickness = 0.4f;
            _outlineEffect.lineIntensity = 2f;
            _outlineEffect.alphaCutoff = 0.9f;
            _outlineEffect.backfaceCulling = true;
        }

        private void UpdatePlayerOutlines()
        {
            // 空检查
            if (PlayerUpdate.LocalEntity == null || PlayerUpdate.EntityList == null)
                return;

            int localTeam = PlayerUpdate.LocalEntity.Team;

            foreach (var player in PlayerUpdate.EntityList)
            {
                if (player == null) continue;
                UpdatePlayerOutline(player, localTeam);
            }
        }

        private void UpdatePlayerOutline(PlayerInfo player, int localTeam)
        {
            if (player.Team == localTeam) return;

            var renderers = GetPlayerRenderers(player);
            if (renderers == null || renderers.Length == 0) return;

            if (!player.IsDead && Config.Chams)
            {
                ApplyOutlines(renderers);
            }
            else
            {
                RemoveOutlines(renderers);
            }
        }

        private SkinnedMeshRenderer[] GetPlayerRenderers(PlayerInfo player)
        {
            if (player.ThirdPersonUnityObjects == null) return null;

            return RuleUtilty.EnableAvater()
                ? player.ThirdPersonUnityObjects.ThirdTran?.BodyTransform?.gameObject
                    .GetComponentsInChildren<SkinnedMeshRenderer>()
                : player.ThirdPersonUnityObjects.CareerSkins?.ToArray();
        }

        private void ApplyOutlines(SkinnedMeshRenderer[] renderers)
        {
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;

                if (!renderer.TryGetComponent<Outline>(out _))
                {
                    renderer.gameObject.AddComponent<Outline>();
                }
            }
        }

        private void RemoveOutlines(SkinnedMeshRenderer[] renderers)
        {
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;

                var outline = renderer.GetComponent<Outline>();
                if (outline != null)
                {
                    Destroy(outline);
                }
            }
        }
    }
}