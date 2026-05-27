using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Tactics.Editor.RoguelikeEventEditor
{
    /// <summary>
    /// 左侧事件列表面板。按区域分组显示事件树。
    /// </summary>
    public class EventBlackboard : VisualElement
    {
        private readonly List<SerializableEventData> _events = new List<SerializableEventData>();
        private readonly Dictionary<string, Foldout> _regionGroups = new();
        private readonly Dictionary<string, VisualElement> _eventRows = new();
        private string _selectedEventId;

        public event Action<SerializableEventData> OnEventSelected;
        public event Action<SerializableEventData> OnEventAdded;

        public IReadOnlyList<SerializableEventData> Events => _events;
        public string SelectedEventId => _selectedEventId;

        public EventBlackboard()
        {
            style.flexGrow = 1; style.minWidth = 160;
            style.backgroundColor = new UnityEngine.Color(0.16f, 0.16f, 0.18f);
            BuildUI();
        }

        private void BuildUI()
        {
            // 标题栏
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.paddingTop = 4; header.style.paddingBottom = 4;
            header.style.paddingLeft = 8; header.style.paddingRight = 4;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new UnityEngine.Color(0.3f, 0.3f, 0.3f);

            var label = new Label("Event List") { style = { unityFontStyleAndWeight = UnityEngine.FontStyle.Bold, fontSize = 12 } };
            var addBtn = new Button(() => CreateNewEvent()) { text = "+", style = { width = 22, height = 22, marginLeft = new StyleLength(StyleKeyword.Auto) } };
            addBtn.tooltip = "New Event";
            header.Add(label); header.Add(addBtn);
            Add(header);

            // 区域分组
            var scroll = new ScrollView();
            foreach (var region in EventRegions.All)
            {
                var foldout = new Foldout
                {
                    text = $"📁 {EventRegions.DisplayNames[System.Array.IndexOf(EventRegions.All, region)]}",
                    value = true,
                    style = { marginLeft = 4, marginRight = 4, marginTop = 2 }
                };
                _regionGroups[region] = foldout;
                scroll.Add(foldout);
            }
            Add(scroll);
        }

        public void AddEvent(SerializableEventData evt)
        {
            if (evt == null || string.IsNullOrEmpty(evt.eventId)) return;
            _events.Add(evt);
            if (_regionGroups.TryGetValue(evt.region, out var group))
            {
                var row = MakeRow(evt);
                group.Add(row);
                _eventRows[evt.eventId] = row;
            }
            OnEventAdded?.Invoke(evt);
        }

        public void RemoveEvent(string eventId)
        {
            var evt = _events.Find(e => e.eventId == eventId);
            if (evt == null) return;
            _events.Remove(evt);
            if (_eventRows.TryGetValue(eventId, out var row))
            {
                row.RemoveFromHierarchy();
                _eventRows.Remove(eventId);
            }
            if (_selectedEventId == eventId) _selectedEventId = null;
        }

        public SerializableEventData GetEvent(string eventId) => _events.Find(e => e.eventId == eventId);

        public void UpdateEvent(SerializableEventData evt)
        {
            if (evt == null || string.IsNullOrEmpty(evt.eventId)) return;

            var idx = _events.FindIndex(e => e.eventId == evt.eventId);
            if (idx >= 0) _events[idx] = evt;

            if (_eventRows.TryGetValue(evt.eventId, out var row))
            {
                var lbl = row.Q<Label>();
                if (lbl != null) lbl.text = evt.title ?? evt.eventId;
            }
        }

        private VisualElement MakeRow(SerializableEventData evt)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, paddingLeft = 12, paddingTop = 2, paddingBottom = 2 } };
            row.name = $"row-{evt.eventId}";
            row.RegisterCallback<ClickEvent>(_ => SelectEvent(evt.eventId));

            var lbl = new Label(evt.title ?? evt.eventId) { style = { flexGrow = 1, fontSize = 11, color = new UnityEngine.Color(0.85f, 0.85f, 0.85f) } };
            var delBtn = new Button(() => RemoveEvent(evt.eventId)) { text = "×", style = { width = 16, height = 16, fontSize = 10, marginRight = 4 } };
            delBtn.tooltip = "Delete";
            row.Add(lbl); row.Add(delBtn);
            return row;
        }

        public void SelectEvent(string eventId)
        {
            if (_selectedEventId != null && _eventRows.TryGetValue(_selectedEventId, out var old))
                old.style.backgroundColor = StyleKeyword.Null;
            _selectedEventId = eventId;
            if (_eventRows.TryGetValue(eventId, out var cur))
                cur.style.backgroundColor = new UnityEngine.Color(0.15f, 0.25f, 0.4f);
            OnEventSelected?.Invoke(GetEvent(eventId));
        }

        public void CreateNewEvent()
        {
            var evt = new SerializableEventData
            {
                eventId = $"event_{_events.Count + 1:D3}",
                title = "新事件", region = EventRegions.DarkForest,
                nodes = new List<EventNodeData>
                {
                    new() { nodeId = "start_1", type = EventNodeTypes.Start, data = new() { eventId = $"event_{_events.Count + 1:D3}", title = "New Event", region = EventRegions.DarkForest } },
                    new() { nodeId = "end_1", type = EventNodeTypes.End, data = new() { summaryText = "Event ends" } }
                },
                connections = new List<EventConnectionData> { new() { from = "start_1", to = "end_1", port = "out" } }
            };
            AddEvent(evt);
            SelectEvent(evt.eventId);
        }

        public void SaveSessionState() { }
    }
}
