using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tactics.Common.Cells;
using UnityEngine;

namespace Tactics.Common.Units.Highlight
{
    /// <summary>
    /// Pure C# manager for applying highlight effects to units.
    /// Created and owned by Unit, does not inherit MonoBehaviour.
    /// </summary>
    public class UnitHighlightManager
    {
        private readonly Unit _unit;
        private readonly UnitHighlightConfigs _configs;
        private readonly CancellationTokenSource _highlightCancellationTokenSource = new();

        public UnitHighlightManager(Unit unit, UnitHighlightConfigs configs)
        {
            _unit = unit;
            _configs = configs;
        }

        /// <summary>
        /// Removes any visual highlights or marks on the unit.
        /// </summary>
        public virtual async Task UnMark()
        {
            await ApplyHighlight(_configs.unMarkConfig);
        }

        /// <summary>
        /// Applies a visual highlight to indicate that the unit is selected.
        /// </summary>
        public virtual async Task MarkAsSelected()
        {
            await ApplyHighlight(_configs.selectedConfig);
        }

        /// <summary>
        /// Applies a visual highlight to indicate that the unit is friendly.
        /// </summary>
        public virtual async Task MarkAsFriendly()
        {
            await ApplyHighlight(_configs.friendlyConfig);
        }

        /// <summary>
        /// Applies a visual highlight to indicate that the unit has completed its actions for the turn.
        /// </summary>
        public virtual async Task MarkAsFinished()
        {
            await ApplyHighlight(_configs.finishedConfig);
        }

        /// <summary>
        /// Applies a visual highlight to indicate that the unit can be targeted for actions such as attacks.
        /// </summary>
        public virtual async Task MarkAsTargetable()
        {
            await ApplyHighlight(_configs.targetableConfig);
        }

        /// <summary>
        /// Applies a visual highlight to indicate that the unit is attacking another unit.
        /// </summary>
        public virtual async Task MarkAsAttacking(Unit otherUnit)
        {
            if (_highlightCancellationTokenSource.IsCancellationRequested)
                return;

            var combatParams = new CombatHighlightParams(_unit, otherUnit);
            await ApplyHighlight(_configs.attackingConfig, combatParams);
        }

        /// <summary>
        /// Applies a visual highlight to indicate that the unit is defending against an attack.
        /// </summary>
        public virtual async Task MarkAsDefending(Unit otherUnit)
        {
            if (_highlightCancellationTokenSource.IsCancellationRequested)
                return;

            var combatParams = new CombatHighlightParams(_unit, otherUnit);
            await ApplyHighlight(_configs.defendingConfig, combatParams);
        }

        /// <summary>
        /// Applies a visual effect to indicate that the unit is moving.
        /// </summary>
        public virtual async Task MarkAsMoving(ICell source, ICell destination, IEnumerable<ICell> path)
        {
            var moveParams = new MoveHighlightParams(source, destination, path);
            await ApplyHighlight(_configs.movingConfig, moveParams);
        }

        /// <summary>
        /// Removes the visual indication of movement from the unit.
        /// </summary>
        public virtual async Task UnMarkAsMoving(ICell source, ICell destination, IEnumerable<ICell> path)
        {
            var moveParams = new MoveHighlightParams(source, destination, path);
            await ApplyHighlight(_configs.unMovingConfig, moveParams);
        }

        /// <summary>
        /// Applies a visual effect to indicate that the unit is destroyed.
        /// </summary>
        public virtual async Task MarkAsDestroyed()
        {
            await ApplyHighlight(_configs.destroyedConfig);
        }

        /// <summary>
        /// Applies all enabled effects from a highlight configuration.
        /// </summary>
        private Task ApplyHighlight(HighlightConfig config, IHighlightParams @params = null)
        {
            var tcs = new TaskCompletionSource<bool>();
            if (_unit == null)
            {
                tcs.SetResult(false);
                return tcs.Task;
            }
            // Fast path: if no visual effects configured, complete immediately
            if (!HasAnyVisualEffect(config))
            {
                tcs.SetResult(true);
                return tcs.Task;
            }
            _unit.StartCoroutine(ApplyHighlightRoutine(config, @params ?? NoParam.Instance, tcs));
            return tcs.Task;
        }

