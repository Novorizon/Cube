# Audio System

本文记录项目当前的简化音频系统。系统只区分背景音乐和普通音效，不建立额外的 UI、环境音或技能音效公开 API。

## 代码位置

```text
Assets/Scripts/Framework/Audio/AudioManager.cs
Assets/Scripts/Framework/Audio/AudioPlayOptions.cs
Assets/Scripts/Framework/Audio/AudioHandle.cs
```

现有 UI 兼容入口：

```text
Assets/Scripts/Game/UI/TowerDefense/GameAudioSettings.cs
```

## 公开播放 API

业务层只使用两个播放接口：

```csharp
AudioManager.Instance.PlayMusic(location, options);
AudioManager.Instance.PlaySound(location, options);
```

`PlayMusic` 默认循环，`PlaySound` 默认不循环。两者共享 `AudioPlayOptions`：

```text
Loop       null 使用类型默认值
Volume     本次播放基础音量，默认 1
Pitch      音高，默认 1
Duration   <= 0 表示按默认生命周期播放
Position   null 为 2D；传值为固定世界位置的 3D 音效
```

`Duration <= 0` 时，非循环音频播放到 Clip 自然结束，循环音频持续到主动停止。`Duration > 0` 使用真实时间限制播放时长，不受游戏 `Time.timeScale` 影响。

## 快速使用

调用代码需要引入命名空间：

```csharp
using Game.Framework;
```

### 播放背景音乐

背景音乐默认循环，最简单的调用不需要参数：

```csharp
AudioManager.Instance.PlayMusic("Assets/Audio/Music/Battle.ogg");
```

再次调用 `PlayMusic` 会发起新音乐加载。加载成功前旧音乐继续播放；新音乐开始播放时自动替换旧音乐。切换地图或模式时，建议在新流程加载成功后调用一次：

```csharp
AudioManager.Instance.PlayMusic(
    "Assets/Audio/Music/Boss.ogg",
    new AudioPlayOptions
    {
        Volume = 0.8f,
        Pitch = 1f,
    });
```

如果某段背景音乐只需要播放一次，可覆盖默认循环设置：

```csharp
AudioManager.Instance.PlayMusic(
    "Assets/Audio/Music/Victory.ogg",
    new AudioPlayOptions { Loop = false });
```

停止背景音乐：

```csharp
AudioManager.Instance.StopMusic();
```

当前没有场景音乐自动调度器，业务流程需要自己决定何时调用 `PlayMusic` 或 `StopMusic`。

### 播放普通音效

未传 `Position` 时作为 2D 音效播放，适合 UI、提示音和不需要空间定位的反馈：

```csharp
AudioManager.Instance.PlaySound("Assets/Audio/Sfx/Confirm.wav");
```

传入世界坐标时作为固定位置的 3D 音效播放：

```csharp
AudioManager.Instance.PlaySound(
    "Assets/Audio/Sfx/Explosion.wav",
    new AudioPlayOptions
    {
        Position = explosionPosition,
        Volume = 0.9f,
        Pitch = 1.05f,
    });
```

`Position` 只在开始播放时设置一次。需要声音跟随移动单位时，应由业务层持续更新音源或等待后续扩展跟随能力。

### 循环与时长

普通音效默认不循环。设置 `Loop = true` 后应保留句柄，以便在业务结束时停止：

```csharp
AudioHandle loopHandle = AudioManager.Instance.PlaySound(
    "Assets/Audio/Sfx/MachineLoop.wav",
    new AudioPlayOptions { Loop = true });

// 业务结束时：
loopHandle?.Stop();
```

也可以限制循环音效最多播放指定秒数，不需要另外计时：

```csharp
AudioManager.Instance.PlaySound(
    "Assets/Audio/Sfx/WarningLoop.wav",
    new AudioPlayOptions
    {
        Loop = true,
        Duration = 5f,
    });
```

句柄在等待异步加载期间同样有效。提前调用 `Stop` 可以取消这次请求，资源稍后返回时不会迟到播放：

