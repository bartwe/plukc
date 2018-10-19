using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler
{
    public static class Require
    {
        public static void Assigned(object data)
        {
            if (data == null)
                throw new ArgumentException(Resource.RequireAssigned);
        }

        public static void Assigned(IntPtr value)
        {
            if (IntPtr.Zero == value)
                throw new ArgumentException(Resource.RequireAssigned);
        }

        public static void Unassigned(object data)
        {
            if (data != null)
                throw new ArgumentException(Resource.RequireUnassigned);
        }

        public static void NotEmpty(string data)
        {
            if (string.IsNullOrEmpty(data))
                throw new ArgumentException(Resource.RequireNotEmpty);
        }

        public static void True(bool truth)
        {
            if (!truth)
                throw new ArgumentException(Resource.RequireTrue);
        }

        public static void True(bool truth, string message)
        {
            if (!truth)
                throw new ArgumentException(Resource.RequireTrue + ": " + message);
        }

        public static void False(bool truth)
        {
            if (truth)
                throw new ArgumentException(Resource.RequireFalse);
        }

        public static void False(bool truth, string message)
        {
            if (truth)
                throw new ArgumentException(Resource.RequireFalse + ": " + message);
        }

        public static void Identifier(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentOutOfRangeException("name");
        }

        public static void Implementation(string message)
        {
            throw new NotImplementedException(message);
        }

        public static void NotCalled()
        {
            throw new NotSupportedException();
        }
    }
}
