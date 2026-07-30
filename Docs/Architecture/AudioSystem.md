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

业务层只使用：

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

现有 `WorldSoundPanel`、`BattleControlPanel` 和 `BattleSettingsPopup` 继续通过 `GameAudioSettings` 操作 Master 音量。`GameAudioSettings` 只是兼容门面，实际状态由 `AudioManager` 管理。

## Ability 接入

`TdPresentation.PlaySound` 已转发到：

```csharp
AudioManager.Instance.PlaySound(
    soundName,
    new AudioPlayOptions { Position = position });
```

Ability 的 `soundName` / 旧 Skill 的 `soundLocation` 应填写 YooAsset 可加载的音频资源完整路径。

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
