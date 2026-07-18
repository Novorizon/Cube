using Game.Framework;
using System;
using System.Threading.Tasks;
using UI;
using UnityEngine;

namespace Game
{
    public sealed class StoryPresenter
    {
        public async Task PresentAsync(StoryConfig config, Action completed)
        {
            UIHandle handle = await UIManager.Instance.Panels.ShowAsync(
                StoryPanel.PrefabPath,
                new StoryPanel.Args(config, completed),
                new PanelOptions
                {
                    UseOutsideClickDetector = false,
                    CacheOnClose = false,
                });

            if (!handle.IsValid)
            {
                Debug.LogError($"Story present failed. Missing or invalid panel prefab: {StoryPanel.PrefabPath}");
                completed?.Invoke();
            }
        }

        public void Present(StoryConfig config, Action completed)
        {
            PresentAsync(config, completed).Forget();
        }
    }
}
