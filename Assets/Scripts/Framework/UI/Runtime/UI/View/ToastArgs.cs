namespace UI
{
    public sealed class ToastArgs
    {
        public string Message { get; }
        public ToastLevel Level { get; }

        public ToastArgs(string message, ToastLevel level = ToastLevel.Info)
        {
            Message = message;
            Level = level;
        }
    }
}