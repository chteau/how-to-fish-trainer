using UnityEngine;

namespace HtfTrainer
{
    internal static class Log
    {
        private const string Prefix = "[HtF-Trainer] ";

        internal static void Info(string msg) => Debug.Log(Prefix + msg);
        internal static void Warn(string msg) => Debug.LogWarning(Prefix + msg);
        internal static void Error(string msg) => Debug.LogError(Prefix + msg);
    }
}
