using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Net.Http;
using UnityEngine;
using SkyDome.Entity;
using Newtonsoft.Json;
using NetData;

namespace SkyDome.Feature
{
    //public class AIChatBot : MonoBehaviour
    //{
    //    // API 配置
    //    private const string API_URL = "https://www.ivn.me/v1/chat/completions";
    //    private const string API_KEY = "<API_KEY>";
    //    private const string MODEL_NAME = "claude-opus-4-5-20251101";

    //    // HTTP 客户端
    //    private static readonly HttpClient _httpClient = new HttpClient();

    //    // 消息历史
    //    private static readonly Queue<ChatMessage> _messageHistory = new Queue<ChatMessage>();
    //    private const int MAX_HISTORY = 20;

    //    // 冷却时间
    //    private float _lastSendTime = 0f;
    //    private const float SEND_COOLDOWN = 3f;

    //    // 正在处理的请求
    //    private bool _isProcessing = false;

    //    // 超时检测
    //    private const float REQUEST_TIMEOUT = 15f;
    //    private float _requestStartTime = 0f;

    //    private void Start()
    //    {
    //        _httpClient.Timeout = TimeSpan.FromSeconds(REQUEST_TIMEOUT);
    //        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {API_KEY}");

    //        ////global::System.Console.WriteLine("[AI聊天] AI 聊天机器人已启动（支持对局信息）");
    //    }

    //    // 获取当前对局信息
    //    private string GetCurrentGameState()
    //    {
    //        try
    //        {
    //            StringBuilder gameInfo = new StringBuilder();
    //            gameInfo.AppendLine("当前对局信息");

    //            //  获取队伍比分
    //            var gameRule = Contexts.sharedInstance?.gameRule;
    //            if (gameRule != null && gameRule.hasBroadcastScore)
    //            {
    //                var score = gameRule.broadcastScore;
    //                gameInfo.AppendLine($"比分: 蓝队 {score.Team2Score} - {score.Team1Score} 红队");
    //                gameInfo.AppendLine($"击杀: 蓝队 {score.Team2Kill} - {score.Team1Kill} 红队");
    //            }

    //            // 获取玩家列表和状态
    //            if (Contexts.sharedInstance?.battleRoom?.playerInfo?.All != null)
    //            {
    //                var allPlayers = Contexts.sharedInstance.battleRoom.playerInfo.All;
    //                gameInfo.AppendLine($"\n玩家总数: {allPlayers.Count}");

    //                // 统计存活人数
    //                int team1Alive = 0, team2Alive = 0;
    //                int team1Total = 0, team2Total = 0;

    //                foreach (var player in allPlayers)
    //                {
    //                    if (player.Team == 1) // 红队
    //                    {
    //                        team1Total++;
    //                        if (!player.IsDead) team1Alive++;
    //                    }
    //                    else if (player.Team == 2) // 蓝队
    //                    {
    //                        team2Total++;
    //                        if (!player.IsDead) team2Alive++;
    //                    }
    //                }

    //                gameInfo.AppendLine($"红队存活: {team1Alive}/{team1Total}");
    //                gameInfo.AppendLine($"蓝队存活: {team2Alive}/{team2Total}");

    //                // 红队玩家详情
    //                gameInfo.AppendLine("\n红队玩家:");
    //                foreach (var player in allPlayers)
    //                {
    //                    if (player.Team == 1) // 红队
    //                    {
    //                        string status = player.IsDead ? "[死亡]" : "[存活]";
    //                        gameInfo.AppendLine($"  {status} {player.PlayerName} - K:{player.KillNum} D:{player.BeKillNum} A:{player.AssistsNum}");
    //                    }
    //                }

    //                // 蓝队玩家详情
    //                gameInfo.AppendLine("\n蓝队玩家:");
    //                foreach (var player in allPlayers)
    //                {
    //                    if (player.Team == 2) // 蓝队
    //                    {
    //                        string status = player.IsDead ? "[死亡]" : "[存活]";
    //                        gameInfo.AppendLine($"  {status} {player.PlayerName} - K:{player.KillNum} D:{player.BeKillNum} A:{player.AssistsNum}");
    //                    }
    //                }
    //            }

    //            // 3. 获取本地玩家信息
    //            if (PlayerUpdate.LocalEntity != null)
    //            {
    //                string myTeam = PlayerUpdate.LocalEntity.Team == 1 ? "红队" : (PlayerUpdate.LocalEntity.Team == 2 ? "蓝队" : "未知");
    //                string myName = PlayerUpdate.LocalEntity.PlayerName;

    //                gameInfo.AppendLine("\n我的状态:");
    //                gameInfo.AppendLine($"  游戏名字: {myName}");
    //                gameInfo.AppendLine($"  队伍: {myTeam}");
    //                gameInfo.AppendLine($"  生命值: {PlayerUpdate.LocalEntity.Hp:F0}/{PlayerUpdate.LocalEntity.MaxHp:F0} ({PlayerUpdate.LocalEntity.HpPercent * 100:F0}%)");
    //                gameInfo.AppendLine($"  当前武器: {PlayerUpdate.LocalEntity.Weapon}");
    //                gameInfo.AppendLine($"  是否存活: {(PlayerUpdate.LocalEntity.IsDead ? "否" : "是")}");
    //            }

