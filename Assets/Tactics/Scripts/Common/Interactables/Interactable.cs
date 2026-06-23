using Tactics.Common.Cells;
using UnityEngine;

namespace Tactics.Common.Interactables
{
    /// <summary>
    /// 战场可交互对象的最小基类（MonoBehaviour）。
    /// 承载位置、占格、可选中、交互入口、基础销毁状态。
    /// 不含回合、阵营、Buff、技能、战斗数值。
    /// </summary>
    public abstract class Interactable : MonoBehaviour, IInteractable
    {
        private ICell _currentCell;
        private bool _isDestroyed;

        public ICell CurrentCell
        {
            get => _currentCell;
            set => _currentCell = value;
        }

        public virtual bool OccupiesCell => true;

        public virtual bool Selectable => true;

        public bool IsDestroyed => _isDestroyed;

        /// <summary>
        /// 执行交互，由子类实现具体行为。
        /// </summary>
        public abstract void Interact();

        /// <summary>
        /// 标记为已销毁，从格子移除并触发清理。
        /// </summary>
        public void Destroy()
        {
            if (_isDestroyed) return;
            _isDestroyed = true;
            OnDestroyed();
        }

        /// <summary>
        /// 销毁时的清理逻辑，由子类重写。
        /// </summary>
        protected virtual void OnDestroyed()
        {
            if (_currentCell != null)
            {
                _currentCell.RemoveInteractable(this);
                _currentCell = null;
            }

            if (gameObject != null)
                Destroy(gameObject);
        }
    }
}
