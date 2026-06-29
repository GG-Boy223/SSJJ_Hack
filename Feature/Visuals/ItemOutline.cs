using SkyDome.Cfg;
using SkyDome.Entity;
using UnityEngine;
using cakeslice;
using System.Collections.Generic;

namespace SkyDome.Feature.Visuals
{
    public class ItemOutline : MonoBehaviour
    {
        // 用于跟踪已添加Outline的物品ID
        private HashSet<int> _outlinedItems = new HashSet<int>();

        // OutlineEffect引用，用于配置物品轮廓颜色
        private OutlineEffect _outlineEffect;
        private Camera _mainCamera;

        private void Update()
        {
            // 确保OutlineEffect已初始化并配置物品轮廓颜色
            EnsureOutlineEffect();

            // 更新物品轮廓高亮
            UpdateItemOutlines();
        }

        private void EnsureOutlineEffect()
        {
            if (_mainCamera == null)
            {
                _mainCamera = PlayerUpdate.MainCamera;
            }
            if (_mainCamera == null) return;

            if (_outlineEffect == null)
            {
                _outlineEffect = _mainCamera.GetComponent<OutlineEffect>();
                if (_outlineEffect == null)
                {
                    _outlineEffect = _mainCamera.gameObject.AddComponent<OutlineEffect>();
                }
            }

            // 配置物品轮廓颜色（使用lineColor1，黄绿色）
            if (_outlineEffect != null && Config.ShowItemOutline)
            {
                // 设置物品轮廓颜色为亮黄绿色
                _outlineEffect.lineColor1 = new Color(0.5f, 1f, 0.3f, 1f);
            }
        }

        private void UpdateItemOutlines()
        {
            var sceneObjectContext = Contexts.sharedInstance?.sceneObject;
            if (sceneObjectContext == null) return;

            // 当前帧存在的物品ID集合
            HashSet<int> currentItems = new HashSet<int>();

            // 遍历所有掉落物
            foreach (SceneObjectEntity sceneObjectEntity in sceneObjectContext.GetGroup(SceneObjectMatcher.SceneWeapon))
            {
                if (sceneObjectEntity == null || !sceneObjectEntity.hasSceneWeapon)
                    continue;

                var weaponData = sceneObjectEntity.sceneWeapon.Current;
                if (weaponData == null) continue;

                int itemId = weaponData.Id;
                currentItems.Add(itemId);

                // 检查是否有UnityObjects组件
                if (!sceneObjectEntity.hasUnityObjects) continue;

                var loadResults = sceneObjectEntity.unityObjects.LoadResults;
                if (loadResults == null) continue;

                // 尝试获取武器模型GameObject
                GameObject weaponGO = null;

                // 首先尝试使用"WeaponModel"键
                if (loadResults.ContainsKey("WeaponModel"))
                {
                    weaponGO = loadResults["WeaponModel"].GameObject;
                }
                // 如果没有，尝试使用索引0
                else if (loadResults.ContainsKey(0))
                {
                    weaponGO = loadResults[0].GameObject;
                }

                if (weaponGO == null) continue;

                if (Config.ShowItemOutline)
                {
                    // 添加轮廓效果
                    ApplyOutlineToItem(weaponGO, itemId);
                }
                else
                {
                    // 移除轮廓效果
                    RemoveOutlineFromItem(weaponGO, itemId);
                }
            }

            // 清理已不存在的物品记录
            _outlinedItems.RemoveWhere(id => !currentItems.Contains(id));
        }

        private void ApplyOutlineToItem(GameObject weaponGO, int itemId)
        {
            if (_outlinedItems.Contains(itemId)) return;

            // 获取所有SkinnedMeshRenderer组件（与游戏原生系统一致）
            SkinnedMeshRenderer[] skinnedRenderers = weaponGO.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var renderer in skinnedRenderers)
            {
                if (renderer == null) continue;
                if (renderer.GetComponent<Outline>() == null)
                {
                    var outline = renderer.gameObject.AddComponent<Outline>();
                    // 使用颜色索引1（对应OutlineEffect的lineColor1）
                    outline.color = 1;
                }
            }

            // 同时处理MeshRenderer（某些物品可能使用MeshRenderer）
            MeshRenderer[] meshRenderers = weaponGO.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var renderer in meshRenderers)
            {
                if (renderer == null) continue;
                if (renderer.GetComponent<Outline>() == null)
                {
                    var outline = renderer.gameObject.AddComponent<Outline>();
                    outline.color = 1;
                }
            }

            _outlinedItems.Add(itemId);
        }

        private void RemoveOutlineFromItem(GameObject weaponGO, int itemId)
        {
            if (!_outlinedItems.Contains(itemId)) return;

            // 移除所有Outline组件
            Outline[] outlines = weaponGO.GetComponentsInChildren<Outline>(true);
            foreach (var outline in outlines)
            {
                if (outline != null)
                {
                    Destroy(outline);
                }
            }

            _outlinedItems.Remove(itemId);
        }

        private void OnDisable()
        {
            // 禁用时清理所有轮廓效果
            CleanupAllOutlines();
        }

        private void OnDestroy()
        {
            // 销毁时清理所有轮廓效果
            CleanupAllOutlines();
        }

        private void CleanupAllOutlines()
        {
            var sceneObjectContext = Contexts.sharedInstance?.sceneObject;
            if (sceneObjectContext == null) return;

            foreach (SceneObjectEntity sceneObjectEntity in sceneObjectContext.GetGroup(SceneObjectMatcher.SceneWeapon))
            {
                if (sceneObjectEntity == null || !sceneObjectEntity.hasUnityObjects) continue;

                var loadResults = sceneObjectEntity.unityObjects.LoadResults;
                if (loadResults == null) continue;

                GameObject weaponGO = null;
                if (loadResults.ContainsKey("WeaponModel"))
                {
                    weaponGO = loadResults["WeaponModel"].GameObject;
                }
                else if (loadResults.ContainsKey(0))
                {
                    weaponGO = loadResults[0].GameObject;
                }

                if (weaponGO == null) continue;

                Outline[] outlines = weaponGO.GetComponentsInChildren<Outline>(true);
                foreach (var outline in outlines)
                {
                    if (outline != null)
                    {
                        Destroy(outline);
                    }
                }
            }

            _outlinedItems.Clear();
        }
    }
}
