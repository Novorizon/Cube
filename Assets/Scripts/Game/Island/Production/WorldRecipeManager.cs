using System.Collections.Generic;
using System.Text;

namespace Game
{
    public sealed class WorldRecipeConfig
    {
        public int Id;
        public string Name;
        public int BuildingId;
        public WorldItem[] Inputs;
        public WorldItem[] Outputs;
        public bool Enable = true;
    }

    public sealed class WorldRecipeManager
    {
        public static WorldRecipeManager Instance { get; } = new WorldRecipeManager();

        private readonly Dictionary<int, WorldRecipeConfig> recipes = new Dictionary<int, WorldRecipeConfig>();
        private readonly Dictionary<int, List<WorldRecipeConfig>> recipesByBuildingId = new Dictionary<int, List<WorldRecipeConfig>>();
        private readonly StringBuilder builder = new StringBuilder(128);

        private WorldRecipeManager()
        {
        }

        public void Initialize()
        {
            recipes.Clear();
            recipesByBuildingId.Clear();
            AddDefaultRecipes();
        }

        public WorldRecipeConfig GetFirstRecipeForBuilding(int buildingId)
        {
            if (!recipesByBuildingId.TryGetValue(buildingId, out List<WorldRecipeConfig> list) || list.Count == 0)
            {
                return null;
            }

            return list[0];
        }

        public bool CanCraft(int recipeId)
        {
            if (!recipes.TryGetValue(recipeId, out WorldRecipeConfig recipe) || recipe == null || !recipe.Enable)
            {
                return false;
            }

            if (!HasActiveBuilding(recipe.BuildingId))
            {
                return false;
            }

            return WorldItemManager.Instance.HasItems(recipe.Inputs);
        }

        public bool TryCraftFirstForBuilding(int buildingId)
        {
            WorldRecipeConfig recipe = GetFirstRecipeForBuilding(buildingId);
            return recipe != null && TryCraft(recipe.Id);
        }

        public bool TryCraft(int recipeId)
        {
            if (!CanCraft(recipeId))
            {
                return false;
            }

            WorldRecipeConfig recipe = recipes[recipeId];
            if (!WorldItemManager.Instance.TryConsumeItems(recipe.Inputs))
            {
                return false;
            }

            WorldItemManager.Instance.AddItems(recipe.Outputs);
            return true;
        }

        public string FormatRecipe(WorldRecipeConfig recipe)
        {
            if (recipe == null)
            {
                return LocalizationManager.Get("ui.recipe.none");
            }

            builder.Clear();
            builder.AppendLine(LocalizedConfigText.RecipeName(recipe.Id, recipe.Name));
            builder.Append(LocalizationManager.Get("ui.recipe.input"));
            builder.Append(' ');
            AppendItems(builder, recipe.Inputs);
            builder.AppendLine();
            builder.Append(LocalizationManager.Get("ui.recipe.output"));
            builder.Append(' ');
            AppendItems(builder, recipe.Outputs);
            return builder.ToString();
        }

        private void AddDefaultRecipes()
        {
            AddRecipe(new WorldRecipeConfig
            {
                Id = 30600001,
                Name = "Saw Planks",
                BuildingId = 30000004,
                Inputs = new[] { new WorldItem(ItemIds.Wood, 5) },
                Outputs = new[] { new WorldItem(ItemIds.Plank, 1) },
            });

            AddRecipe(new WorldRecipeConfig
            {
                Id = 30600002,
                Name = "Mill Wheat",
                BuildingId = 30000007,
                Inputs = new[] { new WorldItem(ItemIds.Wheat, 5) },
                Outputs = new[] { new WorldItem(ItemIds.Food, 1) },
            });

            AddRecipe(new WorldRecipeConfig
            {
                Id = 30600003,
                Name = "Smelt Copper",
                BuildingId = 30000005,
                Inputs = new[] { new WorldItem(ItemIds.CopperOre, 5) },
                Outputs = new[] { new WorldItem(ItemIds.CopperIngot, 1) },
            });

            AddRecipe(new WorldRecipeConfig
            {
                Id = 30600004,
                Name = "Smelt Iron",
                BuildingId = 30000005,
                Inputs = new[] { new WorldItem(ItemIds.IronOre, 5) },
                Outputs = new[] { new WorldItem(ItemIds.IronIngot, 1) },
            });
        }

        private void AddRecipe(WorldRecipeConfig recipe)
        {
            if (recipe == null || recipe.Id <= 0 || recipe.BuildingId <= 0 || !recipe.Enable)
            {
                return;
            }

            recipes[recipe.Id] = recipe;
            if (!recipesByBuildingId.TryGetValue(recipe.BuildingId, out List<WorldRecipeConfig> list))
            {
                list = new List<WorldRecipeConfig>();
                recipesByBuildingId.Add(recipe.BuildingId, list);
            }

            list.Add(recipe);
        }

        private static bool HasActiveBuilding(int buildingId)
        {
            foreach (KeyValuePair<int, WorldBuilding> pair in WorldBuildingManager.Instance.GetAllBuildings())
            {
                WorldBuilding building = pair.Value;
                if (building != null && building.ConfigId == buildingId && building.Status == WorldBuildingStatus.Active)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AppendItems(StringBuilder builder, IReadOnlyList<WorldItem> items)
        {
            if (items == null || items.Count == 0)
            {
                builder.Append(LocalizationManager.Get("ui.common.none"));
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                WorldItem item = items[i];
                if (item == null)
                {
                    continue;
                }

                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(GetItemName(item.ItemId));
                builder.Append(' ');
                builder.Append(item.Count);
            }
        }

        private static string GetItemName(int itemId)
        {
            return LocalizedConfigText.ItemName(itemId);
        }
    }
}
