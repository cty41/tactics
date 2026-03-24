using UnityEngine;
using Tactics;

namespace Tactics.UI
{
    /// <summary>
    /// Shared base for Home/Menu/etc UI controllers.
    /// Keeps common access to <see cref="UIManager"/> and optional show/hide hooks.
    /// </summary>
    public abstract class UIControllerBase : MonoBehaviour
    {
        protected UIManager Ui => UIManager.Instance;

        protected virtual void OnEnable()
        {
            OnShown();
        }

        protected virtual void OnDisable()
        {
            OnHidden();
        }

        /// <summary>Called from <see cref="OnEnable"/>.</summary>
        protected virtual void OnShown() { }

        /// <summary>Called from <see cref="OnDisable"/>.</summary>
        protected virtual void OnHidden() { }
    }
}

