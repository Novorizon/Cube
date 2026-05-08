using System.Collections.Generic;
using System.Threading.Tasks;

namespace UI
{
    public sealed class PageNavigator
    {
        readonly UIInstanceFactory factory;
        readonly Stack<UIHandle> stack = new Stack<UIHandle>();

        bool isNavigating;

        public PageNavigator(UIInstanceFactory factory)
        {
            this.factory = factory;
        }

        public int Count => stack.Count;

        public UIHandle? Peek()
        {
            if (stack.Count == 0)
            {
                return null;
            }

            return stack.Peek();
        }

        public async Task<UIHandle> PushAsync(string prefabPath, object args = null)
        {
            if (isNavigating)
            {
                return default;
            }

            isNavigating = true;
            try
            {
                UIHandle? top = Peek();
                if (top.HasValue && top.Value.View != null)
                {
                    top.Value.View.gameObject.SetActive(false);
                }

                UIHandle handle = await factory.OpenAsync(UIKind.Page, UILayer.Page, prefabPath, args, false, true, null);
                if (handle.IsValid)
                {
                    stack.Push(handle);
                }

                return handle;
            }
            finally
            {
                isNavigating = false;
            }
        }

        public async Task<UIHandle> ReplaceAsync(string prefabPath, object args = null)
        {
            if (isNavigating)
            {
                return default;
            }

            isNavigating = true;
            try
            {
                UIHandle? top = PopInternal();
                if (top.HasValue)
                {
                    factory.Close(top.Value, false, true);
                }

                UIHandle handle = await factory.OpenAsync(UIKind.Page, UILayer.Page, prefabPath, args, false, true, null);
                if (handle.IsValid)
                {
                    stack.Push(handle);
                }

                return handle;
            }
            finally
            {
                isNavigating = false;
            }
        }

        public bool Pop()
        {
            UIHandle? top = PopInternal();
            if (!top.HasValue)
            {
                return false;
            }

            factory.Close(top.Value, false, true);

            UIHandle? next = Peek();
            if (next.HasValue && next.Value.View != null)
            {
                next.Value.View.gameObject.SetActive(true);
                next.Value.View.InternalOnOpen(null);
            }

            return true;
        }

        public async Task<UIHandle> ResetToAsync(string prefabPath, object args = null)
        {
            while (stack.Count > 0)
            {
                UIHandle h = stack.Pop();
                factory.Close(h, false, true);
            }

            UIHandle root = await factory.OpenAsync(UIKind.Page, UILayer.Page, prefabPath, args, false, true, null);
            if (root.IsValid)
            {
                stack.Push(root);
            }

            return root;
        }

        public void Clear(bool destroy = false)
        {
            while (stack.Count > 0)
            {
                UIHandle h = stack.Pop();
                factory.Close(h, destroy, !destroy);
            }
        }

        UIHandle? PopInternal()
        {
            if (stack.Count == 0)
            {
                return null;
            }

            return stack.Pop();
        }
    }
}
