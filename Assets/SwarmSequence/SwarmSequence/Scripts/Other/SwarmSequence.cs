using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SwarmSequencer.Serialization;

namespace SwarmSequencer
{
    public class SwarmSequence : ScriptableObject
    {
        internal List<FrameData> Frames { get { if (m_Frames == null) ForceDeserialize(); return m_Frames; } }
        [SerializeField]
        private List<FrameData> m_Frames;
        public string sequenceName;
        [HideInInspector]
        public string rawData;

        void ForceDeserialize()
        {
            m_Frames = FrameDataSerializer.DeserializeFrameData(rawData);
        }

    }
}
