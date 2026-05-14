/*
This file is a patch example for your current Assets/Scripts/GameEntry.cs.
Do not put this file into Unity as-is unless you rename/remove the comment wrapper.

Based on the current repository version, add these lines after:
DataManager.Instance.Initialize();

    BaseManager.Instance.Initialize(20);
    EnemySpawner.Instance.Initialize();
    EnsureEnemyUpdateDriver();

Then add this method inside GameEntry:

    private void EnsureEnemyUpdateDriver()
    {
        EnemyUpdateDriver driver = FindObjectOfType<EnemyUpdateDriver>();

        if (driver != null)
        {
            return;
        }

        GameObject driverObject = new GameObject("EnemyUpdateDriver");
        driverObject.AddComponent<EnemyUpdateDriver>();
    }

For manual testing, add NpcSpawnTestController to the GameEntry scene object.
Press F5 to load map 1 and spawn NPC 1001.
Press F6 to spawn another NPC 1001 on the currently loaded map.
*/
