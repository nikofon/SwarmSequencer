using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using SwarmSequencer.MathTools;

[CustomEditor(typeof(TestScript))]
public class TestScriptEditor : Editor
{
    private void OnSceneViewGUI(SceneView sv)
    {
        TestScript be = target as TestScript;

        be.startPoint = Handles.PositionHandle(be.startPoint, Quaternion.identity);
        be.endPoint = Handles.PositionHandle(be.endPoint, Quaternion.identity);
        be.tangent = Handles.PositionHandle(be.tangent, Quaternion.identity);
        if (be.enableStandartBezier)
        {
            Handles.DrawBezier(be.startPoint, be.endPoint, be.tangent, be.tangent, Color.red, null, 2f);
        }
        if (be.enableCustomBezier)
        {
            DrawBezier(be.startPoint, be.endPoint, be.tangent);
        }
    }

    void DrawBezier(Vector3 start, Vector3 end, Vector3 determiner)
    {
        Vector3[] points = MathHelper.BezierAproximation(start, end, determiner);
        for (int i = 0; i < points.Length - 1; i++)
        {
            Handles.DrawLine(points[i], points[i + 1], 2f);
        }
    }

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneViewGUI;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneViewGUI;
    }
}
