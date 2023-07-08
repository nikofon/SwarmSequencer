using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SwarmSequencer
{
    namespace Serialization
    {
        public static class FrameDataSerializer
        {
            /// <summary>
            /// Saves data to file at given path
            /// </summary>
            /// <param name="path">File path</param>
            /// <param name="data">data to serialize</param>
            /// <returns>Save succesful</returns>
            public static bool SaveFrameData(string path, List<FrameData> data)
            {
                if (path.Length == 0) return false;
                SaveFrameData(path, SerializeFrameData(data));
                return true;
            }

            /// <summary>
            /// Saves already serialized data to file at given path
            /// </summary>
            /// <param name="path">File path</param>
            /// <param name="data">data to serialize</param>
            /// <returns>Save succesful</returns>
            public static bool SaveFrameData(string path, string data)
            {
                if (path.Length == 0) return false;
                File.WriteAllText(path, data);
                return true;
            }

            /// <summary>
            /// Serializes provided data
            /// </summary>
            /// <param name="data"></param>
            /// <returns>Serialized data</returns>
            public static string SerializeFrameData(List<FrameData> data)
            {
                return JsonConvert.SerializeObject(data);
            }

            /// <summary>
            /// Deserializes given string
            /// </summary>
            /// <param name="data"></param>
            /// <returns>Deserialized data</returns>
            public static List<FrameData> DeserializeFrameData(string data)
            {
                var turnDatas = JsonConvert.DeserializeObject<List<FrameData>>(data);
                return turnDatas;
            }
        }
    }
}
