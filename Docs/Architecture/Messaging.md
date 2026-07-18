# Messaging

本文记录消息系统的使用边界。

## 框架位置

```text
Assets/Scripts/Framework/Message
Assets/Scripts/Game/Message
```

Framework 负责通用消息能力，Game 层定义业务消息和 topic。

## Quest 相关消息

任务系统当前重要消息：

```text
QuestAcceptedMessage
QuestChangedMessage
QuestCompletedMessage
```

`QuestManager` 负责发消息，UI、Toast、Story、引导、音频等系统按需要订阅。Toast 应该是监听器，不要写死在任务目标逻辑里。

当前 topic 仍在 `WorldMessageTopic`，这是历史共享枚举；不要因此把 Quest 域类改成 `WorldQuest*`。

## 使用原则

- 消息用于跨模块通知，不用于替代清晰的同步 API。
- 任务、剧情、UI 提示这类松耦合联动可以用消息。
- 同一模块内部的直接状态变更优先用 manager 方法。








