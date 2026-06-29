using Assets.Sources.Chat;
using Assets.Sources.Framework;
using Assets.Sources.Framework.System;
using Assets.Sources.Modules.Ui.Chat;
using SkyDome.Cfg;
using SkyDome.Extension;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SkyDome.Feature
{
    public class Say : MonoBehaviour
    {
        private float _lastSendTime;

        // 消息类型枚举
        public enum MessageType
        {
            Vip, // VIP消息
            BattleAll, // 战场全体消息
            BattleObserverAll, // 观战者全体
            BattleTeam, // 阵营/队伍消息
            Team, // 小队消息
            Personal, // 私聊
            System, // 系统消息
            Prompt, // 提示信息
            TacticsSound, // 战术语音
            PlayerLogin, // 玩家进场提示
            PlayerLogout, // 玩家退场提示
            BigHorn2, // 大喇叭
            BigHorn3, // 超级大喇叭
            LiveBarrage, // 直播弹幕
            LiveGift, // 直播送礼
            Hononary1, // 荣誉播报1
            Hononary2 // 荣誉播报2
        }

        // 枚举映射
        private static string GetMsgTypeString(MessageType type)
        {
            switch (type)
            {
                case MessageType.Vip: return "vip";
                case MessageType.BattleAll: return "battle_all";
                case MessageType.BattleObserverAll: return "battle_observer_all";
                case MessageType.BattleTeam: return "battle_team";
                case MessageType.Team: return "team";
                case MessageType.Personal: return "personal";
                case MessageType.System: return "system";
                case MessageType.Prompt: return "prompt";
                case MessageType.TacticsSound: return "tacticsSound";
                case MessageType.PlayerLogin: return "playerlogin";
                case MessageType.PlayerLogout: return "playerlogout";
                case MessageType.BigHorn2: return "big_horn2";
                case MessageType.BigHorn3: return "big_horn3";
                case MessageType.LiveBarrage: return "live_barrage";
                case MessageType.LiveGift: return "live_gift";
                case MessageType.Hononary1: return "hononary1";
                case MessageType.Hononary2: return "hononary2";
                default: return "system";
            }
        }

        private void Start()
        {
            // 订阅击中事件
            GlobalEvents.OnPlayerHit += OnHitCallback;
        }

        private void OnDestroy()
        {
            // 取消订阅
            GlobalEvents.OnPlayerHit -= OnHitCallback;
        }

        // 击中喊话
        private void OnHitCallback(NetData.GameServerSetupData data)
        {
            SendLocalMessage(MessageType.System, "", "命中目标");
        }

        // 自动喊话
        private void Update()
        {
            if (Config.Say)
            {
                if (Time.time - _lastSendTime >= 3.0f)
                {
                    SendServerMessage(Config.SendMsg, "battle_all");
                    _lastSendTime = Time.time;
                }
            }
        }

        // 获取聊天系统实例
        private static ChatJobSystem GetChatJobSystem()
        {
            var instance = GameModuleFeature.Instance;
            if (instance == null) return null;

            var playbackSystem = instance.GetFieldValue<PlaybackSystem>("_playbackSystem");
            if (playbackSystem == null) return null;

            var systems = playbackSystem.GetFieldValue<List<IPlaybackSystem>>("_systems");
            if (systems == null) return null;

            return systems.FirstOrDefault(s => s.GetType() == typeof(ChatJobSystem)) as ChatJobSystem;
        }

        /// <summary>
        /// 发送真实消息给服务器
        /// </summary>
        public static void SendServerMessage(string message, string chatChannel = "battle_all")
        {
            var chatSystem = GetChatJobSystem();
            if (chatSystem == null) return;

            var data = new ChatInputData
            {
                SenderInputContent = message,
                SenderType = chatChannel,
                ReceiverName = string.Empty,
                ReceiverCid = string.Empty
            };

            chatSystem.InvokeMethod("SendChatInfo", new object[] { data });
        }

        /// <summary>
        /// 发送本地假消息
        /// </summary>
        /// <param name="type">消息类型</param>
        /// <param name="senderName">发送者名字</param>
        /// <param name="messageContent">消息内容</param>
        public static void SendLocalMessage(MessageType type, string senderName, string messageContent)
        {
            var chatSystem = GetChatJobSystem();
            if (chatSystem == null) return;

            ChatHistroyData chatHistroyData = default;

            // 设置类型字符串
            chatHistroyData.MsgType = GetMsgTypeString(type);

            chatHistroyData.ReceiverName = string.Empty;
            chatHistroyData.ReceiverCid = string.Empty;
            chatHistroyData.SenderName = senderName;
            chatHistroyData.SenderBody = messageContent;

            // 设置显示时长参数
            chatHistroyData.AlphaData.RemainTime = 6000;
            chatHistroyData.AlphaData.AlphaRemainTime = 100;

            // 调用本地接收函数
            chatSystem.InvokeMethod("OnRecvChatInfo", new object[] { chatHistroyData });
        }
    }
}
