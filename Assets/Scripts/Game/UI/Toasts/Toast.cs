namespace UI
{
    public static class RequirementToast
    {
        /// <summary>
        /// Returns true when requirements pass. Otherwise shows one merged warning toast.
        /// </summary>
        public static bool TryPass(Game.RequirementResult result)
        {
            if (result.Succeeded)
            {
                return true;
            }

            Toast.Warning(result.Message);
            return false;
        }
    }

    public static class Toast
    {
        private const string SimpleToastPath = "Assets/Arts/UI/Toasts/SimpleToast.prefab";

        public static void Info(string message)
        {
            Show(message, ToastLevel.Info);
        }

        public static void Warning(string message)
        {
            Show(message, ToastLevel.Warning);
        }

        public static void Error(string message)
        {
            Show(message, ToastLevel.Error);
        }

        public static void Show(string message, ToastLevel level)
        {
            if (string.IsNullOrEmpty(message))
            {
                message = string.Empty;
            }

            UIManager.Instance.Toasts.Enqueue(
                SimpleToastPath,
                new ToastArgs(message, level),
                new ToastOptions
                {
                    MergeKey = BuildMergeKey(SimpleToastPath, message, level)
                });
        }

        private static string BuildMergeKey(string prefabPath, string message, ToastLevel level)
        {
            return prefabPath + "|" + level + "|" + message;
        }
    }
}
