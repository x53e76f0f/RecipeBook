#if CLIENT

using System;
using Barotrauma;
using HarmonyLib;
using Microsoft.Xna.Framework.Input;

namespace RecipeBook
{
    /// <summary>
    /// Открытие/закрытие Recipe Book по F6; закрытие по любой другой клавише, когда окно открыто и нет фокуса ввода.
    /// </summary>
    [HarmonyPatch(typeof(GUI), nameof(GUI.Update), new[] { typeof(float) })]
    internal static class RecipeBookKeyBindPatch
    {
        private static void Postfix(float deltaTime)
        {
            if (GUI.KeyboardDispatcher.Subscriber != null) return; // не перехватывать, если ввод в текстовое поле

            if (RecipeBookUI.IsPanelVisible)
            {
                if (PlayerInput.KeyHit(Keys.F6))
                    RecipeBookMod.OpenRecipeBook();
                else if (AnyKeyHitExcept(Keys.F6))
                    RecipeBookUI.Close();
            }
            else if (PlayerInput.KeyHit(Keys.F6))
            {
                RecipeBookMod.OpenRecipeBook();
            }
        }

        private static bool AnyKeyHitExcept(Keys exclude)
        {
            foreach (Keys key in Enum.GetValues(typeof(Keys)))
            {
                if (key == Keys.None || key == exclude) continue;
                if (PlayerInput.KeyHit(key)) return true;
            }
            return false;
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
