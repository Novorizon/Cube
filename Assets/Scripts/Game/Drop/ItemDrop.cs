using UnityEngine;

/// <summary>
/// Item 的场景掉落表现。
/// Drop / Pick 不是独立资源类型，只是 Item 的表现和交互。
/// </summary>
public class ItemDrop : MonoBehaviour
{
    [SerializeField]
    private int itemId;

    [SerializeField]
    private int count = 1;

    [SerializeField]
    private bool autoPick = true;

    [SerializeField]
    private float autoPickDelay = 0.3f;

    private float timer;
    private bool picked;

    public int ItemId => itemId;
    public int Count => count;

    public void Initialize(int itemId, int count, bool autoPick)
    {
        this.itemId = itemId;
        this.count = count;
        this.autoPick = autoPick;

        timer = 0f;
        picked = false;
    }

    private void Update()
    {
        if (!autoPick || picked)
        {
            return;
        }

        timer += Time.deltaTime;

        if (timer >= autoPickDelay)
        {
            Pick();
        }
    }

    public void Pick()
    {
        if (picked)
        {
            return;
        }

        picked = true;

        ItemManager.Instance.AddItem(itemId, count);

        Destroy(gameObject);
    }

    private void OnMouseDown()
    {
        if (autoPick)
        {
            return;
        }

        Pick();
    }
}
