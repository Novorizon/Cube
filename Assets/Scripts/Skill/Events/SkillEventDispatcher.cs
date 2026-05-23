using System;
using System.Collections.Generic;

namespace Game.Skill
{
    /// <summary>
    /// 技能系统内部事件分发器。
    /// 它只服务于技能系统事件，不依赖 Framework.Messager，也不使用 LastEventData 这种全局缓存方式。
    /// 业务层如果需要 UI 刷新或表现响应，可以通过 SkillManager.EventDispatcher 订阅。
    /// </summary>
    public sealed class SkillEventDispatcher
    {
        private readonly Dictionary<SkillMessageTopic, List<Action<SkillEventData>>> handlerMap = new Dictionary<SkillMessageTopic, List<Action<SkillEventData>>>();

        public void Subscribe(SkillMessageTopic topic, Action<SkillEventData> handler)
        {
            if (handler == null)
            {
                return;
            }

            if (!handlerMap.TryGetValue(topic, out List<Action<SkillEventData>> handlers))
            {
                handlers = new List<Action<SkillEventData>>();
                handlerMap.Add(topic, handlers);
            }

            if (!handlers.Contains(handler))
            {
                handlers.Add(handler);
            }
        }

        public void Unsubscribe(SkillMessageTopic topic, Action<SkillEventData> handler)
        {
            if (handler == null)
            {
                return;
            }

            if (!handlerMap.TryGetValue(topic, out List<Action<SkillEventData>> handlers))
            {
                return;
            }

            handlers.Remove(handler);

            if (handlers.Count == 0)
            {
                handlerMap.Remove(topic);
            }
        }

        /// <summary>
        /// 发布事件。这里复制一份 handler 快照，避免回调中 Subscribe/Unsubscribe 影响当前遍历。
        /// </summary>
        public void Publish(SkillEventData eventData)
        {
            if (eventData == null)
            {
                return;
            }

            if (!handlerMap.TryGetValue(eventData.Topic, out List<Action<SkillEventData>> handlers))
            {
                return;
            }

            Action<SkillEventData>[] snapshot = handlers.ToArray();

            for (int i = 0; i < snapshot.Length; i++)
            {
                snapshot[i]?.Invoke(eventData);
            }
        }

        public void Clear()
        {
            handlerMap.Clear();
        }
    }
}