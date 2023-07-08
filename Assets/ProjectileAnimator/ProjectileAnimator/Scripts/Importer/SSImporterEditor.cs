using System.Collections;
using UnityEditor.AssetImporters;
using UnityEditor;
using UnityEngine;
namespace SwarmSequencer
{
    namespace Serialization
    {
        [CustomEditor(typeof(SSImporter))]
        public class SSImporterEditor : ScriptedImporterEditor
        {
            SSImporter targetImporter;
            public override void OnEnable()
            {
                base.OnEnable();
                targetImporter = (SSImporter)target;
            }

        }
    }
}
