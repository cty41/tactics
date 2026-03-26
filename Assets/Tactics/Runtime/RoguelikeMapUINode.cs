using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tactics.RoguelikeMap
{
    public enum NodeStates
    {
        Locked,
        Visited,
        Attainable
    }
}

namespace Tactics.RoguelikeMap
{
    public class RoguelikeMapUINode : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public SpriteRenderer sr;
        public Image image;
        public SpriteRenderer visitedCircle;
        public Image circleImage;
        public Image visitedCircleImage;

        public RoguelikeMapNode Node { get; private set; }
        public RoguelikeNodeBlueprint Blueprint { get; private set; }

        private float initialScale;
        private const float HoverScaleFactor = 1.2f;
        private float mouseDownTime;
        private const float MaxClickDuration = 0.5f;

        private Color visitedColor = Color.white;
        private Color lockedColor = Color.gray;

        public void SetUp(RoguelikeMapNode node, RoguelikeNodeBlueprint blueprint, Color visited, Color locked)
        {
            Node = node;
            Blueprint = blueprint;
            visitedColor = visited;
            lockedColor = locked;
            
            if (sr != null) sr.sprite = blueprint.sprite;
            if (image != null) image.sprite = blueprint.sprite;
            if (node.nodeType == RoguelikeNodeType.Boss) transform.localScale *= 1.5f;
            if (sr != null) initialScale = sr.transform.localScale.x;
            else if (image != null) initialScale = image.transform.localScale.x;
            else initialScale = 1f;

            if (visitedCircle != null)
            {
                visitedCircle.color = visitedColor;
                visitedCircle.gameObject.SetActive(false);
            }

            if (circleImage != null)
            {
                circleImage.color = visitedColor;
                circleImage.gameObject.SetActive(false);    
            }
            
            SetState(NodeStates.Locked);
        }

        public void SetState(NodeStates state)
        {
            if (visitedCircle != null) visitedCircle.gameObject.SetActive(false);
            if (circleImage != null) circleImage.gameObject.SetActive(false);
            
            switch (state)
            {
                case NodeStates.Locked:
                    if (sr != null)
                    {
                        sr.DOKill();
                        sr.color = lockedColor;
                    }

                    if (image != null)
                    {
                        image.DOKill();
                        image.color = lockedColor;
                    }

                    break;
                case NodeStates.Visited:
                    if (sr != null)
                    {
                        sr.DOKill();
                        sr.color = visitedColor;
                    }
                    
                    if (image != null)
                    {
                        image.DOKill();
                        image.color = visitedColor;
                    }
                    
                    if (visitedCircle != null) visitedCircle.gameObject.SetActive(true);
                    if (circleImage != null) circleImage.gameObject.SetActive(true);
                    break;
                case NodeStates.Attainable:
                    if (sr != null)
                    {
                        sr.color = lockedColor;
                        sr.DOKill();
                        sr.DOColor(visitedColor, 0.5f).SetLoops(-1, LoopType.Yoyo);
                    }
                    
                    if (image != null)
                    {
                        image.color = lockedColor;
                        image.DOKill();
                        image.DOColor(visitedColor, 0.5f).SetLoops(-1, LoopType.Yoyo);
                    }
                    
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        public void OnPointerEnter(PointerEventData data)
        {
            if (sr != null)
            {
                sr.transform.DOKill();
                sr.transform.DOScale(initialScale * HoverScaleFactor, 0.3f);
            }
            else if (image != null)
            {
                image.transform.DOKill();
                image.transform.DOScale(initialScale * HoverScaleFactor, 0.3f);
            }
        }

        public void OnPointerExit(PointerEventData data)
        {
            if (sr != null)
            {
                sr.transform.DOKill();
                sr.transform.DOScale(initialScale, 0.3f);
            }
            else if (image != null)
            {
                image.transform.DOKill();
                image.transform.DOScale(initialScale, 0.3f);
            }
        }

        public void OnPointerDown(PointerEventData data)
        {
            mouseDownTime = Time.time;
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (Time.time - mouseDownTime < MaxClickDuration)
            {
                Tactics.UI.RoguelikeMapUIController.Instance.SelectNode(this);
            }
        }

        public void ShowSwirlAnimation()
        {
            if (visitedCircleImage == null)
                return;

            const float fillDuration = 0.3f;
            visitedCircleImage.fillAmount = 0;

            DOTween.To(() => visitedCircleImage.fillAmount, x => visitedCircleImage.fillAmount = x, 1f, fillDuration);
        }

        private void OnDestroy()
        {
            if (image != null)
            {
                image.transform.DOKill();
                image.DOKill();
            }

            if (sr != null)
            {
                sr.transform.DOKill();
                sr.DOKill();
            }
        }
    }
}