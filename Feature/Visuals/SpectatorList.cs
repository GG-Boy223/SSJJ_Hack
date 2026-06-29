using SkyDome.Cfg;
using SkyDome.Render;
using UnityEngine;

namespace SkyDome.Feature.Visuals
{
    public class SpectatorList : MonoBehaviour
    {
        private void OnGUI()
        {
            if (!Config.ShowWatcher) return;

            var watchers = Contexts.sharedInstance.battleRoom.playerInfo.ObserverList;
            if (watchers == null || watchers.Count == 0) return;

            float itemHeight = 20f;
            float titleHeight = 25f;
            float listHeight = titleHeight + (watchers.Count * itemHeight);
            float startX = 20f;
            float startY = Screen.height / 2 - listHeight / 2;

            // 绘制标题
            ImmediateRenderer.DrawString(
                new Vector2(startX + 25f, startY),
                "观战",
                Color.cyan,
                false,
                15
            );

            // 绘制观战者列表
            for (int i = 0; i < watchers.Count; i++)
            {
                string watcher = watchers[i];
                float yPos = startY + titleHeight + i * itemHeight;

                ImmediateRenderer.DrawString(
                    new Vector2(startX, yPos),
                    $"{watcher}",
                    Color.white,
                    false,
                    15
                );
            }
        }
    }
}
