using System;
using UnityEngine;

namespace UI
{
    public readonly struct UIAssetLoadResult
    {
        public readonly GameObject Prefab;
        public readonly IDisposable Lease;

        public bool IsValid => Prefab != null;

        public UIAssetLoadResult(GameObject prefab, IDisposable lease)
        {
            Prefab = prefab;
            Lease = lease;
        }
    }
}
