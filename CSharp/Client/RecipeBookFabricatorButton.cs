#if CLIENT

using System;
using System.Reflection;
using Barotrauma;
using HarmonyLib;
using Microsoft.Xna.Framework;

namespace RecipeBook
{
    /// <summary>
    /// Добавляет vanilla-style кнопку «Recipe Book» в правую часть хедера Fabricator (под заголовком).
    /// Fabricator — внутренний тип клиентской сборки, не API; патч применяется вручную по имени типа, логика — через рефлексию.
    /// </summary>
    internal static class RecipeBookFabricatorButton
    {
        /// <summary> Глобальная защита от рекурсии: без lock, чтобы не вызывать дедлок при вызове GUI из другого потока. </summary>
        private static bool _inPostfix;

        /// <summary> Postfix для CreateGUI; __instance — Fabricator, тип не доступен в сборке мода, поэтому object + рефлексия. </summary>
        public static void Postfix(object __instance)
        {
            if (__instance == null || _inPostfix) return;
            _inPostfix = true;
            try
            {
                GUIFrame guiFrame = GetGuiFrame(__instance);
                if (guiFrame?.RectTransform == null || guiFrame.RectTransform.CountChildren < 1) return;

                RectTransform paddedFrameRect = guiFrame.RectTransform.GetChild(0);
                if (paddedFrameRect == null) return;
                // Только каноничная структура: ровно label (0) и innerArea (1). В редакторе/других локациях может быть иначе.
                if (paddedFrameRect.CountChildren != 2) return;

                var rowRect = new RectTransform(new Vector2(1f, 0.04f), paddedFrameRect);
                var rowLayout = new GUILayoutGroup(rowRect, isHorizontal: true, childAnchor: Anchor.CenterRight)
                {
                    Stretch = true,
                    RelativeSpacing = 0.02f,
                    AbsoluteSpacing = GUI.IntScale(4)
                };

                new GUIFrame(new RectTransform(new Vector2(1f, 1f), rowLayout.RectTransform), style: null, color: Color.Transparent)
                { CanBeFocused = false };

                var btnRect = new RectTransform(new Vector2(0.18f, 1f), rowLayout.RectTransform)
                { MinSize = new Point(GUI.IntScale(90), GUI.IntScale(22)) };
                var btn = new GUIButton(btnRect, TextManager.Get("recipebook.fabricatorbutton"), Alignment.Center, "GUIButtonSmall")
                {
                    ToolTip = TextManager.Get("recipebook.fabricatorbutton.tooltip"),
                    OnClicked = (_, __) => { RecipeBookMod.OpenRecipeBook(); return true; }
                };

                rowRect.RepositionChildInHierarchy(1);
                RectTransform innerAreaRect = paddedFrameRect.GetChild(2);
                if (innerAreaRect != null)
                    innerAreaRect.RelativeSize = new Vector2(1f, 0.91f);
            }
            finally
            {
                _inPostfix = false;
            }
        }

        private static GUIFrame GetGuiFrame(object itemComponent)
        {
            PropertyInfo pi = itemComponent.GetType().GetProperty("GuiFrame", BindingFlags.Public | BindingFlags.Instance);
            return pi?.GetValue(itemComponent) as GUIFrame;
        }

        /// <summary> Применить Postfix к Fabricator.CreateGUI без ссылки на тип Fabricator. </summary>
        public static void ApplyPatch(Harmony harmony)
        {
            if (harmony == null) return;
            Type fabricatorType = Type.GetType("Barotrauma.Items.Components.Fabricator, BarotraumaClient");
            if (fabricatorType == null)
            {
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (a.GetName().Name != "BarotraumaClient") continue;
                    fabricatorType = a.GetType("Barotrauma.Items.Components.Fabricator");
                    if (fabricatorType != null) break;
                }
            }
            if (fabricatorType == null) return;

            MethodInfo createGui = fabricatorType.GetMethod("CreateGUI", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (createGui == null) return;

            MethodInfo postfix = typeof(RecipeBookFabricatorButton).GetMethod("Postfix", BindingFlags.Public | BindingFlags.Static);
            if (postfix == null) return;

            harmony.Patch(createGui, postfix: new HarmonyMethod(postfix));
        }
    }
}

#endif
