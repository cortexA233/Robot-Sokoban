using System;
using System.Linq;
using Sokoban.Domain;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Sokoban
{
    public sealed class RobotPresenter : MonoBehaviour
    {
        public Animator animator;
        public AnimationClip idle;
        public AnimationClip move;
        public AnimationClip push;
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private AnimationClipPlayable[] clips;
        public Transform Contact => animator.transform.Find("RobotRoot/PushSlide/PushContact");
        public Transform EmotionAnchor => animator.transform.Find("RobotRoot/EmotionAnchor");

        public void Initialize()
        {
            if (graph.IsValid()) return;
            if (!animator || !idle || !move || !push) throw new InvalidOperationException("机器人 Animator 或 Idle/Move/Push 引用缺失。");
            animator.applyRootMotion = false;
            animator.runtimeAnimatorController = null;
            graph = PlayableGraph.Create("Sokoban robot sampling");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, 3);
            clips = new[] { idle, move, push }.Select(c => AnimationClipPlayable.Create(graph, c)).ToArray();
            for (int i = 0; i < clips.Length; i++) { graph.Connect(clips[i], 0, mixer, i); clips[i].SetSpeed(0); }
            AnimationPlayableOutput.Create(graph, "Robot", animator).SetSourcePlayable(mixer);
            graph.Play(); Sample(0, 0);
        }

        public void Sample(int clip, float seconds)
        {
            for (int i = 0; i < clips.Length; i++) mixer.SetInputWeight(i, i == clip ? 1 : 0);
            clips[clip].SetTime(clip == 2 ? Mathf.Min(seconds, push.length) : seconds);
            graph.Evaluate(0);
        }
        public void Restore(Cell cell, Direction facing)
        {
            transform.position = BoardView.Position(cell);
            transform.rotation = Quaternion.Euler(0, (int)facing * 90, 0);
            Sample(0, 0);
        }
        private void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }
    }
}
