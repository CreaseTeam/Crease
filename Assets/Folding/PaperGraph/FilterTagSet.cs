using System.Collections.Generic;
using UnityEngine;

namespace Crease.Folding.PaperGraph
{
    [System.Serializable]
    public class FilterTagSet
    {
        [SerializeField] private List<string> _tags = new List<string>();

        public IReadOnlyList<string> Tags {
            get {
                if (_tags == null) _tags = new List<string>();
                return _tags;
            }
        }

        public List<string> GetMutableTags() {
            if (_tags == null) _tags = new List<string>();
            return _tags;
        }

        public void SetTags(IEnumerable<string> tags) {
            _tags = tags == null ? new List<string>() : new List<string>(tags);
        }

        public void Clear() {
            if (_tags == null) _tags = new List<string>();
            else _tags.Clear();
        }
    }
}
