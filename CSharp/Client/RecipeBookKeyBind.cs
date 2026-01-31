#if CLIENT

using Barotrauma;
using HarmonyLib;
using Microsoft.Xna.Framework.Input;

namespace RecipeBook
{
    /// <summary>
    /// Открытие Recipe Book по клавише F6 (в GUI.Update).
    /// </summary>
    [HarmonyPatch(typeof(GUI), nameof(GUI.Update), new[] { typeof(float) })]
    internal static class RecipeBookKeyBindPatch
    {
        private static void Postfix(float deltaTime)
        {
            if (GUI.KeyboardDispatcher.Subscriber != null) return; // не перехватывать, если ввод в текстовое поле
            if (PlayerInput.KeyHit(Keys.F6))
            {
                RecipeBookMod.OpenRecipeBook();
            }
        }
    }

    /// <summary>
    /// Добавлять UI мода в список отрисовки в контексте игрового экрана — иначе окно не появляется
    /// (как в LineofSight SlotCompatibilityTooltip и SpeakerList: список собирается в GameScreen.AddToGUIUpdateList).
    /// </summary>
    [HarmonyPatch(typeof(GameScreen), nameof(GameScreen.AddToGUIUpdateList))]
    internal static class RecipeBook_GameScreen_AddToGUIUpdateList_Patch
    {
        private static void Postfix()
        {
            if (RecipeBookUI.IsInitialized)
                RecipeBookUI.AddToGUIUpdateList();
        }
    }
}

#endif
