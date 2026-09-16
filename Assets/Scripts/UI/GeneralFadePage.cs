using System;
using DG.Tweening;
using KToolkit;
using UnityEngine;

namespace Sokoban.UI
{
    public enum FadeAxis { Horizontal, Vertical }

    // Adapted from Element_Ballance's GeneralFadePage: cover -> swap -> reveal.
    // Normalized anchors replace its fixed pixel travel distances.
    [KUI_Info("screens/GeneralFade", nameof(GeneralFadePage))]
    public sealed class GeneralFadePage : KUIPage
    {
        public const float Duration = .6f;
        public bool IsFading { get; private set; }
        public float Coverage { get; private set; }
        private Sequence sequence;
        private RectTransform first, second;
        private FadeAxis axis;

        public override void OnStart()
        {
            first = (RectTransform)transform.Find("First");
            second = (RectTransform)transform.Find("Second");
            gameObject.SetActive(false);
        }
        public void Play(FadeAxis direction, Action covered, Action finished)
        {
            Cancel();
            axis = direction;
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            IsFading = true; SetCoverage(0);
            sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Append(DOTween.To(() => Coverage, SetCoverage, 1, Duration).SetEase(Ease.InOutQuad));
            sequence.AppendCallback(() => covered?.Invoke());
            // Keep the new cameras and deferred destruction hidden for at least one rendered frame.
            sequence.AppendInterval(.08f);
            sequence.Append(DOTween.To(() => Coverage, SetCoverage, 0, Duration).SetEase(Ease.InOutQuad));
            sequence.OnComplete(() =>
            {
                sequence = null; IsFading = false; gameObject.SetActive(false);
                finished?.Invoke();
            });
        }
        private void SetCoverage(float value)
        {
            Coverage = value;
            float p = value * .5f;
            if (axis == FadeAxis.Horizontal)
            {
                first.anchorMin = new Vector2(-.5f + p, 0); first.anchorMax = new Vector2(p, 1);
                second.anchorMin = new Vector2(1 - p, 0); second.anchorMax = new Vector2(1.5f - p, 1);
            }
            else
            {
                first.anchorMin = new Vector2(0, -.5f + p); first.anchorMax = new Vector2(1, p);
                second.anchorMin = new Vector2(0, 1 - p); second.anchorMax = new Vector2(1, 1.5f - p);
            }
            first.offsetMin = second.offsetMin = Vector2.one * -2;
            first.offsetMax = second.offsetMax = Vector2.one * 2;
        }
        public void Cancel()
        {
            sequence?.Kill(false); sequence = null; IsFading = false; Coverage = 0;
            if (gameObject) gameObject.SetActive(false);
        }
        public override void OnDestroy() => Cancel();
    }
}
