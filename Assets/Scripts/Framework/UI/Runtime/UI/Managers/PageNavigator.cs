using System.Threading.Tasks;

namespace UI
{
    public sealed class PageNavigator
    {
        readonly UIInstanceFactory factory;
        readonly System.Collections.Generic.Stack<UIHandle> stack = new();

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

        public async Task<UIHandle> PushAsync(string prefabPath, object? args = null)
        {
            UIHandle? top = Peek();
            if (top.HasValue && top.Value.View != null)
            {
                top.Value.View.gameObject.SetActive(false);
            }

            UIHandle handle = await factory.OpenAsync(UIKind.Page, UILayer.Page, prefabPath, args, false, true, null);
            stack.Push(handle);
            return handle;
        }

        public async Task<UIHandle> ReplaceAsync(string prefabPath, object? args = null)
        {
            UIHandle? top = PopInternal();
            if (top.HasValue)
            {
                factory.Close(top.Value, false, true);
            }

            UIHandle handle = await factory.OpenAsync(UIKind.Page, UILayer.Page, prefabPath, args, false, true, null);
            stack.Push(handle);
            return handle;
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
            }

            return true;
        }

        public async Task<UIHandle> ResetToAsync(string prefabPath, object? args = null)
        {
            while (stack.Count > 0)
            {
                UIHandle h = stack.Pop();
                factory.Close(h, false, true);
            }

            UIHandle root = await factory.OpenAsync(UIKind.Page, UILayer.Page, prefabPath, args, false, true, null);
            stack.Push(root);
            return root;
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
