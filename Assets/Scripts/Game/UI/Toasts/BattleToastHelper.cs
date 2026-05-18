using UI;

namespace Game
{
    public static class BattleToastHelper
    {
        private const string BattleToastPath = "Assets/Arts/UI/Toasts/BattleToast.prefab";

        public static void Show(string message)
        {
            UIManager.Instance.Toasts.Enqueue(
                BattleToastPath,
                message,
                new ToastOptions
                {
                    MergeKey = message
                });
        }

        public static void ShowNotEnoughGold()
        {
            Show("金币不足");
        }

        public static void ShowCannotBuildHere()
        {
            Show("该地块不可建造");
        }
    }
}