using System;
using System.Collections.Generic;

namespace Game.Skill
{
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
