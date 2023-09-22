using System;
using SwarmSequencer.Serialization;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SwarmSequencer
{

    /// <summary>
    /// This class represents compete info about positions of all projectiles in one frame, including their bezier interpolation point
    /// </summary>
    public class FrameData : IComparable<FrameData>
    {
        /// <summary>
        /// key - prefab id, unique projectile id, value - Tuple, first - relative position, second - Bezier Interpolation Point
        /// </summary>       
        [Newtonsoft.Json.JsonIgnore]
        public Dictionary<ProjectileKey, Tuple<SerializableVector3, SerializableVector3>> ProjectilePositionData;

        public List<KeyValuePair<ProjectileKey, Tuple<SerializableVector3, SerializableVector3>>> SerializeProjectilePositionData
        {
            get { return ProjectilePositionData == null ? null : ProjectilePositionData.ToList(); }
            set { ProjectilePositionData = value.ToDictionary(x => x.Key, x => x.Value); }
        }

        public int Order;

        public FrameData(Dictionary<ProjectileKey, Tuple<SerializableVector3, SerializableVector3>> projectilePositionData, int order)
        {
            ProjectilePositionData = projectilePositionData.SortProjectileDictionary();
            Order = order;
        }

        [Newtonsoft.Json.JsonConstructor]
        public FrameData() { }
        public FrameData(int order, List<KeyValuePair<ProjectileKey, Tuple<SerializableVector3, SerializableVector3>>> projectilePositionData)
        {
            Debug.Log(projectilePositionData == null);
            SerializeProjectilePositionData = projectilePositionData;
            Order = order;
        }

        public int CompareTo(FrameData other)
        {
            return Order.CompareTo(other.Order);
        }
    }

    [System.Serializable]
    public struct ProjectileKey : IComparable<ProjectileKey>
    {
        [Min(0)]
        public int GroupIndex;
        [Min(0)]
        public int InstanceIndex;

        public ProjectileKey(int projectilePrefabId, int prokectileInternalId)
        {
            GroupIndex = projectilePrefabId;
            InstanceIndex = prokectileInternalId;
        }

        public override bool Equals(object obj)
        {
            if ((obj == null) || !this.GetType().Equals(obj.GetType())) return false;
            var o = (ProjectileKey)obj;
            return GroupIndex == o.GroupIndex && InstanceIndex == o.InstanceIndex;
        }

        public int CompareTo(ProjectileKey other)
        {
            if (other.GroupIndex > GroupIndex) return -1;
            if (other.GroupIndex == GroupIndex)
            {
                if (other.InstanceIndex > InstanceIndex) return -1;
                else if (other.InstanceIndex == InstanceIndex) return 0;
            }
            return 1;
        }

        public override string ToString()
        {
            return $"GroupIndex: {GroupIndex} InstanceIndex: {InstanceIndex}";
        }
    }

    [System.Serializable]
    public struct SerializableVector3
    {
        private Vector3 BezierInterpolation(Vector3 pZero, Vector3 pTwo, Vector3 pOne, float t)
        {
            Vector3 pQZero = Vector3.Lerp(pZero, pOne, t);
            Vector3 pQOne = Vector3.Lerp(pOne, pTwo, t);
            return Vector3.Lerp(pQZero, pQOne, t);
        }
        public float x;
        public float y;
        public float z;

        public SerializableVector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static SerializableVector3 operator -(SerializableVector3 a, SerializableVector3 b) => new SerializableVector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static SerializableVector3 operator -(SerializableVector3 a) => new SerializableVector3(-a.x, -a.y, -a.z);
        public static SerializableVector3 operator +(SerializableVector3 a, SerializableVector3 b) => new SerializableVector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static SerializableVector3 operator /(SerializableVector3 a, float b) => new SerializableVector3(a.x / b, a.y / b, a.z / b);
        public static bool operator ==(SerializableVector3 a, SerializableVector3 b) => a.x == b.x && a.y == b.y && a.z == b.z;
        public static bool operator !=(SerializableVector3 a, SerializableVector3 b) => !(a.x == b.x && a.y == b.y && a.z == b.z);

        public Vector3 ScaleToVector3(float scale)
        {
            return scale * new Vector3(x, y, z);
        }

        public readonly static SerializableVector3 Infinity = new SerializableVector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);

        public static implicit operator Vector3(SerializableVector3 a) => new Vector3(a.x, a.y, a.z);
        public static implicit operator SerializableVector3(Vector3 a) => new SerializableVector3(a.x, a.y, a.z);

    }

    [Serializable]
    public class InstanceLookUp
    {
        public GameObject prefab;
        [Min(0)]
        public int groupIndex = 0;
    }

    [System.Serializable]
    public class FrameTimeOverride
    {
        public int FrameOne;
        public int FrameTwo;
        public float value;

        public override bool Equals(object obj)
        {
            if ((obj == null) || !this.GetType().Equals(obj.GetType())) return false;
            var o = (FrameTimeOverride)obj;
            return (FrameOne == o.FrameOne && FrameTwo == o.FrameTwo) || (FrameOne == o.FrameTwo && FrameTwo == o.FrameOne);
        }
    }

}
