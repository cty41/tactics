using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Tactics.AssetPipeline;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.RoguelikeMap.Events
{
    /// <summary>
    /// 事件管理器
    /// 负责加载和管理事件数据
    /// </summary>
    public class EventManager
    {
        private static EventManager _instance;
        public static EventManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new EventManager();
                return _instance;
            }
        }

        private Dictionary<string, RoguelikeEvent> _events = new Dictionary<string, RoguelikeEvent>();
        private Dictionary<string, List<RoguelikeEvent>> _regionEvents = new Dictionary<string, List<RoguelikeEvent>>();

        private EventManager() { }

        /// <summary>
        /// 加载指定区域的所有事件
        /// </summary>
        public void LoadRegionEvents(string regionName)
        {
            if (_regionEvents.ContainsKey(regionName))
            {
                TLog.Info($"[EventManager] 区域 {regionName} 的事件已加载");
                return;
            }

            string[] assetPaths = GetEventAssetPaths(regionName);
            if (assetPaths == null || assetPaths.Length == 0)
            {
                TLog.Warning($"[EventManager] 未找到区域 {regionName} 的事件文件");
                _regionEvents[regionName] = new List<RoguelikeEvent>();
                return;
            }

            List<RoguelikeEvent> events = new List<RoguelikeEvent>();

            foreach (var assetPath in assetPaths)
            {
                TextAsset file = GameAssetManager.Instance.Load<TextAsset>(assetPath);
                if (file == null)
                {
                    TLog.Warning($"[EventManager] 加载事件文件失败: {assetPath}");
                    continue;
                }

                try
                {
                    var evt = RoguelikeEvent.FromJson(file.text);
                    if (evt != null)
                    {
                        events.Add(evt);
                        _events[evt.eventId] = evt;
                        TLog.Info($"[EventManager] 加载事件: {evt.eventId} - {evt.title}");
                    }
                }
                catch (System.Exception e)
                {
                    TLog.Error($"[EventManager] 加载事件文件失败: {assetPath}, 错误: {e.Message}");
                }
                finally
                {
                    GameAssetManager.Instance.Release(assetPath);
                }
            }

            _regionEvents[regionName] = events;
            TLog.Info($"[EventManager] 区域 {regionName} 共加载 {events.Count} 个事件");
        }

        /// <summary>
        /// 获取指定区域的随机事件
        /// </summary>
        public RoguelikeEvent GetRandomEvent(string regionName)
        {
            if (!_regionEvents.TryGetValue(regionName, out var events) || events.Count == 0)
            {
                TLog.Warning($"[EventManager] 区域 {regionName} 没有可用事件");
                return null;
            }

            int index = UnityEngine.Random.Range(0, events.Count);
            return events[index];
        }

        /// <summary>
        /// 根据ID获取事件
        /// </summary>
        public RoguelikeEvent GetEvent(string eventId)
        {
            if (_events.TryGetValue(eventId, out var evt))
                return evt;

            TLog.Warning($"[EventManager] 未找到事件: {eventId}");
            return null;
        }

        /// <summary>
        /// 获取指定区域的事件数量
        /// </summary>
        public int GetEventCount(string regionName)
        {
            if (_regionEvents.TryGetValue(regionName, out var events))
                return events.Count;

            return 0;
        }

        /// <summary>
        /// 清除所有加载的事件
        /// </summary>
        public void ClearEvents()
        {
            _events.Clear();
            _regionEvents.Clear();
            TLog.Info($"[EventManager] 已清除所有事件");
        }

        private static readonly Dictionary<string, string[]> RegionEventPaths = new Dictionary<string, string[]>
        {
            ["DarkForest"] = new[]
            {
                "Assets/Tactics/GameData/Events/DarkForest/cursed_chest_001.json",
                "Assets/Tactics/GameData/Events/DarkForest/fallen_altar_001.json",
                "Assets/Tactics/GameData/Events/DarkForest/lost_villager_001.json",
            }
        };

        private string[] GetEventAssetPaths(string regionName)
        {
            if (RegionEventPaths.TryGetValue(regionName, out var paths))
                return paths;
            return Array.Empty<string>();
        }
    }
}
