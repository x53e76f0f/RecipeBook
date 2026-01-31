using System;
using System.Collections.Generic;
using System.Linq;
using Barotrauma;
using HarmonyLib;
using Microsoft.Xna.Framework.Input;

namespace RecipeBook
{
    /// <summary>
    /// Recipe Book mod: collects fabrication recipes and shows a Helmod-style list (result | ingredients).
    /// Client-only. Открытие: F6, или команда recipebook/rb, или кнопка внизу справа.
    /// </summary>
    public class RecipeBookMod : IAssemblyPlugin
    {
#if CLIENT
        private Harmony _harmony;
#endif

        public void Initialize()
        {
#if CLIENT
            _harmony = new Harmony("RecipeBook");
#endif
        }

        public void OnLoadCompleted()
        {
#if CLIENT
            _harmony?.PatchAll();
            IReadOnlyList<RecipeEntry> recipes = CollectRecipes();
            RecipeBookUI.Initialize(recipes);
            RegisterConsoleCommand();
#endif
        }

#if CLIENT
        /// <summary> Открыть/закрыть окно. Вызывается по F6, команде recipebook/rb или кнопке. </summary>
        public static void OpenRecipeBook()
        {
            if (!RecipeBookUI.IsInitialized)
            {
                var recipes = CollectRecipes();
                RecipeBookUI.Initialize(recipes);
            }
            if (RecipeBookUI.IsInitialized)
                RecipeBookUI.Toggle();
            else
                DebugConsole.NewMessage("[Recipe Book] Зайдите в кампанию/подлодку и нажмите F6 или введите recipebook.", Microsoft.Xna.Framework.Color.Orange);
        }

        private static void RegisterConsoleCommand()
        {
            try
            {
                var cmd = new DebugConsole.Command(
                    "recipebook|rb",
                    "recipebook (или rb): открыть/закрыть окно сборника рецептов крафта",
                    (string[] args) => OpenRecipeBook());
                DebugConsole.Commands.Add(cmd);
            }
            catch (Exception ex)
            {
                LuaCsLogger.LogError($"[RecipeBook] RegisterConsoleCommand: {ex.Message}");
            }
        }
#endif

        public void Dispose()
        {
#if CLIENT
            _harmony?.UnpatchSelf();
            RecipeBookUI.Dispose();
#endif
        }

        public void PreInitPatching() { }

#if CLIENT
        /// <summary>
        /// One row for the recipe list: result item + ingredients + device (where it's crafted).
        /// </summary>
        public sealed class RecipeEntry
        {
            public string ResultName { get; }
            public string ResultDisplayName { get; }
            public List<(string Name, int Amount)> Ingredients { get; }
            /// <summary> Название устройства, где крафтится (Fabricator, Deconstructor, Medical Fabricator и т.д.). </summary>
            public string DeviceName { get; }

            public RecipeEntry(string resultName, string resultDisplayName, List<(string Name, int Amount)> ingredients, string deviceName = "")
            {
                ResultName = resultName ?? "";
                ResultDisplayName = resultDisplayName ?? resultName ?? "";
                Ingredients = ingredients ?? new List<(string, int)>();
                DeviceName = deviceName ?? "";
            }
        }

        private static IReadOnlyList<RecipeEntry> CollectRecipes()
        {
            var list = new List<RecipeEntry>();
            try
            {
                foreach (ItemPrefab prefab in ItemPrefab.Prefabs)
                {
                    if (prefab?.FabricationRecipes == null) continue;
                    foreach (var recipe in prefab.FabricationRecipes.Values)
                    {
                        if (recipe?.RequiredItems == null || recipe.RequiredItems.Length == 0) continue;
                        string resultName = recipe.TargetItemPrefabIdentifier.Value;
                        string resultDisplayName = recipe.DisplayName?.Value ?? recipe.TargetItem?.Name?.Value ?? resultName;
                        var ingredients = new List<(string Name, int Amount)>();
                        foreach (var req in recipe.RequiredItems)
                        {
                            int amount = req.Amount;
                            foreach (var ip in req.ItemPrefabs)
                            {
                                if (ip == null) continue;
                                ingredients.Add((ip.Name?.Value ?? ip.Identifier.Value, amount));
                                break; // one representative prefab per RequiredItem
                            }
                        }
                        if (ingredients.Count > 0)
                        {
                            string deviceName = GetDeviceNameFromRecipe(recipe);
                            list.Add(new RecipeEntry(resultName, resultDisplayName, ingredients, deviceName));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LuaCsLogger.LogError($"[RecipeBook] CollectRecipes: {ex.Message}");
            }
            return list;
        }

        /// <summary>
        /// Имя устройства из рецепта: suitablefabricators или "Fabricator" если подходит любой.
        /// </summary>
        private static string GetDeviceNameFromRecipe(FabricationRecipe recipe)
        {
            if (recipe.SuitableFabricatorIdentifiers.Length == 0)
                return "Fabricator";
            var names = new List<string>();
            foreach (var id in recipe.SuitableFabricatorIdentifiers)
            {
                if (id.IsEmpty) continue;
                if (ItemPrefab.Prefabs.TryGet(id, out var devicePrefab))
                    names.Add(devicePrefab.Name?.Value ?? id.Value);
                else
                    names.Add(id.Value);
            }
            return names.Count > 0 ? string.Join(", ", names) : "Fabricator";
        }
#endif
    }
}
