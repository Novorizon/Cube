namespace UI
{
    public readonly struct UIHandle
    {
        public readonly int Id;
        public readonly int Version;
        public readonly string PrefabPath;
        public readonly UIKind Kind;
        public readonly UILayer Layer;
        public readonly UIView View;

        public bool IsValid => Id != 0 && View != null && View.InstanceId == Id && View.InstanceVersion == Version && !View.IsDestroyed;

        public UIHandle(int id, int version, string prefabPath, UIKind kind, UILayer layer, UIView view)
        {
            Id = id;
            Version = version;
            PrefabPath = prefabPath;
            Kind = kind;
            Layer = layer;
            View = view;
        }

        public override string ToString()
        {
            return $"UIHandle(Id={Id}, Version={Version}, Kind={Kind}, Layer={Layer}, Path={PrefabPath}, View={(View != null ? View.name : "null")})";
        }
    }
}
