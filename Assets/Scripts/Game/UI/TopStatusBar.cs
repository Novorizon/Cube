using TMPro;
using UnityEngine;

public class TopStatusBar : MonoBehaviour
{
    [SerializeField]
    private TMP_Text goldText;

    [SerializeField]
    private TMP_Text baseLifeText;

    [SerializeField]
    private TMP_Text waveText;

    private void OnEnable()
    {
        ItemManager.Instance.OnItemChanged += OnItemChanged;
        GameStatsManager.Instance.OnBaseLifeChanged += RefreshBaseLife;
        GameStatsManager.Instance.OnWaveChanged += RefreshWave;

        RefreshGold(ItemManager.Instance.GetCount(ItemIds.Gold));
        RefreshBaseLife(GameStatsManager.Instance.BaseLife);
        RefreshWave(GameStatsManager.Instance.CurrentWave);
    }

    private void OnDisable()
    {
        ItemManager.Instance.OnItemChanged -= OnItemChanged;
        GameStatsManager.Instance.OnBaseLifeChanged -= RefreshBaseLife;
        GameStatsManager.Instance.OnWaveChanged -= RefreshWave;
    }

    private void OnItemChanged(int itemId, int count)
    {
        if (itemId == ItemIds.Gold)
        {
            RefreshGold(count);
        }
    }

    private void RefreshGold(int gold)
    {
        if (goldText != null)
        {
            goldText.text = $"金币: {gold}";
        }
    }

    private void RefreshBaseLife(int life)
    {
        if (baseLifeText != null)
        {
            baseLifeText.text = $"生命: {life}";
        }
    }

    private void RefreshWave(int wave)
    {
        if (waveText != null)
        {
            waveText.text = $"波次: {wave}";
        }
    }
}
