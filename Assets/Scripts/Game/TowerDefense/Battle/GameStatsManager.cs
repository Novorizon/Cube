using System;
using UnityEngine;

/// <summary>
/// 战斗状态管理。
/// 注意：金币不在这里，金币由 ItemManager 管。
/// </summary>
public class GameStatsManager
{
    public static GameStatsManager Instance { get; } = new GameStatsManager();

    public int BaseLife { get; private set; }
    public int CurrentWave { get; private set; }
    public int KilledNpcCount { get; private set; }

    public event Action<int> OnBaseLifeChanged;
    public event Action<int> OnWaveChanged;
    public event Action<int> OnKilledNpcCountChanged;

    private GameStatsManager()
    {
    }

    public void Initialize(int baseLife)
    {
        BaseLife = Mathf.Max(0, baseLife);
        CurrentWave = 0;
        KilledNpcCount = 0;

        OnBaseLifeChanged?.Invoke(BaseLife);
        OnWaveChanged?.Invoke(CurrentWave);
        OnKilledNpcCountChanged?.Invoke(KilledNpcCount);

        Debug.Log("GameStatsManager initialized.");
    }

    public void SetBaseLife(int life)
    {
        BaseLife = Mathf.Max(0, life);
        OnBaseLifeChanged?.Invoke(BaseLife);
    }

    public void AddBaseLife(int amount)
    {
        if (amount == 0)
        {
            return;
        }

        BaseLife = Mathf.Max(0, BaseLife + amount);
        OnBaseLifeChanged?.Invoke(BaseLife);
    }

    public void SetCurrentWave(int wave)
    {
        CurrentWave = Mathf.Max(0, wave);
        OnWaveChanged?.Invoke(CurrentWave);
    }

    public void AddKilledNpcCount(int count)
    {
        if (count <= 0)
        {
            return;
        }

        KilledNpcCount += count;
        OnKilledNpcCountChanged?.Invoke(KilledNpcCount);
    }
}
