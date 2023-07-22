using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.AssetImporters;
using System.IO;

namespace SwarmSequencer
{
    namespace Serialization
    {
        [ScriptedImporter(1, "ss")]
        public class SSImporter : ScriptedImporter
        {
            SwarmSequence asset;
            public override void OnImportAsset(AssetImportContext ctx)
            {
                var data = File.ReadAllText(ctx.assetPath);
                asset = ScriptableObject.CreateInstance<SwarmSequence>();
                asset.sequenceName = ctx.assetPath;
                asset.rawData = data;
                ctx.AddObjectToAsset("Main", asset);
                ctx.SetMainObject(asset);
            }

        }
    }

}
