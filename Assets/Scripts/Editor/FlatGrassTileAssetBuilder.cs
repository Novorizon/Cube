#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class TDEffectPrefabCreator
{
    private const string EffectFolder = "Assets/Resources/Effects";
    private const string MaterialFolder = "Assets/Resources/Effects/Materials";

    [MenuItem("Tools/TD/Create Effect Prefabs")]
    public static void CreateEffectPrefabs()
    {
        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Resources", "Effects");
        EnsureFolder("Assets/Resources/Effects", "Materials");

        Material whiteMaterial = CreateMaterial("Effect_White", new Color(1f, 1f, 1f, 1f));
        Material yellowMaterial = CreateMaterial("Effect_Yellow", new Color(1f, 0.75f, 0.2f, 1f));
        Material orangeMaterial = CreateMaterial("Effect_Orange", new Color(1f, 0.35f, 0.08f, 1f));
        Material blueMaterial = CreateMaterial("Effect_Blue", new Color(0.35f, 0.75f, 1f, 1f));
        Material grayMaterial = CreateMaterial("Effect_Gray", new Color(0.45f, 0.45f, 0.45f, 1f));
        Material darkMaterial = CreateMaterial("Effect_Dark", new Color(0.12f, 0.12f, 0.12f, 1f));

        CreateBowAttackEffect(yellowMaterial);
        CreateBowHitEffect(yellowMaterial, orangeMaterial);

        CreateIceAttackEffect(blueMaterial, whiteMaterial);
        CreateIceHitEffect(blueMaterial, whiteMaterial);

        CreateBaseDamageStageOneEffect(grayMaterial);
        CreateBaseDamageStageTwoEffect(grayMaterial, orangeMaterial);
        CreateBaseDestroyedEffect(grayMaterial, orangeMaterial, darkMaterial);

        CreateNpcDeathBurstEffect(orangeMaterial, grayMaterial);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("TD effect prefabs created.");
    }

    private static void CreateBowAttackEffect(Material material)
    {
        GameObject root = new GameObject("BowAttackEffect");
        root.AddComponent<VisualProjectileEffect>();

        GameObject arrow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        arrow.name = "ArrowBody";
        arrow.transform.SetParent(root.transform);
        arrow.transform.localPosition = Vector3.zero;
        arrow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        arrow.transform.localScale = new Vector3(0.035f, 0.35f, 0.035f);

        Object.DestroyImmediate(arrow.GetComponent<Collider>());

        MeshRenderer renderer = arrow.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;

        TrailRenderer trail = root.AddComponent<TrailRenderer>();
        trail.time = 0.12f;
        trail.startWidth = 0.08f;
        trail.endWidth = 0f;
        trail.material = material;

        SavePrefab(root);
    }

    private static void CreateBowHitEffect(Material yellowMaterial, Material orangeMaterial)
    {
        GameObject root = new GameObject("BowHitEffect");

        ParticleSystem spark = AddParticleSystem(root, "Spark");
        ConfigureBurstParticle(spark, 16, 0.18f, 2.8f, 0.07f, yellowMaterial, ParticleSystemShapeType.Sphere);

        ParticleSystem dust = AddParticleSystem(root, "Dust");
        ConfigureBurstParticle(dust, 10, 0.25f, 1.1f, 0.12f, orangeMaterial, ParticleSystemShapeType.Hemisphere);

        SavePrefab(root);
    }

    private static void CreateIceAttackEffect(Material blueMaterial, Material whiteMaterial)
    {
        GameObject root = new GameObject("IceAttackEffect");
        root.AddComponent<VisualProjectileEffect>();

        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "IceCore";
        core.transform.SetParent(root.transform);
        core.transform.localPosition = Vector3.zero;
        core.transform.localScale = Vector3.one * 0.18f;

        Object.DestroyImmediate(core.GetComponent<Collider>());

        MeshRenderer renderer = core.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = blueMaterial;

        TrailRenderer trail = root.AddComponent<TrailRenderer>();
        trail.time = 0.18f;
        trail.startWidth = 0.14f;
        trail.endWidth = 0.02f;
        trail.material = whiteMaterial;

        ParticleSystem mist = AddParticleSystem(root, "IceMist");
        ConfigureLoopParticle(mist, 20, 0.25f, 0.25f, 0.08f, blueMaterial, ParticleSystemShapeType.Sphere);

        SavePrefab(root);
    }

    private static void CreateIceHitEffect(Material blueMaterial, Material whiteMaterial)
    {
        GameObject root = new GameObject("IceHitEffect");

        ParticleSystem shards = AddParticleSystem(root, "IceShards");
        ConfigureBurstParticle(shards, 24, 0.32f, 2.2f, 0.08f, blueMaterial, ParticleSystemShapeType.Sphere);

        ParticleSystem frost = AddParticleSystem(root, "FrostRing");
        ConfigureBurstParticle(frost, 18, 0.45f, 0.9f, 0.14f, whiteMaterial, ParticleSystemShapeType.Circle);

        SavePrefab(root);
    }

    private static void CreateBaseDamageStageOneEffect(Material grayMaterial)
    {
        GameObject root = new GameObject("BaseDamageStageOneEffect");

        ParticleSystem smoke = AddParticleSystem(root, "LightSmoke");
        ConfigureLoopParticle(smoke, 12, 1.2f, 0.45f, 0.35f, grayMaterial, ParticleSystemShapeType.Cone);

        SavePrefab(root);
    }

    private static void CreateBaseDamageStageTwoEffect(Material grayMaterial, Material orangeMaterial)
    {
        GameObject root = new GameObject("BaseDamageStageTwoEffect");

        ParticleSystem smoke = AddParticleSystem(root, "HeavySmoke");
        ConfigureLoopParticle(smoke, 24, 1.6f, 0.55f, 0.48f, grayMaterial, ParticleSystemShapeType.Cone);

        ParticleSystem sparks = AddParticleSystem(root, "Sparks");
        ConfigureLoopParticle(sparks, 18, 0.35f, 1.5f, 0.06f, orangeMaterial, ParticleSystemShapeType.Cone);

        SavePrefab(root);
    }

    private static void CreateBaseDestroyedEffect(Material grayMaterial, Material orangeMaterial, Material darkMaterial)
    {
        GameObject root = new GameObject("BaseDestroyedEffect");

        ParticleSystem explosion = AddParticleSystem(root, "Explosion");
        ConfigureBurstParticle(explosion, 40, 0.45f, 4.2f, 0.22f, orangeMaterial, ParticleSystemShapeType.Sphere);

        ParticleSystem debris = AddParticleSystem(root, "Debris");
        ConfigureBurstParticle(debris, 32, 0.8f, 3.4f, 0.16f, darkMaterial, ParticleSystemShapeType.Sphere);

        ParticleSystem smoke = AddParticleSystem(root, "Smoke");
        ConfigureBurstParticle(smoke, 36, 1.8f, 1.1f, 0.55f, grayMaterial, ParticleSystemShapeType.Hemisphere);

        SavePrefab(root);
    }

    private static void CreateNpcDeathBurstEffect(Material orangeMaterial, Material grayMaterial)
    {
        GameObject root = new GameObject("NpcDeathBurstEffect");

        ParticleSystem burst = AddParticleSystem(root, "DeathBurst");
        ConfigureBurstParticle(burst, 28, 0.35f, 2.6f, 0.12f, orangeMaterial, ParticleSystemShapeType.Sphere);

        ParticleSystem pieces = AddParticleSystem(root, "SmallPieces");
        ConfigureBurstParticle(pieces, 18, 0.55f, 1.8f, 0.1f, grayMaterial, ParticleSystemShapeType.Sphere);

        SavePrefab(root);
    }

    private static ParticleSystem AddParticleSystem(GameObject root, string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(root.transform);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;

        return child.AddComponent<ParticleSystem>();
    }

    private static void ConfigureBurstParticle(ParticleSystem particleSystem, int count, float lifetime, float speed, float size, Material material, ParticleSystemShapeType shapeType)
    {
        ParticleSystem.MainModule main = particleSystem.main;
        main.loop = false;
        main.duration = 0.12f;
        main.startLifetime = lifetime;
        main.startSpeed = speed;
        main.startSize = size;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = true;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[]
        {
            new ParticleSystem.Burst(0f, (short)count)
        });

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = shapeType;
        shape.radius = 0.25f;
        shape.angle = 25f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    private static void ConfigureLoopParticle(ParticleSystem particleSystem, int rate, float lifetime, float speed, float size, Material material, ParticleSystemShapeType shapeType)
    {
        ParticleSystem.MainModule main = particleSystem.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = lifetime;
        main.startSpeed = speed;
        main.startSize = size;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = true;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = rate;

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = shapeType;
        shape.radius = 0.25f;
        shape.angle = 18f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.75f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    private static Material CreateMaterial(string name, Color color)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material != null)
        {
            return material;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Particles/Standard Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        material = new Material(shader);
        material.name = name;
        material.color = color;

        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void SavePrefab(GameObject root)
    {
        string path = $"{EffectFolder}/{root.name}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif