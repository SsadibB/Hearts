using System;
using DG.Tweening;
using UnityEngine;

namespace Hearts.Presentation
{
    public class CardAnimationController : MonoBehaviour
    {
        [SerializeField] float moveDuration = 0.35f;

        int _locks;

        public bool InputLocked => _locks > 0;

        public void Move(RectTransform rect, Vector2 anchoredPosition, bool critical, Action onComplete, float delay = 0f)
        {
            if (rect == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (critical)
                _locks++;

            var finished = false;
            void Finish()
            {
                if (finished)
                    return;
                finished = true;
                rect.localScale = Vector3.one;
                if (critical)
                    _locks = Mathf.Max(0, _locks - 1);
                onComplete?.Invoke();
            }

            rect.DOKill(false);
            rect.localScale = Vector3.one;
            rect.DOAnchorPos(anchoredPosition, moveDuration)
                .SetDelay(delay)
                .SetEase(Ease.OutCubic)
                .OnComplete(Finish)
                .OnKill(Finish);
        }

        public void Flip(RectTransform rect, Action onEdge, Action onComplete, float delay = 0f)
        {
            if (rect == null)
            {
                onEdge?.Invoke();
                onComplete?.Invoke();
                return;
            }

            _locks++;
            rect.DOKill(false);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            var edged = false;
            var finished = false;
            void Edge()
            {
                if (edged)
                    return;
                edged = true;
                onEdge?.Invoke();
            }
            void Finish()
            {
                if (finished)
                    return;
                finished = true;
                Edge();
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one;
                _locks = Mathf.Max(0, _locks - 1);
                onComplete?.Invoke();
            }

            var sequence = DOTween.Sequence();
            sequence.SetDelay(delay);
            sequence.Append(rect.DOLocalRotate(new Vector3(0f, 90f, 0f), 0.12f).SetEase(Ease.InQuad));
            sequence.AppendCallback(Edge);
            sequence.Append(rect.DOLocalRotate(Vector3.zero, 0.12f).SetEase(Ease.OutQuad));
            sequence.OnComplete(Finish);
            sequence.OnKill(Finish);
        }

        public void Fade(CanvasGroup group, float alpha, bool activeWhenShown, Action onComplete)
        {
            if (group == null)
            {
                onComplete?.Invoke();
                return;
            }

            group.DOKill(false);
            if (alpha > 0.01f)
                group.gameObject.SetActive(true);

            _locks++;
            var finished = false;
            void Finish()
            {
                if (finished)
                    return;
                finished = true;
                group.alpha = alpha;
                group.interactable = alpha > 0.9f;
                group.blocksRaycasts = alpha > 0.9f;
                if (alpha <= 0.01f && !activeWhenShown)
                    group.gameObject.SetActive(false);
                _locks = Mathf.Max(0, _locks - 1);
                onComplete?.Invoke();
            }

            group.DOFade(alpha, 0.25f)
                .SetEase(Ease.OutQuad)
                .OnComplete(Finish)
                .OnKill(Finish);
        }

        void OnDisable()
        {
            _locks = 0;
        }
    }
}