    //            // 4. C4信息
    //            if (gameRule != null && gameRule.hasC4State && gameRule.c4State.Active)
    //            {
    //                int remainingTime = 35000 - gameRule.c4State.Time;
    //                float seconds = remainingTime / 1000f;
    //                gameInfo.AppendLine($"\n⚠️ C4已安放！剩余时间: {seconds:F1}秒");
    //            }

    //            return gameInfo.ToString();
    //        }
    //        catch (Exception ex)
    //        {
    //            ////global::System.Console.WriteLine($"[AI聊天] 获取对局信息失败: {ex.Message}");
    //            return "对局信息暂时无法获取";
    //        }
    //    }

    //    // 处理接收到的聊天消息
    //    public void ProcessChatMessage(string sender, string content, string msgType)
    //    {
    //        // 只处理全体和队伍频道
    //        if (msgType != "battle_all" && msgType != "battle_team")
    //            return;

    //        // 过滤掉自己发送的消息
    //        if (sender == "我" || string.IsNullOrEmpty(sender))
    //            return;

    //        // 检查冷却时间
    //        if (Time.time - _lastSendTime < SEND_COOLDOWN)
    //        {
    //            ////global::System.Console.WriteLine($"[AI聊天] 冷却中，跳过消息: {sender}: {content}");
    //            return;
    //        }

    //        // 如果正在处理，跳过
    //        if (_isProcessing)
    //        {
    //            ////global::System.Console.WriteLine($"[AI聊天] 正在处理中，跳过消息: {sender}: {content}");
    //            return;
    //        }

    //        // 添加到历史记录
    //        AddToHistory(sender, content, msgType);

    //        // 异步请求 AI 回复
    //        StartCoroutine(GetAIResponse(sender, content, msgType));
    //    }

    //    // 添加消息到历史记录
    //    private void AddToHistory(string sender, string content, string msgType)
    //    {
    //        var message = new ChatMessage
    //        {
    //            role = "user",
    //            content = $"[{GetChannelName(msgType)}] {sender}: {content}"
    //        };

    //        _messageHistory.Enqueue(message);

    //        while (_messageHistory.Count > MAX_HISTORY)
    //        {
    //            _messageHistory.Dequeue();
    //        }
    //    }

    //    // 获取频道名称
    //    private string GetChannelName(string msgType)
    //    {
    //        return msgType == "battle_all" ? "全体" : "队伍";
    //    }

    //    // 移除思考标签
    //    private string RemoveThinkingTags(string text)
    //    {
    //        if (string.IsNullOrEmpty(text))
    //            return text;

    //        var regex = new Regex(@"<思考>.*?</思考>", RegexOptions.Singleline);
    //        return regex.Replace(text, "").Trim();
    //    }

    //    // 请求 AI 生成回复
    //    private IEnumerator GetAIResponse(string sender, string content, string msgType)
    //    {
    //        _isProcessing = true;
    //        _requestStartTime = Time.time;

    //        ////global::System.Console.WriteLine($"[AI聊天] 收到消息: {sender}: {content}");

    //        // 每次请求前更新对局信息
    //        string currentGameState = GetCurrentGameState();
    //        ////global::System.Console.WriteLine($"[AI聊天] 当前对局状态:\n{currentGameState}");

    //        // 构建消息列表
    //        var messages = new List<ChatMessage>();

    //        // 系统提示词
    //        string systemPrompt = "你是个喜欢玩游戏的猫娘，说话自然随意，喜欢在句末加'喵'。" +
    //                             "陈述句用句号+喵（是这样喵。），问句感叹句直接加喵（好厉害喵！）。" +
    //                             "回复别超过50字，不用emoji和动作描写，就像普通玩家聊天那样轻松自在喵~\n\n" +
    //                             "注意：回复绝对绝对绝对绝对不能使用这些违禁词 因为这些是游戏的违禁词 发不出去：躺平、服务器、测试、近、平、收、开、进、枪、真、平、杀、C4、习、尽。\n" +
    //                             "回复不需要[全体][队伍]这些前缀。\n\n" +
    //                             "你可以看到当前对局的实时信息：\n" + currentGameState;

    //        messages.Add(new ChatMessage
    //        {
    //            role = "system",
    //            content = systemPrompt
    //        });

    //        // 添加历史消息
    //        foreach (var msg in _messageHistory)
    //        {
    //            messages.Add(msg);
    //        }

    //        // 构建请求体
    //        var requestBody = new AIRequest
    //        {
    //            model = MODEL_NAME,
    //            messages = messages.ToArray(),
    //            max_tokens = 100,
    //            temperature = 0.8f,
    //            stream = false
    //        };

    //        string jsonRequest = JsonConvert.SerializeObject(requestBody, Formatting.Indented);

