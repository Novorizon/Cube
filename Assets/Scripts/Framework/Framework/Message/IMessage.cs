///------------------------------------
/// Author：guanjinbiao
/// Mail：novogooglor@gmail.com
/// Date：2026-02-05
/// Description：只有实现 IMessage 的类型”才能被订阅/发送，避免误把普通业务类型当消息发出去
///------------------------------------
namespace Game.Framework
{
    public interface IMessage { }

    public class Notify : IMessage
    {
        public object Message {  get; set; }
    }
}
