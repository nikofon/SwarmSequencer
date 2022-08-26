using UnityEngine;

public static class MathHelper
{
    public static Vector3 BezierInterpolation(Vector3 pZero, Vector3 pTwo, Vector3 pOne, float t)
    {
        Vector3 pQZero = Vector3.Lerp(pZero, pOne, t);
        Vector3 pQOne = Vector3.Lerp(pOne, pTwo, t);
        return Vector3.Lerp(pQZero, pQOne, t);
    }

    public static Vector3[] BezierAproximation(Vector3 pZero, Vector3 pTwo, Vector3 pOne, float step = 0.02f)
    {
        int length = Mathf.CeilToInt(1 / step);
        Vector3[] result = new Vector3[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = BezierInterpolation(pZero, pTwo, pOne, Mathf.Min(1, i * step));
        }
        return result;
    }
}