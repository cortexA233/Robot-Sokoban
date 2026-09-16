using System;
using DG.Tweening;
using Sokoban.Domain;
using UnityEngine;

namespace Sokoban
{
    public sealed class CommandPresenter : MonoBehaviour
    {
        private Tween command;
        private BoardView board;
        private int generation;
        public bool Busy => command != null && command.IsActive();
        public void Initialize(BoardView view) => board = view;

        public void Present(MoveResolution result, Action completed)
        {
            Cancel();
            int ticket = generation;
            var first = result.Microsteps[0];
            bool pushing = first.CrateId != null;
            float duration = pushing ? Mathf.Max(.6f, .5f + .12f * (result.Microsteps.Count - 1)) : .25f;
            Quaternion fromRotation = board.Robot.transform.rotation;
            Quaternion toRotation = Quaternion.Euler(0, (int)result.NextState.Facing * 90, 0);
            float clock = 0;
            int appliedStep = -1;
            command = DOTween.To(() => clock, t =>
            {
                if (ticket != generation) return;
                clock = t;
                board.Robot.transform.rotation = Quaternion.Slerp(fromRotation, toRotation, Mathf.Clamp01(t / .08f));
                float actionTime = Mathf.Max(0, t - .08f);
                float u = Mathf.SmoothStep(0, 1, Mathf.Clamp01(pushing ? (actionTime - .1f) / .4f : actionTime / .25f));
                board.Robot.transform.position = Vector3.Lerp(BoardView.Position(first.PlayerFrom), BoardView.Position(first.PlayerTo), u);
                board.Robot.Sample(pushing ? 2 : 1, actionTime);
                if (pushing)
                {
                    Vector3 position = Vector3.Lerp(BoardView.Position(first.CrateFrom), BoardView.Position(first.CrateTo), u);
                    if (actionTime > .5f && result.Microsteps.Count > 1)
                    {
                        int step = Mathf.Min(1 + Mathf.FloorToInt((actionTime - .5f) / .12f), result.Microsteps.Count - 1);
                        float slide = Mathf.Clamp01((actionTime - .5f - .12f * (step - 1)) / .12f);
                        position = Vector3.Lerp(BoardView.Position(result.Microsteps[step].CrateFrom), BoardView.Position(result.Microsteps[step].CrateTo), slide);
                    }
                    board.Crates[first.CrateId].position = position;
                }
                for (int i = appliedStep + 1; i < result.Microsteps.Count; i++)
                {
                    float arrival = pushing ? .5f + .12f * i : .25f;
                    if (actionTime + .0001f < arrival) break;
                    board.ApplyPower(result.Microsteps[i].Power); appliedStep = i;
                }
            }, duration + .08f, duration + .08f).SetEase(Ease.Linear).SetTarget(this).SetLink(gameObject)
                .OnComplete(() => { if (ticket != generation) return; command = null; board.Robot.Sample(0, 0); completed?.Invoke(); });
        }
        public void SetPaused(bool paused) { if (paused) command?.Pause(); else command?.Play(); }
        public void Cancel() { generation++; command?.Kill(false); command = null; }
        private void OnDestroy() => Cancel();
    }
}
