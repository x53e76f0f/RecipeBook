#if CLIENT

using System.Collections.Generic;
using Barotrauma;
using Microsoft.Xna.Framework;

namespace RecipeBook
{
    /// <summary>
    /// Helmod-style recipe list UI: toggle button + frame with scrollable list (result | ingredients).
    /// </summary>
    internal static class RecipeBookUI
    {
        private static IReadOnlyList<RecipeBookMod.RecipeEntry> _recipes;
        private static GUIFrame _rootFrame;
        private static GUIFrame _panelFrame;
        private static GUIButton _toggleButton;
        private static GUIListBox _listBox;

        /// <summary> UI создан и привязан к канвасу (кнопка и окно есть). У канваса нет GUIComponent, поэтому проверяем RectTransform.Parent. </summary>
        public static bool IsInitialized => _rootFrame != null && _rootFrame.RectTransform.Parent != null;

        /// <summary>
        /// Добавить корневой фрейм в список обновления/отрисовки GUI. Вызывать каждый кадр, иначе окно не рисуется (как в SpeakerList).
        /// </summary>
        public static void AddToGUIUpdateList()
        {
            if (_rootFrame != null)
                _rootFrame.AddToGUIUpdateList(order: 1);
        }

        public static void Initialize(IReadOnlyList<RecipeBookMod.RecipeEntry> recipes)
        {
            _recipes = recipes ?? new List<RecipeBookMod.RecipeEntry>();
            if (GUI.Canvas == null) return;
            if (_rootFrame != null) return; // уже создан

            // Root container (full screen, no draw) for our UI
            _rootFrame = new GUIFrame(new RectTransform(Vector2.One, GUI.Canvas), style: null)
            {
                CanBeFocused = false
            };

            // CreateToggleButton();
            CreatePanel();
        }

        /// <summary>
        /// Открыть/закрыть окно сборника рецептов. Вызывается кнопкой или консольной командой recipebook / rb.
        /// Если UI ещё не создан (Canvas был null при загрузке), не делает ничего — вызывающий должен сначала EnsureInitialized().
        /// </summary>
        public static void Toggle()
        {
            if (_panelFrame == null || _rootFrame == null) return;
            _panelFrame.Visible = !_panelFrame.Visible;
            if (_panelFrame.Visible)
            {
                RefreshList();
                // Как в ConfigHub: окно поверх остальных элементов и в дереве канваса
                if (!ReferenceEquals(_rootFrame.RectTransform.Parent, GUI.Canvas))
                    _rootFrame.RectTransform.Parent = GUI.Canvas;
                _rootFrame.SetAsLastChild();
                _rootFrame.AddToGUIUpdateList(order: 1);
            }
        }

        public static void Dispose()
        {
            // GUIComponent в Barotrauma не имеет Dispose — отвязываем корень от канваса (Parent у канваса = null, поэтому через RectTransform)
            if (_rootFrame?.RectTransform.Parent != null)
            {
                _rootFrame.RectTransform.Parent = null;
            }
            _toggleButton = null;
            _panelFrame = null;
            _listBox = null;
            _rootFrame = null;
            _recipes = null;
        }

        private static void CreateToggleButton()
        {
            // Button bottom-right, like Sandbox Menu
            float scale = GUI.Scale;
            var size = new Point((int)(140 * scale), (int)(25 * scale));
            var rect = new RectTransform(size, _rootFrame.RectTransform, Anchor.BottomRight)
            {
                AbsoluteOffset = new Point((int)(24 * scale), 0)
            };
            _toggleButton = new GUIButton(rect, "Recipe Book (F6)", Alignment.Center, "GUIButtonSmall")
            {
                OnClicked = (btn, _) => { RecipeBookMod.OpenRecipeBook(); return true; }
            };
        }

        // Helmod-подобные цвета: тёмная панель, заголовок, акцент результата
        private static readonly Color HelmodPanelBg = new Color(0.10f, 0.12f, 0.16f, 0.98f);
        private static readonly Color HelmodHeaderBg = new Color(0.14f, 0.17f, 0.22f, 0.98f);
        private static readonly Color HelmodRowAlt = new Color(0.12f, 0.14f, 0.18f, 0.85f);
        private const float ResultColumnRatio = 0.38f;

