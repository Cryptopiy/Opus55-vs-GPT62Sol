using System.Collections.Generic;
using UnityEngine;

namespace WaveSurvival
{
    /// <summary>
    /// Remembers every runtime-generated asset so they can be destroyed when the application / Play Mode ends
    /// (this project disables domain reload, so nothing would clean them up otherwise).
    /// </summary>
    public static class AssetTracker
    {
        static readonly List<Object> tracked = new List<Object>(256);

        public static T Track<T>(T asset) where T : Object
        {
            asset.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            tracked.Add(asset);
            return asset;
        }

        public static void DestroyAll()
        {
            for (int i = 0; i < tracked.Count; i++)
                if (tracked[i] != null) Object.DestroyImmediate(tracked[i]);
            tracked.Clear();
        }
    }
}
