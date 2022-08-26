using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectileAnimator
{
    /// <summary>
    /// This class represents compete info about positions of all projectiles in one frame, including their bezier interpolation point
    /// </summary>
    public class FrameData: IComparable<FrameData>
    {
        /// <summary>
        /// key - prefab id, unique projectile id, value - Tuple, first - relative position, second - Bezier Interpolation Point
        /// </summary>       
        [Newtonsoft.Json.JsonIgnore]
        public Dictionary<ProjectileKey, Tuple<SerializableVector3, SerializableVector3>> ProjectilePositionData;

        public List<KeyValuePair<ProjectileKey, Tuple<SerializableVector3, SerializableVector3>>> SerializeProjectilePositionData
        {
            get { return ProjectilePositionData==null? null: ProjectilePositionData.ToList(); }
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
    public struct ProjectileKey: IComparable<ProjectileKey>
    {
        [Range(1, 255)]
        public int ProjectilePrefabId;
        [Range(0, 255)]
        public int ProjectileInternalId;

        public ProjectileKey(int projectilePrefabId, int prokectileInternalId)
        {
            ProjectilePrefabId = projectilePrefabId;
            ProjectileInternalId = prokectileInternalId;
        }

        public override bool Equals(object obj)
        {
            if ((obj == null) || !this.GetType().Equals(obj.GetType())) return false;
            var o = (ProjectileKey)obj;
            return ProjectilePrefabId == o.ProjectilePrefabId && ProjectileInternalId == o.ProjectileInternalId;
        }

        public int CompareTo(ProjectileKey other)
        {
            if (other.ProjectilePrefabId > ProjectilePrefabId) return -1;
            if(other.ProjectilePrefabId == ProjectilePrefabId)
            {
                if (other.ProjectileInternalId > ProjectileInternalId) return -1;
                else if (other.ProjectileInternalId == ProjectileInternalId) return 0;
            }
            return 1;
        }

        public override string ToString()
        {
            return $"ProjectilePrefabId: {ProjectilePrefabId} ProjectileInternalId: {ProjectileInternalId}";
        }
    }

    [System.Serializable]
    public struct SerializableVector3 {private Vector3 BezierInterpolation(Vector3 pZero, Vector3 pTwo, Vector3 pOne, float t){
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

        public Vector3 ScaleToVector3(float scale)
        {
            SerializableVector3 a = Infinity;
            return new Vector3(scale*x, scale * y, scale * z);
        }

        public readonly static SerializableVector3 Infinity = new SerializableVector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);

        public static implicit operator Vector3(SerializableVector3 a) => new Vector3(a.x, a.y, a.z); 
        public static implicit operator SerializableVector3(Vector3 a) => new SerializableVector3(a.x, a.y, a.z); 
    
    }

    [Serializable]
    public class ProjectileLookUp
    {
        public GameObject prefab;
        [Range(1, 255)]
        public int id = 1;
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
            var o = (FrameTimeOverride) obj;
            return (FrameOne == o.FrameOne && FrameTwo == o.FrameTwo) || (FrameOne == o.FrameTwo && FrameTwo == o.FrameOne);
        }
    }
}
