using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ProjectileAnimator
{
    public class SequenceCreator : EditorWindow
    {

        Vector3 zero;
        Vector2Int gridSize;

        List<ProjectileKey> ProjectileKeys = new List<ProjectileKey>();
        List<FrameData> GeneratedFrameDatas = new List<FrameData>();
        int currentFrame;

        public enum Orientation
        {
            XY, XZ
        }

        public Orientation orientation;

        [MenuItem("/Window/ProjectileAnimator/SequenceCreator")]
        private static void ShowWindow()
        {
            var window = GetWindow<SequenceCreator>();
            window.titleContent = new GUIContent("SequenceCreator");
            window.minSize = new Vector2(678, 450);
            window.Show();
        }

        private void OnEnable()
        {
            VisualTreeAsset origin = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/ProjectileAnimator/ProjectileAnimator/UIDocuments/UXML/SequenceCreatorUXML.uxml");
            TemplateContainer container = origin.CloneTree();
            rootVisualElement.Add(container);
        }

        void AddProjectileInfoToFrame(int frameCount, ProjectileKey key, SerializableVector3 position, SerializableVector3 bezierControl)
        {
            if (frameCount >= GeneratedFrameDatas.Count)
            {
                Debug.LogWarning($"A frame you trying to add info to: {frameCount} doesn't exist");
                return;
            }
            if (GeneratedFrameDatas[frameCount] == null) { Debug.LogWarning($"A frame you trying to add info to: {frameCount} is null, you should initialize it first"); return; }
            if (GeneratedFrameDatas[frameCount].ProjectilePositionData.ContainsKey(key))
                GeneratedFrameDatas[frameCount].ProjectilePositionData[key] = new Tuple<SerializableVector3, SerializableVector3>(position, bezierControl);
            else
                GeneratedFrameDatas[frameCount].ProjectilePositionData.Add(key, new Tuple<SerializableVector3, SerializableVector3>(position, bezierControl));


        }

        void CreateNewFrame(int frameCount, Dictionary<ProjectileKey, Tuple<SerializableVector3, SerializableVector3>> projectilePositionData)
        {
            if (frameCount >= GeneratedFrameDatas.Count)
            {
                frameCount = GeneratedFrameDatas.Count;
                GeneratedFrameDatas.Add(new FrameData(projectilePositionData, frameCount));
            }
            else
            {
                List<FrameData> newFrameDatas = new List<FrameData>(GeneratedFrameDatas.Count + 1);
                for (int i = 0; i < frameCount; i++)
                {
                    newFrameDatas[i] = GeneratedFrameDatas[i];
                }
                newFrameDatas[frameCount].ProjectilePositionData = projectilePositionData;
                for (int i = frameCount + 2; i <= GeneratedFrameDatas.Count; i++)
                {
                    newFrameDatas[i] = GeneratedFrameDatas[i - 1];
                    newFrameDatas[i].Order = i;
                }
                GeneratedFrameDatas = newFrameDatas;
            }
        }

        void DeleteFrame(int frameCount)
        {
            if (frameCount >= GeneratedFrameDatas.Count)
            {
                Debug.LogWarning($"You are trying to delete an unexisting frame: {frameCount}");
                return;
            }
            GeneratedFrameDatas.RemoveAt(frameCount);
            for (int i = frameCount; i < GeneratedFrameDatas.Count; i++)
            {
                GeneratedFrameDatas[i].Order = i;
            }
        }

        void DrawGrid(Vector2Int gridSize, float cellSize, float yLevel, Vector3 zero)
        {
            Vector3 actualZero = orientation == Orientation.XY ? new Vector3(zero.x, zero.y, zero.z + yLevel * cellSize) : new Vector3(zero.x, zero.y + yLevel * cellSize, zero.z);
            Vector3Int actualGridSize = orientation == Orientation.XY ? (Vector3Int)gridSize : new Vector3Int(gridSize.x, 0, gridSize.y);
            GizmoHelper.DrawGrid3D(actualGridSize, cellSize, actualZero);
        }

        private void OnGUI()
        {

        }
    }
}