```csharp
AudioHandle handle = AudioManager.Instance.PlaySound(location);
if (noLongerNeeded && handle != null && handle.IsValid)
{
    handle.Stop();
}
```

## 停止播放

播放接口返回 `AudioHandle`。普通一次性音效可以忽略返回值；循环音效或仍在异步加载的音效可以通过句柄取消：

```csharp
AudioHandle handle = AudioManager.Instance.PlaySound(
    location,
    new AudioPlayOptions { Loop = true });

handle.Stop();
```

也可以使用：

```csharp
AudioManager.Instance.Stop(handle);
AudioManager.Instance.StopMusic();
AudioManager.Instance.StopAllSounds();
```

## 播放实现

- 音频通过 `ResourceManager.LoadAssetAsync<AudioClip>` 从 YooAsset 加载。
- `AudioManager` 按需自动创建并跨场景保留，不要求在 Scene 或 Prefab 中预放节点。
- 背景音乐使用一个常驻 `AudioSource`；新音乐加载成功后替换当前音乐。
- 普通音效使用最多 16 个可复用 `AudioSource`。
- 音效池满时停止最早开始播放的音效并复用其声源。
- 句柄在异步加载完成前被停止时，不会发生迟到播放。
- `Position` 只设置播放开始时的固定世界位置，不自动跟随移动对象。

## 音量

当前音量分为：

```text
Master  使用 AudioListener.volume
Music  作用于背景音乐 AudioSource
Sound  作用于普通音效 AudioSource
```

保存键：

```text
World.Sound.Volume
Audio.Music.Volume
Audio.Sound.Volume
```

代码调用示例：

```csharp
AudioManager.Instance.SetMasterVolume(0.8f); // 所有音频
AudioManager.Instance.SetMusicVolume(0.6f);  // 仅背景音乐
AudioManager.Instance.SetSoundVolume(0.9f);  // 仅普通音效

AudioManager.Instance.Mute();
AudioManager.Instance.Unmute();
AudioManager.Instance.ToggleMute();
```

三个设置接口默认立即写入 `PlayerPrefs`。批量加载或临时预览时可以传 `save: false`，避免写盘。

现有 `WorldSoundPanel`、`BattleControlPanel` 和 `BattleSettingsPopup` 继续通过 `GameAudioSettings` 操作 Master 音量。`GameAudioSettings` 只是兼容门面，实际状态由 `AudioManager` 管理。

## Ability 接入

`TdPresentation.PlaySound` 已转发到：

```csharp
AudioManager.Instance.PlaySound(
    soundName,
    new AudioPlayOptions { Position = position });
```

Ability 的 `soundName` / 旧 Skill 的 `soundLocation` 应填写 YooAsset 可加载的音频资源完整路径。

## 资源要求与常见问题

- `location` 必须是 `ResourceManager.LoadAssetAsync<AudioClip>` 能加载的完整资源路径，并包含在当前 YooAsset 构建内容中。
- 路径为空时播放接口返回 `null`；加载失败时输出警告并使句柄失效。
- 当前仓库尚未提供正式音频资源，因此文档中的路径只是格式示例，接入时需替换为真实资源路径。
- 普通音效最多同时占用 16 个声源。达到上限时会停止最早开始的音效并复用声源。
- 播放自然结束、主动停止、加载失败或被声源池抢占后，`AudioHandle.IsValid` 都会变为 `false`。
- UI 音效、循环音效和技能音效仍调用 `PlaySound`，通过 `AudioPlayOptions` 表达差异，不新增 `PlayUi`、`PlayLoop` 等平行 API。

## 当前不包含

第一版不包含：

```text
AudioMixer 和 Snapshot
Audio Cue 配置表
场景音乐自动调度器
淡入淡出或双 AudioSource 交叉混音
按 Cue 的并发和冷却配置
让 3D 音效跟随 Transform
运行时音频调试窗口
```

需要这些能力时应在现有两个播放入口和 `AudioPlayOptions` 上扩展，不新增 `PlayUi`、`PlayLoop` 等平行公开 API。
