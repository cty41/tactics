using Tactics.Common.Cells;

namespace Tactics.Common.Interactables
{
    /// <summary>
    /// 尸体：战场中由敌人死亡生成的可交互对象。
    /// 占格、可选中、可被死灵法术消耗。
    /// </summary>
    public sealed class Corpse : Interactable
    {
        public override bool OccupiesCell => true;
        public override bool Selectable => true;

        public override void Interact()
        {
            Consume();
        }

        /// <summary>
        /// 消耗尸体（例如被死灵法术用于召唤骷髅）。
        /// </summary>
        public void Consume()
        {
            Destroy();
        }
    }
}
