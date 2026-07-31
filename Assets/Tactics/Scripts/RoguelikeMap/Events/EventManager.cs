using System;
using System.Collections.Generic;
using System.Linq;
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
        public void LoadRegionEvents(string regionName, RoguelikeMapConfig config)
        {
            if (HasLoadedEvents(regionName))
                return;

            if (config == null)
            {
                TLog.Warning($"[EventManager] 配置为空: {regionName}");
                return;
            }

            var events = LoadEventsFromAssets(config.eventFiles);
            if (events.Count == 0 && config.eventPaths != null && config.eventPaths.Count > 0)
            {
                TLog.Warning($"[EventManager] 事件文件引用为空或未能加载，改用运行时事件路径: {regionName}");
                events = LoadEventsFromPaths(config.eventPaths);
            }

            CacheRegionEvents(regionName, events);
        }

        /// <summary>
        /// 通过GameAssetManager加载指定区域的所有事件（动态路径方式）
        /// 使用此方法加载时，eventFiles在Inspector中分配优先于此方法
        /// </summary>
        /// <param name="regionName">区域名称</param>
        /// <param name="eventPaths">资产路径列表（如"Assets/Tactics/GameData/Events/DarkForest/cursed_chest_001.json"）</param>
        public void LoadRegionEventsFromPaths(string regionName, List<string> eventPaths)
        {
            if (HasLoadedEvents(regionName))
                return;

            CacheRegionEvents(regionName, LoadEventsFromPaths(eventPaths));
        }

        private bool HasLoadedEvents(string regionName)
        {
            if (!_regionEvents.TryGetValue(regionName, out var events))
                return false;

            if (events.Count > 0)
            {
                TLog.Info($"[EventManager] 区域 {regionName} 的事件已加载");
                return true;
            }

            _regionEvents.Remove(regionName);
            TLog.Warning($"[EventManager] 丢弃区域 {regionName} 的空事件缓存并允许重试");
            return false;
        }

        private List<RoguelikeEvent> LoadEventsFromAssets(List<TextAsset> eventFiles)
        {
            var events = new List<RoguelikeEvent>();
            if (eventFiles == null)
                return events;

            foreach (var file in eventFiles)
            {
                if (file == null)
                    continue;

                try
                {
                    AddEvent(events, RoguelikeEvent.FromJson(file.text));
                }
                catch (Exception e)
                {
                    TLog.Error($"[EventManager] 加载事件文件失败: {file.name}, 错误: {e.Message}");
                }
            }

            return events;
        }

        private List<RoguelikeEvent> LoadEventsFromPaths(List<string> eventPaths)
        {
            var events = new List<RoguelikeEvent>();
            if (eventPaths == null || eventPaths.Count == 0)
            {
                TLog.Warning("[EventManager] 事件路径列表为空");
                return events;
            }

            foreach (var path in eventPaths)
            {
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                TextAsset file = null;
                try
                {
                    file = GameAssetManager.Instance?.Load<TextAsset>(path);
                    if (file == null)
                    {
                        TLog.Warning($"[EventManager] GameAssetManager无法加载: {path}");
                        continue;
                    }

                    AddEvent(events, RoguelikeEvent.FromJson(file.text));
                }
                catch (Exception e)
                {
                    TLog.Error($"[EventManager] 通过GameAssetManager加载事件文件失败: {path}, 错误: {e.Message}");
                }
                finally
                {
                    if (file != null)
                        GameAssetManager.Instance.Release(path);
                }
            }

            return events;
        }

        private void AddEvent(List<RoguelikeEvent> events, RoguelikeEvent evt)
        {
            if (evt == null)
                return;

            events.Add(evt);
            _events[evt.eventId] = evt;
            TLog.Info($"[EventManager] 加载事件: {evt.eventId} - {evt.title}");
        }

        private void CacheRegionEvents(string regionName, List<RoguelikeEvent> events)
        {
            if (events == null || events.Count == 0)
            {
                TLog.Warning($"[EventManager] 区域 {regionName} 未加载到事件，不缓存空目录");
                return;
            }

            _regionEvents[regionName] = events;
            TLog.Info($"[EventManager] 区域 {regionName} 共加载 {events.Count} 个事件");
        }

        /// <summary>
        /// 清除指定区域的缓存事件（用于重新加载）
        /// </summary>
        public void ClearRegion(string regionName)
        {
            _regionEvents.Remove(regionName);
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
        /// Selects an event from a stable, ID-sorted region pool.
        /// </summary>
        public RoguelikeEvent GetDeterministicEvent(string regionName, int seed)
        {
            if (!_regionEvents.TryGetValue(regionName, out var events) || events.Count == 0)
                return null;

            var ordered = events
                .Where(evt => evt != null)
                .OrderBy(evt => evt.eventId, StringComparer.Ordinal)
                .ToList();
            if (ordered.Count == 0)
                return null;

            return ordered[new System.Random(seed).Next(ordered.Count)];
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

        public bool IsRegionLoaded(string regionName)
        {
            return _regionEvents.ContainsKey(regionName);
        }

    }
}
