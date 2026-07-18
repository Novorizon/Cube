using System.Collections.Generic;
using Game.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class WorldHpBarManager
    {
        private const string PrefabPath = "Assets/Arts/UI/Panels/Battle/WorldHpBar.prefab";

        public static WorldHpBarManager Instance { get; } = new WorldHpBarManager();

        private readonly Dictionary<Npc, WorldHpBarView> npcBars = new Dictionary<Npc, WorldHpBarView>();
        private GameObject barPrefab;
        private RectTransform root;
        private WorldHpBarView baseBar;
        private bool initialized;

        private WorldHpBarManager()
        {
        }

        public void Initialize()
        {
            Clear();
            barPrefab = ResourceManager.Instance.LoadGameObject(PrefabPath);
            if (barPrefab == null)
            {
                Debug.LogError($"World HP bar prefab could not be loaded: {PrefabPath}");
                return;
            }

            CreateRoot();
            NpcManager.Instance.NpcRegistered += BindNpc;
            NpcManager.Instance.NpcUnregistered += UnbindNpc;
            BaseManager.Instance.BaseLoaded += BindBase;
            BaseManager.Instance.BaseRemoving += UnbindBase;
            initialized = true;

            IReadOnlyList<Npc> activeNpcs = NpcManager.Instance.ActiveNpcs;
            for (int i = 0; i < activeNpcs.Count; i++)
            {
                BindNpc(activeNpcs[i]);
            }

            if (BaseManager.Instance.HasBaseObject)
            {
                BindBase();
            }
        }

        public void Update()
        {
            if (!initialized)
            {
                return;
            }

            foreach (KeyValuePair<Npc, WorldHpBarView> pair in npcBars)
            {
                Npc npc = pair.Key;
                if (npc != null && npc.Data != null && pair.Value != null)
                {
                    pair.Value.SetLife(npc.Data.CurrentHp, npc.Data.MaxHp);
                }
            }

            if (baseBar != null)
            {
                baseBar.SetLife(BaseManager.Instance.CurrentLife, BaseManager.Instance.MaxLife);
            }
        }

        public void Clear()
        {
            NpcManager.Instance.NpcRegistered -= BindNpc;
            NpcManager.Instance.NpcUnregistered -= UnbindNpc;
            BaseManager.Instance.BaseLoaded -= BindBase;
            BaseManager.Instance.BaseRemoving -= UnbindBase;

            npcBars.Clear();
            baseBar = null;
            barPrefab = null;
            initialized = false;

            if (root != null)
            {
                Object.Destroy(root.gameObject);
                root = null;
            }
        }

        private void BindNpc(Npc npc)
        {
            if (!initialized || npc == null || npc.Data == null || npcBars.ContainsKey(npc))
            {
                return;
            }

            WorldHpBarView bar = CreateBar();
            if (bar == null)
            {
                return;
            }

            int npcId = npc.Config != null ? npc.Config.Id : 0;
            bar.Bind(npc.transform, LocalizedConfigText.NpcName(npcId), Vector3.up * 1.8f, Camera.main);
            bar.SetLife(npc.Data.CurrentHp, npc.Data.MaxHp);
            npcBars[npc] = bar;
        }

        private void UnbindNpc(Npc npc)
        {
            if (npc == null || !npcBars.TryGetValue(npc, out WorldHpBarView bar))
            {
                return;
            }

            npcBars.Remove(npc);
            if (bar != null)
            {
                Object.Destroy(bar.gameObject);
            }
        }

        private void BindBase()
        {
            UnbindBase();
            Transform target = BaseManager.Instance.BaseTransform;
            if (!initialized || target == null)
            {
                return;
            }

            baseBar = CreateBar();
            if (baseBar == null)
            {
                return;
            }

            int baseId = BaseManager.Instance.Config != null ? BaseManager.Instance.Config.Id : 0;
            baseBar.Bind(target, LocalizedConfigText.BaseName(baseId), Vector3.up * 2.2f, Camera.main);
            baseBar.SetLife(BaseManager.Instance.CurrentLife, BaseManager.Instance.MaxLife);
        }

        private void UnbindBase()
        {
            if (baseBar != null)
            {
                Object.Destroy(baseBar.gameObject);
                baseBar = null;
            }
        }

        private WorldHpBarView CreateBar()
        {
            if (barPrefab == null || root == null)
            {
                return null;
            }

            GameObject instance = Object.Instantiate(barPrefab, root, false);
            WorldHpBarView view = instance.GetComponent<WorldHpBarView>();
            if (view == null)
            {
                Debug.LogError($"World HP bar prefab is missing {nameof(WorldHpBarView)}.", barPrefab);
                Object.Destroy(instance);
            }

            return view;
        }

        private void CreateRoot()
        {
            GameObject rootObject = new GameObject("BattleWorldHpBars", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Object.DontDestroyOnLoad(rootObject);
            root = rootObject.GetComponent<RectTransform>();

            Canvas canvas = rootObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 900;

            CanvasScaler scaler = rootObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }
    }
}
