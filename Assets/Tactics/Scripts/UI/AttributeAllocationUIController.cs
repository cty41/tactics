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
            ClearUIElementReferences();
        }

        private void EnsureUIElements()
        {
            var currentRoot = Ui.GetRootElement(UIManager.UIId.AttributeAllocation);
            if (ReferenceEquals(_root, currentRoot) && _root != null) return;

            UnregisterEvents();
            ClearUIElementReferences();
            _root = currentRoot;
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
                row.PlusCallback = () => OnAttributePlus(attrType);
                row.MinusCallback = () => OnAttributeMinus(attrType);
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
                var row = kvp.Value;

                if (row.PlusButton != null)
                    row.PlusButton.clicked += row.PlusCallback;

                if (row.MinusButton != null)
                    row.MinusButton.clicked += row.MinusCallback;
            }

            var confirmButton = _root?.Q<Button>("ConfirmButton");
            if (confirmButton != null)
                confirmButton.clicked += OnConfirm;
        }

        private void UnregisterEvents()
        {
            foreach (var kvp in _attributeRows)
            {
                var row = kvp.Value;

                if (row.PlusButton != null)
                    row.PlusButton.clicked -= row.PlusCallback;

                if (row.MinusButton != null)
                    row.MinusButton.clicked -= row.MinusCallback;
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

                int allocated = _currentCharacter.AllocatedAttributes.GetValueOrDefault(attrType, 0);
                int effectiveValue = GetAttributeValue(attrType);

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

            // 加点系统每点只增加目标属性 1；撤销时必须严格对称。
            switch (type)
            {
                case AttributeType.Strength:
                    _currentCharacter.Strength--;
                    break;
                case AttributeType.Agility:
                    _currentCharacter.Agility--;
                    break;
                case AttributeType.Intelligence:
                    _currentCharacter.Intelligence--;
                    break;
                case AttributeType.Constitution:
                    _currentCharacter.Constitution--;
                    break;
                case AttributeType.Charisma:
                    _currentCharacter.Charisma--;
                    break;
                case AttributeType.Luck:
                    _currentCharacter.Luck--;
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
            if (_currentCharacter.AttributePoints > 0)
                return;

            TLog.Info($"[AttributeAllocationUI] 角色 {_currentCharacter.DisplayName} 属性加点已确认。剩余点数: {_currentCharacter.AttributePoints}");

            // 关闭界面
            Ui.Hide(UIManager.UIId.AttributeAllocation);
        }

        private void ClearUIElementReferences()
        {
            _root = null;
            _characterNameLabel = null;
            _pointsRemainingLabel = null;
            _confirmButtonLabel = null;
            _attributeRows.Clear();
        }

        /// <summary>
        /// 获取角色当前已生效的属性值。
        /// </summary>
        private int GetAttributeValue(AttributeType type)
        {
            if (_currentCharacter == null) return 0;

            return type switch
            {
                AttributeType.Strength => _currentCharacter.Strength,
                AttributeType.Agility => _currentCharacter.Agility,
                AttributeType.Intelligence => _currentCharacter.Intelligence,
                AttributeType.Constitution => _currentCharacter.Constitution,
                AttributeType.Charisma => _currentCharacter.Charisma,
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
            public System.Action PlusCallback { get; set; }
            public System.Action MinusCallback { get; set; }
        }
    }
}
