using System;
using Tactics.Common.Cells;

namespace Tactics.Common.Interactables
{
    /// <summary>
    /// 战场可交互对象的统一接口。
    /// 与 IUnit 并列，不代表可行动战斗单位，而是战场中可占格、可选中、可交互的存在物。
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// 对象所在格子。
        /// </summary>
        ICell CurrentCell { get; set; }

        /// <summary>
        /// 是否占据格子（尸体/宝箱/木桶=true，金币=false）。
        /// </summary>
        bool OccupiesCell { get; }

        /// <summary>
        /// 是否可被选中。
        /// </summary>
        bool Selectable { get; }

        /// <summary>
        /// 执行交互（打开/拾取/破坏/消耗等）。
        /// </summary>
        void Interact();

        /// <summary>
        /// 对象是否已被销毁/消耗。
        /// </summary>
        bool IsDestroyed { get; }
    }
}
