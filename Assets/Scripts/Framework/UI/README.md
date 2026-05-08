# Unity UI Framework - Optimized

轻量 UI 框架优化版。

## 本次优化重点

- `UIManager` 作为 UI 统一入口，保留 `UIRoot` 兼容旧调用。
- Toast 不再使用 Coroutine，改为 `Task` + `CancellationToken`。
- 修复缓存、多实例、生命周期、资源释放、重复打开等问题。
- 新增 `ResourceManagerUIAssetLoader`，可对接 `Game.Framework.ResourceManager` / YooAsset。
- `UIMessageBus.Subscribe` 返回 `IDisposable`，便于 UI 生命周期内自动解绑。

## 推荐入口

```csharp
await UI.UIManager.Instance.Pages.ResetToAsync("Assets/Data/UI/Pages/MainMenuPage.prefab");
await UI.UIManager.Instance.Popups.OpenAsync("Assets/Data/UI/Popups/SettingsPopup.prefab");
UI.UIManager.Instance.Toasts.Enqueue("Assets/Data/UI/Toasts/SimpleToast.prefab", "Saved!");
```

## 使用 YooAsset / ResourceManager

```csharp
UI.UIManager.Instance.SetAssetLoader(new UI.ResourceManagerUIAssetLoader());
```

如果你的 UI prefab 仍在 Resources 目录，也可以继续使用默认的 `ResourcesUIAssetLoader`。

## Back 优先级

1. 关闭最上层 Popup
2. 隐藏 HideOnBack 的 Panel
3. Pop Page
4. 返回 false，交给游戏退出逻辑
