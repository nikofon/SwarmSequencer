using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


public class TestScript : MonoBehaviour
{
    public Vector3 startPoint = new Vector3(-0.0f, 0.0f, 0.0f);
    public Vector3 endPoint = new Vector3(-2.0f, 2.0f, 0.0f);
    public Vector3 tangent = new Vector3(-1.0f, 2.0f, 0.0f);

    public bool enableStandartBezier;
    public bool enableCustomBezier;
}
