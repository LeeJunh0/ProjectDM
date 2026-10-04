using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectDM
{
    /// <summary>Owns the current transient notice and its unscaled expiration time.</summary>
    internal sealed class GameNotifications
    {
        internal GameNotifications() { }

        internal float noticeUntil;

        internal string prototypeNotice;

        internal void Notice(string message)
        { prototypeNotice = message; noticeUntil = Time.unscaledTime + 3f; }
    }
}
