/*
Patch example only. Do not add this file to compilation if your GameEntry is not partial.

Add these lines into GameEntry.Initialize after ResourceManager.InitializeAsync():

bool dataInitialized = DataManager.Instance.Initialize();

if (!dataInitialized)
{
    Debug.LogError("DataManager initialize failed.");
}

EnemySpawner.Instance.Initialize();
BaseManager.Instance.Initialize(20);
EnsureEnemyUpdateDriver();

Add this helper method to GameEntry:

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

Test after map load:

EnemySpawner.Instance.SpawnEnemyFromFirstSpawn(1001);
*/
