using System;
using System.Collections.Generic;
using System.Linq;

namespace Sokoban.Domain
{
    public sealed class GameSession
    {
        private sealed class HistoryEntry
        {
            public GameState State;
            public string Commands;
            public bool ReplayValid;
        }
        private readonly Stack<HistoryEntry> history = new Stack<HistoryEntry>();
        private readonly bool originalStart;
        private readonly GameState initialState;
        public RuleEngine Rules { get; }
        public GameState State { get; private set; }
        public string Commands { get; private set; } = "";
        public bool ReferenceReplayValid { get; private set; }
        public int UndoCount => history.Count;
        public GameSession(LevelDefinition definition) : this(definition, null) { }
        private GameSession(LevelDefinition definition, BoardSnapshot checkpoint)
        {
            Rules = new RuleEngine(definition, checkpoint); originalStart = checkpoint == null;
            initialState = Rules.CreateInitialState(); Restart();
        }
        public static GameSession FromCheckpoint(LevelDefinition definition, BoardSnapshot checkpoint)
        {
            if (checkpoint == null) throw new ArgumentNullException(nameof(checkpoint));
            return new GameSession(definition, checkpoint);
        }
        private GameSession(GameSession source)
        {
            Rules = source.Rules; originalStart = source.originalStart; initialState = source.initialState;
            State = source.State; Commands = source.Commands; ReferenceReplayValid = source.ReferenceReplayValid;
            foreach (var entry in source.history.Reverse()) history.Push(entry);
        }
        public GameSession Copy() => new GameSession(this);
        private void Record() => history.Push(new HistoryEntry { State = State, Commands = Commands, ReplayValid = ReferenceReplayValid });
        public MoveResolution Move(Direction direction)
        {
            MoveResolution result = Rules.Resolve(State, direction);
            if (result.Accepted)
            {
                Record();
                State = result.NextState;
                Commands += direction.ToString();
            }
            return result;
        }
        public bool Undo()
        {
            if (history.Count == 0) return false;
            var entry = history.Pop(); State = entry.State;
            Commands = entry.Commands; ReferenceReplayValid = entry.ReplayValid;
            return true;
        }
        public bool TryApplyDebugEdit(DebugBoardEdit edit, out string error)
        {
            error = null;
            if (edit == null) { error = "缺少 GM 操作。"; return false; }
            try
            {
                Cell player = State.Player;
                var crates = State.Crates.ToDictionary(c => c.Key, c => c.Value);
                foreach (var change in edit.Positions)
                    if (change.Key == DebugBoardEdit.PlayerId) player = change.Value;
                    else if (crates.ContainsKey(change.Key)) crates[change.Key] = change.Value;
                    else throw new ArgumentException("箱子不存在：" + change.Key);
                Direction facing = edit.Facing ?? State.Facing;
                var next = Rules.WithPositions(State, player, facing, crates);
                if (player == State.Player && facing == State.Facing && crates.All(c => State.Crates[c.Key] == c.Value))
                { error = "局面没有变化。"; return false; }
                Record(); State = next; ReferenceReplayValid = false; return true;
            }
            catch (ArgumentException exception) { error = exception.Message; return false; }
        }
        public void Restart() { history.Clear(); Commands = ""; State = initialState; ReferenceReplayValid = originalStart; }
    }
}
