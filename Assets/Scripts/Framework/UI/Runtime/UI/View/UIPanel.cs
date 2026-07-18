namespace UI
{
    public abstract class UIPanel : UIView
    {
        public virtual bool HideOnBack => true;
        public override UICloseTriggers CloseTriggers =>
            UICloseTriggers.CloseButton |
            (HideOnBack ? UICloseTriggers.Back : UICloseTriggers.None);
    }
}