        private static bool HasAnyVisualEffect(HighlightConfig config)
        {
            return (config.scaling && config.target != null)
                || (config.color && config.targetSprite != null)
                || (config.animation && config.animator != null)
                || (config.delayEffect && config.delaySeconds > 0)
                || (config.activate && config.targetObj != null)
                || config.activateMulti
                || (config.spinning && config.spinTarget != null)
                || (config.rendererColor && config.renderer != null)
                || (config.spriteOrder && config.orderSprite != null);
        }

        private IEnumerator ApplyHighlightRoutine(HighlightConfig config, IHighlightParams @params, TaskCompletionSource<bool> tcs)
        {
            var token = _highlightCancellationTokenSource.Token;

            if (config.scaling && config.target != null)
            {
                var originalScale = config.target.localScale;
                var elapsed = 0f;
                while (elapsed < config.duration)
                {
                    if (token.IsCancellationRequested) { tcs.SetCanceled(); yield break; }
                    elapsed += Time.deltaTime;
                    var t = Mathf.Clamp01(elapsed / config.duration);
                    config.target.localScale = Vector3.Lerp(originalScale, config.targetValue, t);
                    yield return null;
                }
            }

            if (config.color && config.targetSprite != null)
            {
                config.targetSprite.color = config.colorValue;
            }

            if (config.animation && config.animator != null)
            {
                config.animator.SetTrigger(config.parameter);
                if (config.delay > 0)
                {
                    if (token.IsCancellationRequested) { tcs.SetCanceled(); yield break; }
                    yield return new WaitForSeconds(config.delay);
                }
            }

            if (config.delayEffect && config.delaySeconds > 0)
            {
                if (token.IsCancellationRequested) { tcs.SetCanceled(); yield break; }
                yield return new WaitForSeconds(config.delaySeconds);
            }

            if (config.activate && config.targetObj != null)
            {
                config.targetObj.SetActive(config.status);
            }

            if (config.activateMulti)
            {
                foreach (var target in config.targets)
                    target?.SetActive(config.multiStatus);
            }

            if (config.sway && config.swayTarget != null)
            {
                var original = config.swayTarget.position;
                var elapsed = 0f;
                while (elapsed < config.swayDuration)
                {
                    if (token.IsCancellationRequested) { config.swayTarget.position = original; tcs.SetCanceled(); yield break; }
                    elapsed += Time.deltaTime;
                    var sway = Mathf.Sin(elapsed * config.swayFrequency * Mathf.PI * 2) * config.swayAmplitude;
                    config.swayTarget.position = original + new Vector3(sway, 0, 0);
                    yield return null;
                }
                config.swayTarget.position = original;
            }

            if (config.spinning && config.spinTarget != null)
            {
                var elapsed = 0f;
                var axis = config.spinAxis.normalized;
                while (elapsed < config.spinDuration)
                {
                    if (token.IsCancellationRequested) { tcs.SetCanceled(); yield break; }
                    config.spinTarget.Rotate(axis, config.spinSpeed * Time.deltaTime);
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            if (config.rendererColor && config.renderer != null)
            {
                config.renderer.material.color = config.rendererColorValue;
            }

            if (config.spriteOrder && config.orderSprite != null)
            {
                config.orderSprite.sortingOrder = config.orderValue;
            }

            tcs.SetResult(true);
        }

        /// <summary>
        /// Cancels all ongoing highlight operations.
        /// </summary>
        public void CancelAllHighlights()
        {
            _highlightCancellationTokenSource.Cancel();
        }

        /// <summary>
        /// Releases resources used by the highlight manager.
        /// </summary>
        public void Dispose()
        {
            _highlightCancellationTokenSource.Cancel();
            _highlightCancellationTokenSource.Dispose();
        }
    }
}