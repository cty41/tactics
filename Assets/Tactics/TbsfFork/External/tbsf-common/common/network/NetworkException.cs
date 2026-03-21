using System;

namespace Tactics.Tbsf.Common.Network
{
    public class NetworkException : Exception
    {
        public NetworkException(string message) : base(message)
        {
        }
    }
}