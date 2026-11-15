using System.Collections.Generic;

namespace GunCustomizationTool.SciFi_Weapons
{
    public class HistoryManager
    {
        private const int MaxHistorySteps = 10;

        public class HistoryState
        {
            public GunEntry Gun;
            public Dictionary<AttachmentType, AttachmentEntry> ActiveAttachments;
            public Dictionary<AttachmentType, int> VariantIndices;
            public Dictionary<AttachmentType, bool> AttachmentEnabled;
            public Dictionary<AttachmentType, bool> RayEnabled;
            public int SkinIndex;
        }

        private readonly List<HistoryState> _history = new();
        private int _historyIndex = -1;

        public bool CanUndo => _historyIndex >= 0;

        public void PushState(HistoryState state)
        {
            if (_historyIndex < _history.Count - 1)
            {
                _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
            }

            _history.Add(state);
            _historyIndex = _history.Count - 1;

            if (_history.Count > MaxHistorySteps)
            {
                _history.RemoveAt(0);
                _historyIndex--;
            }
        }

        public HistoryState Undo()
        {
            if (!CanUndo)
            {
                return null;
            }

            var state = _history[_historyIndex];
            _historyIndex--;
            return state;
        }
    }
}