        private static void CreatePanel()
        {
            // Helmod-style: тёмная панель, полоса заголовка, таблица результат | ингредиенты
            var panelSize = new Vector2(0.5f, 0.7f);
            var rect = new RectTransform(panelSize, _rootFrame.RectTransform, Anchor.Center)
            {
                MinSize = new Point(GUI.IntScale(380), GUI.IntScale(400))
            };
            _panelFrame = new GUIFrame(rect, style: "InnerFrame", color: HelmodPanelBg)
            {
                Visible = false,
                CanBeFocused = true
            };

            var layout = new GUILayoutGroup(new RectTransform(new Vector2(0.98f, 0.98f), _panelFrame.RectTransform, Anchor.Center), isHorizontal: false)
            {
                Stretch = true,
                RelativeSpacing = 0.008f
            };

            // Полоса заголовка (Helmod: тёмная шапка)
            var headerBg = new GUIFrame(new RectTransform(new Vector2(1f, 0.07f), layout.RectTransform), style: null, color: HelmodHeaderBg)
            {
                CanBeFocused = false
            };
            var headerText = new GUITextBlock(new RectTransform(new Vector2(0.96f, 0.85f), headerBg.RectTransform, Anchor.CenterLeft)
            {
                AbsoluteOffset = new Point(GUI.IntScale(8), 0)
            }, "Recipe Book", font: GUIStyle.SubHeadingFont, textColor: GUIStyle.TextColorBright)
            {
                CanBeFocused = false
            };

            // Строка заголовков таблицы
            var headerRow = new GUILayoutGroup(new RectTransform(new Vector2(1f, 0.045f), layout.RectTransform), isHorizontal: true, childAnchor: Anchor.CenterLeft)
            {
                Stretch = true,
                RelativeSpacing = 0.02f,
                AbsoluteSpacing = GUI.IntScale(4)
            };
            new GUITextBlock(new RectTransform(new Vector2(ResultColumnRatio, 1f), headerRow.RectTransform), "Result", font: GUIStyle.SmallFont, textColor: GUIStyle.TextColorDim)
            { CanBeFocused = false };
            new GUITextBlock(new RectTransform(new Vector2(1f - ResultColumnRatio - 0.02f, 1f), headerRow.RectTransform), "Ingredients", font: GUIStyle.SmallFont, textColor: GUIStyle.TextColorDim)
            { CanBeFocused = false };

            // Прокручиваемый список строк (таблица)
            var listRect = new RectTransform(new Vector2(1f, 0.88f), layout.RectTransform);
            _listBox = new GUIListBox(listRect, isHorizontal: false);
            _listBox.CurrentSelectMode = GUIListBox.SelectMode.None;
            RefreshList();
        }

        private static void RefreshList()
        {
            if (_listBox?.Content?.RectTransform == null || _recipes == null) return;

            _listBox.Content.ClearChildren();
            int index = 0;
            foreach (var entry in _recipes)
            {
                // Строка таблицы: чередующийся фон (Helmod-стиль)
                bool alternate = (index % 2) == 1;
                var rowBg = new GUIFrame(new RectTransform(new Vector2(1f, 0.055f), _listBox.Content.RectTransform)
                {
                    MinSize = new Point(0, GUI.IntScale(22))
                }, style: null, color: alternate ? HelmodRowAlt : Color.Transparent)
                {
                    CanBeFocused = false
                };

                var rowLayout = new GUILayoutGroup(new RectTransform(new Vector2(0.98f, 0.85f), rowBg.RectTransform, Anchor.CenterLeft), isHorizontal: true, childAnchor: Anchor.CenterLeft)
                {
                    Stretch = true,
                    RelativeSpacing = 0.02f,
                    AbsoluteSpacing = GUI.IntScale(4)
                };

                var resultBlock = new GUITextBlock(new RectTransform(new Vector2(ResultColumnRatio, 1f), rowLayout.RectTransform), entry.ResultDisplayName ?? "", font: GUIStyle.SmallFont, textColor: GUIStyle.Green)
                { CanBeFocused = false };
                string ingredientsStr = string.Join(", ", System.Linq.Enumerable.Select(entry.Ingredients, i => $"{i.Name} ×{i.Amount}"));
                var ingBlock = new GUITextBlock(new RectTransform(new Vector2(1f - ResultColumnRatio - 0.02f, 1f), rowLayout.RectTransform), ingredientsStr, font: GUIStyle.SmallFont, textColor: GUIStyle.TextColorNormal)
                { CanBeFocused = false };

                index++;
            }
        }
    }
}

#endif
