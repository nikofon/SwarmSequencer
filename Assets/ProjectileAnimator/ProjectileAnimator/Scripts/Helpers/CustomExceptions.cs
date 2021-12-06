using System;
using UnityEngine;

namespace ProjectileAnimator
{
    public class NoPrefabWithGivenIdFoundException : Exception
    {
        public NoPrefabWithGivenIdFoundException()
        {
            Debug.LogError("No prefab with given id found");
        }

        public NoPrefabWithGivenIdFoundException(string message)
            : base(message)
        {
        }

        public NoPrefabWithGivenIdFoundException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    public class InstanceConflictException: Exception
    {
        public InstanceConflictException()
        {
            Debug.LogError("Found two pixels with identical r and g values in a same frame!");
        }

        public InstanceConflictException(string message)
            : base(message)
        {
        }

        public InstanceConflictException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