    //        ////global::System.Console.WriteLine($"[AI聊天] ===== 开始发送API请求 =====");
    //        ////global::System.Console.WriteLine($"[AI聊天] 请求URL: {API_URL}");
    //        ////global::System.Console.WriteLine($"[AI聊天] 请求体:\n{jsonRequest}");
    //        ////global::System.Console.WriteLine($"[AI聊天] ===============================");

    //        // 创建 HTTP 请求
    //        using (var request = new HttpRequestMessage(HttpMethod.Post, API_URL))
    //        {
    //            request.Content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

    //            HttpResponseMessage response = null;

    //            // 发送请求
    //            var sendTask = _httpClient.SendAsync(request);

    //            // 超时检测
    //            while (!sendTask.IsCompleted)
    //            {
    //                if (Time.time - _requestStartTime > REQUEST_TIMEOUT)
    //                {
    //                    ////global::System.Console.WriteLine($"[AI聊天] 请求超时（{REQUEST_TIMEOUT}秒），判断失败");
    //                    _isProcessing = false;
    //                    yield break;
    //                }
    //                yield return null;
    //            }

    //            if (sendTask.IsFaulted)
    //            {
    //                ////global::System.Console.WriteLine($"[AI聊天] 请求失败: {sendTask.Exception?.Message}");
    //                _isProcessing = false;
    //                yield break;
    //            }

    //            response = sendTask.Result;

    //            // 读取响应
    //            var readTask = response.Content.ReadAsStringAsync();
    //            yield return new WaitUntil(() => readTask.IsCompleted);

    //            if (!response.IsSuccessStatusCode)
    //            {
    //                ////global::System.Console.WriteLine($"[AI聊天] API 错误: {response.StatusCode} - {readTask.Result}");
    //                _isProcessing = false;
    //                yield break;
    //            }

    //            string jsonResponse = readTask.Result;

    //            ////global::System.Console.WriteLine($"[AI聊天] ===== 收到API响应 =====");
    //            ////global::System.Console.WriteLine($"[AI聊天] 响应体:\n{jsonResponse}");
    //            ////global::System.Console.WriteLine($"[AI聊天] =============================");

    //            try
    //            {
    //                // 解析响应
    //                var aiResponse = JsonConvert.DeserializeObject<AIResponse>(jsonResponse);

    //                if (aiResponse?.choices == null || aiResponse.choices.Length == 0)
    //                {
    //                    ////global::System.Console.WriteLine("[AI聊天] API 响应格式错误");
    //                    _isProcessing = false;
    //                    yield break;
    //                }

    //                string aiReply = aiResponse.choices[0].message.content.Trim();

    //                // 过滤掉思考标签
    //                aiReply = RemoveThinkingTags(aiReply);

    //                // 如果过滤后为空，跳过
    //                if (string.IsNullOrWhiteSpace(aiReply))
    //                {
    //                    ////global::System.Console.WriteLine("[AI聊天] AI 回复为空，跳过发送");
    //                    _isProcessing = false;
    //                    yield break;
    //                }

    //                ////global::System.Console.WriteLine($"[AI聊天] AI 回复: {aiReply}");

    //                // 发送到游戏聊天
    //                SendChatMessage(aiReply, msgType);

    //                // 添加 AI 回复到历史
    //                _messageHistory.Enqueue(new ChatMessage
    //                {
    //                    role = "assistant",
    //                    content = aiReply
    //                });

    //                // 更新发送时间
    //                _lastSendTime = Time.time;
    //            }
    //            catch (Exception ex)
    //            {
    //                ////global::System.Console.WriteLine($"[AI聊天] 解析响应失败: {ex.Message}");
    //            }
    //        }

    //        _isProcessing = false;
    //    }

    //    /// <summary>
    //    /// 发送消息到游戏聊天
    //    /// </summary>
    //    private void SendChatMessage(string message, string msgType)
    //    {
    //        try
    //        {
    //            string channel = msgType == "battle_all" ? "battle_all" : "battle_team";
    //            Say.SendServerMessage(message, channel);
    //        }
    //        catch (Exception ex)
    //        {
    //            ////global::System.Console.WriteLine($"[AI聊天] 发送消息失败: {ex.Message}");
    //        }
    //    }

    //    private void OnDestroy()
    //    {
    //        _httpClient?.Dispose();
    //    }

    //    #region 数据结构

    //    [Serializable]
    //    private class AIRequest
    //    {
    //        public string model = "";
    //        public ChatMessage[] messages = Array.Empty<ChatMessage>();
    //        public int max_tokens;
    //        public float temperature;
    //        public bool stream;
    //    }

    //    [Serializable]
    //    private class ChatMessage
    //    {
    //        public string role = "";
    //        public string content = "";
    //    }

    //    [Serializable]
    //    private class AIResponse
    //    {
    //        public Choice[] choices = Array.Empty<Choice>();
    //    }

    //    [Serializable]
    //    private class Choice
    //    {
    //        public ChatMessage message = new ChatMessage();
    //    }

    //    #endregion
    //}
}
