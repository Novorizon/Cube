namespace UI
{
    public abstract class UIPopup : UIView
    {
        public virtual bool CloseOnBlockerClick => false;
        public override UICloseTriggers CloseTriggers =>
            UICloseTriggers.CloseButton |
            UICloseTriggers.Back |
            (CloseOnBlockerClick ? UICloseTriggers.LeftOutside | UICloseTriggers.RightOutside : UICloseTriggers.None);
    }
}
