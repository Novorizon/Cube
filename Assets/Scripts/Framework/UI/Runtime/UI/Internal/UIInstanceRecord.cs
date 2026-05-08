using System;

namespace UI
{
    internal sealed class UIInstanceRecord
    {
        public UIHandle Handle;
        public bool CacheOnClose;
        public bool IsCached;
        public IDisposable AssetLease;

        public UIInstanceRecord(UIHandle handle, bool cacheOnClose, IDisposable assetLease)
        {
            Handle = handle;
            CacheOnClose = cacheOnClose;
            AssetLease = assetLease;
        }

        public void DisposeAssetLease()
        {
            AssetLease?.Dispose();
            AssetLease = null;
        }
    }
}
