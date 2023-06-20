using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEngine;

namespace ProjectileAnimator
{
    public static class Extensions
    {
        public static Dictionary<ProjectileKey, Tuple<SerializableVector3, SerializableVector3>> SortProjectileDictionary(this Dictionary<ProjectileKey, Tuple<SerializableVector3, SerializableVector3>> d)
        {
            var v = d.Keys;
            var l = v.ToList();
            l.Sort();
            Dictionary<ProjectileKey, Tuple<SerializableVector3, SerializableVector3>> res = new Dictionary<ProjectileKey, Tuple<SerializableVector3, SerializableVector3>>();
            foreach (var n in l) res.Add(n, d[n]);
            return res;
        }

        public static Dictionary<ProjectileKey, Transform> SortProjectileDictionary(this Dictionary<ProjectileKey, Transform> d)
        {
            var v = d.Keys;
            var l = v.ToList();
            l.Sort();
            Dictionary<ProjectileKey, Transform> res = new Dictionary<ProjectileKey, Transform>();
            foreach (var n in l)
                res.Add(n, d[n]);
            return res;
        }

        public static Vector3Field MakeDelayed(this Vector3Field v3)
        {

            var xInput = v3.Q<FloatField>("unity-x-input");
            xInput.isDelayed = true;

            var yInput = v3.Q<FloatField>("unity-y-input");
            yInput.isDelayed = true;

            var zInput = v3.Q<FloatField>("unity-z-input");
            zInput.isDelayed = true;

            return v3;
        }
    }
}

