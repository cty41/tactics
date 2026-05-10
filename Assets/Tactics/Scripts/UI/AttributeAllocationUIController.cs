using System.Collections.Generic;
using Tactics.Common.Battle;
using Tactics.Roster;
using Tactics.Runtime.Utilities;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.UI
{
    /// <summary>
    /// 属性加点界面 Controller。
    /// 管理角色属性点的分配与撤销，并配合 <see cref="AttributePointSystem"/> 应用属性变更。
    /// </summary>
    public sealed class AttributeAllocationUIController : UIControllerBase
    {
        private VisualElement _root;
        private Label _characterNameLabel;
        private Label _pointsRemainingLabel;
        private Label _confirmButtonLabel;

        private CharacterDefinition _currentCharacter;

        /// <summary>存储每种属性对应的 UI 元素引用。</summary>
        private readonly Dictionary<AttributeType, AttributeRowElements> _attributeRows = new();

        protected override void OnShown()
        {
            base.OnShown();
            EnsureUIElements();
            RegisterEvents();
            if (_currentCharacter != null)
                RefreshUI();
        }

        protected override void OnHidden()
        {
            UnregisterEvents();
        }

        private void EnsureUIElements()
        {
            if (_root != null) return;

            _root = Ui.GetRootElement(UIManager.UIId.AttributeAllocation);
            if (_root == null) return;

            _characterNameLabel = _root.Q<Label>("CharacterNameLabel");
            _pointsRemainingLabel = _root.Q<Label>("PointsRemainingLabel");

            // 为每种属性类型注册 UI 元素
            var attributeTypes = new[]
            {
                AttributeType.Strength,
                AttributeType.Agility,
                AttributeType.Constitution,
                AttributeType.Intelligence,
                AttributeType.Charisma,
                AttributeType.Luck,
            };

            foreach (var attrType in attributeTypes)
            {
                var row = new AttributeRowElements
                {
                    NameLabel = _root.Q<Label>($"AttrName_{attrType}"),
                    ValueLabel = _root.Q<Label>($"AttrValue_{attrType}"),
                    MinusButton = _root.Q<Button>($"AttrMinus_{attrType}"),
                    AllocatedLabel = _root.Q<Label>($"AttrAllocated_{attrType}"),
                    PlusButton = _root.Q<Button>($"AttrPlus_{attrType}"),
                    DescLabel = _root.Q<Label>($"AttrDesc_{attrType}"),
                };
                _attributeRows[attrType] = row;
            }

            var confirmButton = _root.Q<Button>("ConfirmButton");
            if (confirmButton != null)
                _confirmButtonLabel = confirmButton.Q<Label>();
        }

        private void RegisterEvents()
        {
            foreach (var kvp in _attributeRows)
            {
                var type = kvp.Key;
                var row = kvp.Value;

                if (row.PlusButton != null)
                    row.PlusButton.clicked += () => OnAttributePlus(type);

                if (row.MinusButton != null)
                    row.MinusButton.clicked += () => OnAttributeMinus(type);
            }

            var confirmButton = _root?.Q<Button>("ConfirmButton");
            if (confirmButton != null)
                confirmButton.clicked += OnConfirm;
        }

        private void UnregisterEvents()
        {
            foreach (var kvp in _attributeRows)
            {
                var type = kvp.Key;
                var row = kvp.Value;

                if (row.PlusButton != null)
                    row.PlusButton.clicked -= () => OnAttributePlus(type);

                if (row.MinusButton != null)
                    row.MinusButton.clicked -= () => OnAttributeMinus(type);
            }

            var confirmButton = _root?.Q<Button>("ConfirmButton");
            if (confirmButton != null)
                confirmButton.clicked -= OnConfirm;
        }

        /// <summary>
        /// 设置当前要分配属性的角色。
        /// </summary>
        public void SetCharacter(CharacterDefinition character)
        {
            _currentCharacter = character;
            if (isActiveAndEnabled)
                RefreshUI();
        }

        /// <summary>
        /// 刷新所有属性显示。
        /// </summary>
        public void RefreshUI()
        {
            if (_currentCharacter == null) return;

            if (_characterNameLabel != null)
                _characterNameLabel.text = _currentCharacter.DisplayName;

            if (_pointsRemainingLabel != null)
                _pointsRemainingLabel.text = $"剩余点数：{_currentCharacter.AttributePoints}";

            var attributeTypes = new[]
            {
                AttributeType.Strength,
                AttributeType.Agility,
                AttributeType.Constitution,
                AttributeType.Intelligence,
                AttributeType.Charisma,
                AttributeType.Luck,
            };

            foreach (var attrType in attributeTypes)
            {
                if (!_attributeRows.TryGetValue(attrType, out var row))
                    continue;

                // 基础属性值
                int baseValue = GetBaseAttribute(attrType);
                int allocated = _currentCharacter.AllocatedAttributes.GetValueOrDefault(attrType, 0);
                int effectiveValue = baseValue + allocated;

                // 该属性点分配后实际产生的加成值（仅计算分配的属性点）
                string bonusDesc = allocated > 0
                    ? AttributePointSystem.GetAttributeBonus(attrType, allocated)
                    : AttributePointSystem.GetAttributeDescription(attrType);

                if (row.NameLabel != null)
                    row.NameLabel.text = AttributePointSystem.GetAttributeDisplayName(attrType);

                if (row.ValueLabel != null)
                    row.ValueLabel.text = effectiveValue.ToString();

                if (row.AllocatedLabel != null)
                    row.AllocatedLabel.text = $"+{allocated}";

                if (row.DescLabel != null)
                    row.DescLabel.text = bonusDesc;

                // 按钮状态
                if (row.PlusButton != null)
                    row.PlusButton.SetEnabled(_currentCharacter.AttributePoints > 0);

                if (row.MinusButton != null)
                    row.MinusButton.SetEnabled(allocated > 0);
            }
        }

        /// <summary>
        /// 加点处理。
        /// </summary>
        private void OnAttributePlus(AttributeType type)
        {
            if (_currentCharacter == null) return;

            bool success = AttributePointSystem.ApplyAttributePoint(_currentCharacter, type);
            if (success)
            {
                RefreshUI();
            }
        }

        /// <summary>
        /// 减点处理（撤销已分配的属性点）。
        /// </summary>
        private void OnAttributeMinus(AttributeType type)
        {
            if (_currentCharacter == null) return;

            var allocated = _currentCharacter.AllocatedAttributes.GetValueOrDefault(type, 0);
            if (allocated <= 0) return;

            // 撤销：减少已分配点数，返还属性点
            _currentCharacter.AllocatedAttributes[type] = allocated - 1;
            _currentCharacter.AttributePoints++;

            // 反向应用属性变更
            switch (type)
            {
                case AttributeType.Strength:
                    _currentCharacter.Strength -= 2;
                    break;
                case AttributeType.Agility:
                    _currentCharacter.Agility -= 1;
                    _currentCharacter.Speed -= 1f;
                    break;
                case AttributeType.Intelligence:
                    _currentCharacter.Intelligence -= 2;
                    _currentCharacter.Charisma -= 10;
                    break;
                case AttributeType.Constitution:
                    _currentCharacter.Constitution -= 10;
                    _currentCharacter.DefenceFactor -= 1;
                    break;
                case AttributeType.Charisma:
                    _currentCharacter.Charisma -= 1;
                    _currentCharacter.Luck -= 2;
                    break;
            }

            TLog.Info($"[AttributeAllocationUI] 角色 {_currentCharacter.DisplayName} 撤回 1 点 {AttributePointSystem.GetAttributeDisplayName(type)}，剩余点数: {_currentCharacter.AttributePoints}");

            RefreshUI();
        }

        /// <summary>
        /// 确认加点并触发事件。
        /// </summary>
        private void OnConfirm()
        {
            if (_currentCharacter == null) return;

            TLog.Info($"[AttributeAllocationUI] 角色 {_currentCharacter.DisplayName} 属性加点已确认。剩余点数: {_currentCharacter.AttributePoints}");

            // 关闭界面
            Ui.Hide(UIManager.UIId.AttributeAllocation);
        }

        /// <summary>
        /// 获取指定属性的基础值（不包含已分配的点数）。
        /// </summary>
        private int GetBaseAttribute(AttributeType type)
        {
            if (_currentCharacter == null) return 0;

            int allocated = _currentCharacter.AllocatedAttributes.GetValueOrDefault(type, 0);

            return type switch
            {
                AttributeType.Strength => _currentCharacter.Strength - (allocated * 2),
                AttributeType.Agility => _currentCharacter.Agility - allocated,
                AttributeType.Intelligence => _currentCharacter.Intelligence - (allocated * 2),
                AttributeType.Constitution => _currentCharacter.Constitution - (allocated * 10),
                AttributeType.Charisma => _currentCharacter.Charisma - allocated,
                AttributeType.Luck => _currentCharacter.Luck,
                _ => 0,
            };
        }

        /// <summary>
        /// 单条属性行 UI 元素的容器。
        /// </summary>
        private sealed class AttributeRowElements
        {
            public Label NameLabel { get; set; }
            public Label ValueLabel { get; set; }
            public Button MinusButton { get; set; }
            public Label AllocatedLabel { get; set; }
            public Button PlusButton { get; set; }
            public Label DescLabel { get; set; }
        }
    }
}
