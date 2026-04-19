using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Tactics.Common.Units.Highlight
{
    /// <summary>
    /// Interface for highlight parameters.
    /// </summary>
    public interface IHighlightParams
    {
    }

    /// <summary>
    /// Represents a parameter set with no specific data.
    /// </summary>
    public readonly struct NoParam : IHighlightParams
    {
        public static readonly IHighlightParams Instance = new NoParam();
    }

    /// <summary>
    /// Unified highlight configuration containing all possible effect parameters.
    /// Uses direct component references instead of paths for better Inspector workflow.
    /// Each state (UnMark, Selected, etc.) uses one instance of this config.
    /// </summary>
    [Serializable]
    public class HighlightConfig
    {
        [Title("Scaling")]
        [LabelText("Enable")]
        public bool scaling = false;

        [ShowIf("scaling")]
        public Transform target;

        [ShowIf("scaling")]
        [LabelText("Duration")]
        public float duration = 1f;

        [ShowIf("scaling")]
        [LabelText("Target Scale")]
        public Vector3 targetValue = Vector3.one;

        [Title("Color")]
        [LabelText("Enable")]
        public bool color = false;

        [ShowIf("color")]
        public SpriteRenderer targetSprite;

        [ShowIf("color")]
        public Color colorValue = Color.white;

        [Title("Animation")]
        [LabelText("Enable")]
        public bool animation = false;

        [ShowIf("animation")]
        public Animator animator;

        [ShowIf("animation")]
        [LabelText("Trigger")]
        public string parameter = "";

        [ShowIf("animation")]
        [LabelText("Delay")]
        public float delay = 0f;

        [Title("Delay")]
        [LabelText("Enable")]
        public bool delayEffect = false;

        [ShowIf("delayEffect")]
        [LabelText("Seconds")]
        public float delaySeconds = 0f;

        [Title("Activate")]
        [LabelText("Enable")]
        public bool activate = false;

        [ShowIf("activate")]
        public GameObject targetObj;

        [ShowIf("activate")]
        [LabelText("Status")]
        public bool status = true;

        [Title("Multi Activate")]
        [LabelText("Enable")]
        public bool activateMulti = false;

        [ShowIf("activateMulti")]
        public List<GameObject> targets = new();

        [ShowIf("activateMulti")]
        [LabelText("Status")]
        public bool multiStatus = true;

        [Title("Sway")]
        [LabelText("Enable")]
        public bool sway = false;

        [ShowIf("sway")]
        public Transform swayTarget;

        [ShowIf("sway")]
        [LabelText("Duration")]
        public float swayDuration = 1f;

        [ShowIf("sway")]
        [LabelText("Amplitude")]
        public float swayAmplitude = 0.1f;

        [ShowIf("sway")]
        [LabelText("Frequency")]
        public float swayFrequency = 2f;

        [Title("Spinning")]
        [LabelText("Enable")]
        public bool spinning = false;

        [ShowIf("spinning")]
        public Transform spinTarget;

        [ShowIf("spinning")]
        [LabelText("Duration")]
        public float spinDuration = 1f;

        [ShowIf("spinning")]
        [LabelText("Speed")]
        public float spinSpeed = 360f;

        [ShowIf("spinning")]
        [LabelText("Axis")]
        public Vector3 spinAxis = Vector3.up;

        [Title("Renderer Color")]
        [LabelText("Enable")]
        public bool rendererColor = false;

        [ShowIf("rendererColor")]
        public Renderer renderer;

        [ShowIf("rendererColor")]
        [LabelText("Color")]
        public Color rendererColorValue = Color.white;

        [Title("Sprite Order")]
        [LabelText("Enable")]
        public bool spriteOrder = false;

        [ShowIf("spriteOrder")]
        public SpriteRenderer orderSprite;

        [ShowIf("spriteOrder")]
        [LabelText("Order")]
        public int orderValue = 0;
    }

    /// <summary>
    /// Aggregated container for all unit highlight configurations.
    /// Each state has one unified HighlightConfig.
    /// </summary>
    [Serializable]
    public class UnitHighlightConfigs
    {
        [FoldoutGroup("UnMark")]
        public HighlightConfig unMarkConfig = new();

        [FoldoutGroup("Selected")]
        public HighlightConfig selectedConfig = new();

        [FoldoutGroup("Friendly")]
        public HighlightConfig friendlyConfig = new();

        [FoldoutGroup("Finished")]
        public HighlightConfig finishedConfig = new();

        [FoldoutGroup("Targetable")]
        public HighlightConfig targetableConfig = new();

        [FoldoutGroup("Attacking")]
        public HighlightConfig attackingConfig = new();

        [FoldoutGroup("Defending")]
        public HighlightConfig defendingConfig = new();

        [FoldoutGroup("Moving")]
        public HighlightConfig movingConfig = new();

        [FoldoutGroup("UnMoving")]
        public HighlightConfig unMovingConfig = new();

        [FoldoutGroup("Destroyed")]
        public HighlightConfig destroyedConfig = new();
    }
}