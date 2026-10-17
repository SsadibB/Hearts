using System;
using DG.Tweening;
using UnityEngine;

namespace Hearts.Presentation
{
    public class CardAnimationController : MonoBehaviour
    {
        [SerializeField] float moveDuration = 0.85f;

        int _locks;

        public bool InputLocked => _locks > 0;

        public void Move(RectTransform rect, Vector2 anchoredPosition, bool critical, Action onComplete, float delay = 0f, float zDegrees = 0f, float duration = -1f)
        {
            if (rect == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (critical)
                _locks++;

            float time = duration > 0f ? duration : moveDuration;
            var finished = false;
            void Finish()
            {
                if (finished)
                    return;
                finished = true;
                rect.localRotation = Quaternion.Euler(0f, 0f, zDegrees);
                if (critical)
                    _locks = Mathf.Max(0, _locks - 1);
                onComplete?.Invoke();
            }

            rect.DOKill(false);
            rect.DOAnchorPos(anchoredPosition, time)
                .SetDelay(delay)
                .SetEase(Ease.InOutCubic)
                .OnComplete(Finish)
                .OnKill(Finish);
            rect.DOLocalRotate(new Vector3(0f, 0f, zDegrees), time, RotateMode.Fast)
                .SetDelay(delay)
                .SetEase(Ease.InOutCubic);
        }

        public void MoveY(RectTransform rect, float y, Action onComplete = null)
        {
            if (rect == null)
            {
                onComplete?.Invoke();
                return;
            }

            rect.DOKill(false);
            rect.DOAnchorPosY(y, 0.22f)
                .SetEase(Ease.OutCubic)
                .OnComplete(() => onComplete?.Invoke());
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
            float facing = rect.localEulerAngles.z;
            rect.localRotation = Quaternion.Euler(0f, 0f, facing);
            rect.localScale = Vector3.one;
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
                rect.localRotation = Quaternion.Euler(0f, 0f, facing);
                rect.localScale = Vector3.one;
                _locks = Mathf.Max(0, _locks - 1);
                onComplete?.Invoke();
            }

            var sequence = DOTween.Sequence();
            sequence.SetDelay(delay);
            sequence.Append(rect.DOScaleX(0f, 0.12f).SetEase(Ease.InQuad));
            sequence.AppendCallback(Edge);
            sequence.Append(rect.DOScaleX(1f, 0.12f).SetEase(Ease.OutQuad));
            sequence.OnComplete(Finish);
            sequence.OnKill(Finish);
        }

        public void ResetCard(RectTransform rect)
        {
            if (rect == null)
                return;
            rect.DOKill(false);
            rect.localScale = Vector3.one;
            var group = rect.GetComponent<CanvasGroup>();
            if (group == null)
                return;
            group.DOKill(false);
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
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

        public void Collect(RectTransform rect, Vector2 anchoredPosition, Action onComplete, float delay = 0f)
        {
            if (rect == null)
            {
                onComplete?.Invoke();
                return;
            }

            _locks++;
            rect.DOKill(false);
            var group = rect.GetComponent<CanvasGroup>();
            if (group == null)
                group = rect.gameObject.AddComponent<CanvasGroup>();
            group.DOKill(false);
            group.alpha = 1f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var finished = false;
            void Finish()
            {
                if (finished)
                    return;
                finished = true;
                _locks = Mathf.Max(0, _locks - 1);
                onComplete?.Invoke();
            }

            var sequence = DOTween.Sequence();
            sequence.SetDelay(delay);
            sequence.Append(rect.DOAnchorPos(anchoredPosition, 0.75f).SetEase(Ease.InOutSine));
            sequence.Join(rect.DOScale(rect.localScale * 0.88f, 0.75f).SetEase(Ease.InCubic));
            sequence.Join(group.DOFade(0f, 0.75f).SetEase(Ease.InQuad));
            sequence.OnComplete(Finish);
            sequence.OnKill(Finish);
        }

        void OnDisable()
        {
            _locks = 0;
        }
    }
}
