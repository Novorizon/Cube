using UnityEngine;

/// <summary>
/// TD 战斗 HUD 容器。
/// 金币、生命、波次等顶部状态由 TopStatusBar 负责。
/// 建塔面板、商人面板、道具快捷栏可以作为子节点或独立 Panel。
/// </summary>
public class GameHudPage : MonoBehaviour
{
    [SerializeField]
    private TopStatusBar topStatusBar;

    public TopStatusBar TopStatusBar => topStatusBar;
}
