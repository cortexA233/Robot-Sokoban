using System.Collections.Generic;

namespace Sokoban.Domain
{
    public sealed class GameSession
    {
        private readonly Stack<GameState> history = new Stack<GameState>();
        public RuleEngine Rules { get; }
        public GameState State { get; private set; }
        public string Commands { get; private set; } = "";
        public int UndoCount => history.Count;
        public GameSession(LevelDefinition definition) { Rules = new RuleEngine(definition); Restart(); }
        public MoveResolution Move(Direction direction)
        {
            MoveResolution result = Rules.Resolve(State, direction);
            if (result.Accepted)
            {
                history.Push(State);
                State = result.NextState;
                Commands += direction.ToString();
            }
            return result;
        }
        public bool Undo()
        {
            if (history.Count == 0) return false;
            State = history.Pop();
            Commands = Commands.Substring(0, Commands.Length - 1);
            return true;
        }
        public void Restart() { history.Clear(); Commands = ""; State = Rules.CreateInitialState(); }
    }
}
