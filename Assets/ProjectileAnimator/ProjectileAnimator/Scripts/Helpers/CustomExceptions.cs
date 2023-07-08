using System;
using UnityEngine;

namespace SwarmSequencer
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

    public class NamingViolationException : Exception
    {
        public NamingViolationException()
        {
            Debug.LogError("Can't parse provided name!");
        }

        public NamingViolationException(string message)
            : base(message)
        {
        }

        public NamingViolationException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    public class SelfReferencingLoopException : Exception
    {
        public SelfReferencingLoopException()
        {
            Debug.LogError("This texture points to itself!");
        }

        public SelfReferencingLoopException(string message)
            : base(message)
        {
        }

        public SelfReferencingLoopException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    public class InstanceConflictException : Exception
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
