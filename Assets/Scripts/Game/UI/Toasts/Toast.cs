namespace UI
{
    public static class Toast
    {
        const string SimpleToastPath = "Assets/Data/UI/Toasts/SimpleToast.prefab";

        public static void Info(string message)
        {
            UIManager.Instance.Toasts.Enqueue(SimpleToastPath, new ToastArgs(message, ToastLevel.Info));
        }

        public static void Warning(string message)
        {
            UIManager.Instance.Toasts.Enqueue(SimpleToastPath, new ToastArgs(message, ToastLevel.Warning));
        }

        public static void Error(string message)
        {
            UIManager.Instance.Toasts.Enqueue(SimpleToastPath, new ToastArgs(message, ToastLevel.Error));
        }
    }
}