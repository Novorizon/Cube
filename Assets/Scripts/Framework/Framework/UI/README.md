# Unity UI Framework (Pro Management Skeleton)

可读性优先的 UI 框架骨架：**分类明确 + 管理策略专业**。

## 分类与管理器（哪一类用哪些接口/入口）
- **Page**：`UIPage : UIView` + `PageNavigator`  
  入口：`UIRoot.Instance.Pages.PushAsync/ReplaceAsync/Pop/ResetToAsync`  
  原因：需要“返回路径”历史 → **栈**
- **Popup**：`UIPopup : UIView` + `PopupManager`  
  入口：`UIRoot.Instance.Popups.OpenAsync/CloseTop/Close`  
  特性：`PopupOptions.Modal` 创建遮罩；返回键先关最上层
- **Panel（侧边栏/非模态面板）**：`UIPanel : UIView` + `PanelManager`  
  入口：`ShowAsync/Hide/ToggleAsync`（多数是开关状态，不进 page 栈）  
  可选：`GroupId` → `PushInGroupAsync/PopGroup` 做“面板内深入”栈
- **Overlay（Loading/Blocker 等状态层）**：`UIOverlay : UIView` + `OverlayManager`  
  入口：`using var token = await Overlays.ShowBlockingAsync(path);`  
  原因：它是“状态”不是“路径” → **token/引用计数** 防止并发提前关闭
- **Toast（短提示）**：`UIToast : UIView` + `ToastManager`  
  入口：`Toasts.Enqueue(path, args)`  
  原因：短暂提示，不需要返回 → **队列**

## Back 优先级（标准建议）
`UIRoot.Instance.HandleBack()`：
1) 关闭最上层 Popup  
2) 隐藏任意 HideOnBack 的 Panel  
3) Pop Page（如果栈深度>1）  
4) 返回 false（交给游戏退出逻辑）

## 快速使用
```csharp
await UI.UIRoot.Instance.Pages.ResetToAsync("UI/Pages/MainMenuPage");
await UI.UIRoot.Instance.Popups.OpenAsync("UI/Popups/SettingsPopup");
UI.UIRoot.Instance.Toasts.Enqueue("UI/Toasts/SimpleToast", "Saved!");
```
