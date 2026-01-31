#if CLIENT

using System;
using System.Collections.Generic;
using System.Linq;
using Barotrauma;
using Microsoft.Xna.Framework;

namespace RecipeBook
{
    /// <summary>
    /// Helmod-style recipe list UI: search, toggle button + frame with scrollable list (result | ingredients).
    /// </summary>
    internal static class RecipeBookUI
    {
        private static IReadOnlyList<RecipeBookMod.RecipeEntry> _recipes;
        private static GUIFrame _rootFrame;
        private static GUIFrame _panelFrame;
        private static GUIButton _toggleButton;
        private static GUIListBox _listBox;
        private static GUITextBox _searchBox;

        /// <summary> UI создан и привязан к канвасу (кнопка и окно есть). У канваса нет GUIComponent, поэтому проверяем RectTransform.Parent. </summary>
        public static bool IsInitialized => _rootFrame != null && _rootFrame.RectTransform.Parent != null;

        /// <summary> Окно сборника рецептов открыто. </summary>
        public static bool IsPanelVisible => _panelFrame != null && _panelFrame.Visible;

        /// <summary> Закрыть окно (без переключения). </summary>
        public static void Close()
        {
            if (_panelFrame != null)
                _panelFrame.Visible = false;
        }

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

            // CreateToggleButton();  // TODO: Не требуется делать GUI так. Потом разбёмся 
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
                // GUICanvas хранит детей в weak refs, не в children — SetAsLastChild даёт ошибку, не вызываем для канваса
                if (!ReferenceEquals(_rootFrame.RectTransform.Parent, GUI.Canvas))
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
            _searchBox = null;
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
        // Три столбца: результат | ингредиенты | устройство (где крафтится)
        private const float ResultColumnRatio = 0.28f;
        private const float IngredientsColumnRatio = 0.48f;
        private const float DeviceColumnRatio = 0.22f;
        private const float ColumnSpacing = 0.01f;
        /// <summary> Доля колонки ингредиентов под кнопку «Состав ▾» (остальное — текст списка). </summary>
        private const float IngredientsDropdownButtonRatio = 0.18f;

        /// <summary> Размер иконки результата (квадрат, 16–20 px, SmallFont-совместимо). </summary>
        private const int ResultIconSize = 18;

        /// <summary> Кнопка, выглядящая как текст: по клику подставляет текст в поиск и обновляет список. </summary>
        private static GUIButton CreateSearchClickButton(RectTransform rect, string text, Color textColor, string searchValueOnClick)
        {
            var btn = new GUIButton(rect, text ?? "", Alignment.CenterLeft, style: null)
            {
                Color = Color.Transparent,
                HoverColor = new Color(1f, 1f, 1f, 0.12f),
                PressedColor = new Color(1f, 1f, 1f, 0.08f),
                TextColor = textColor,
                CanBeFocused = true
            };
            btn.Frame.Color = Color.Transparent;
            btn.Frame.HoverColor = btn.HoverColor;
            btn.Frame.PressedColor = btn.PressedColor;
            btn.Frame.CanBeFocused = false;
            if (btn.TextBlock != null) { btn.TextBlock.CanBeFocused = false; }
            string capture = searchValueOnClick ?? text ?? "";
            btn.OnClicked = (_, __) =>
            {
                if (_searchBox != null) { _searchBox.Text = capture; RefreshList(); }
                return true;
            };
            return btn;
        }

        /// <summary> Добавить иконку результата слева от текста внутри кнопки. Иконка — часть кликабельной зоны. Если prefab/sprite нет — ничего не добавляем. </summary>
        private static void AddResultIconToButton(GUIButton button, ItemPrefab resultPrefab)
        {
            if (resultPrefab == null) return;
            Sprite sprite = resultPrefab.InventoryIcon ?? resultPrefab.Sprite;
            if (sprite == null) return;
            Color iconColor = sprite == resultPrefab.Sprite ? resultPrefab.SpriteColor : resultPrefab.InventoryIconColor;
            int size = GUI.IntScale(ResultIconSize);
            var iconRect = new RectTransform(new Point(size, size), button.RectTransform, Anchor.CenterLeft)
            { AbsoluteOffset = new Point(GUI.IntScale(2), 0) };
            var img = new GUIImage(iconRect, sprite, scaleToFit: true)
            {
                Color = iconColor,
                HoverColor = iconColor,
                CanBeFocused = false
            };
            img.RectTransform.SetAsFirstChild();
            if (button.TextBlock != null)
            {
                int leftPad = size + GUI.IntScale(4);
                button.TextBlock.Padding = new Vector4(leftPad, button.TextBlock.Padding.Y, button.TextBlock.Padding.Z, button.TextBlock.Padding.W);
            }
        }

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
            var headerText =             new GUITextBlock(new RectTransform(new Vector2(0.96f, 0.85f), headerBg.RectTransform, Anchor.CenterLeft)
            {
                AbsoluteOffset = new Point(GUI.IntScale(8), 0)
            }, "Recipe Book", font: GUIStyle.SubHeadingFont, textColor: GUIStyle.TextColorBright)
            {
                CanBeFocused = false
            };

            // Поиск в стиле Helmod: строка фильтра над таблицей
            var searchRow = new GUILayoutGroup(new RectTransform(new Vector2(1f, 0.055f), layout.RectTransform), isHorizontal: true, childAnchor: Anchor.CenterLeft)
            {
                Stretch = true,
                RelativeSpacing = 0.02f,
                AbsoluteSpacing = GUI.IntScale(4)
            };
            new GUITextBlock(new RectTransform(new Vector2(0.12f, 1f), searchRow.RectTransform), "Search", font: GUIStyle.SmallFont, textColor: GUIStyle.TextColorDim)
            { CanBeFocused = false };
            _searchBox = new GUITextBox(new RectTransform(new Vector2(0.86f, 1f), searchRow.RectTransform), "", createClearButton: true)
            {
                OverflowClip = true
            };
            _searchBox.OnTextChanged += (tb, text) =>
            {
                RefreshList();
                return true;
            };

            // Строка заголовков таблицы (Result | Ingredients | Device)
            var headerRow = new GUILayoutGroup(new RectTransform(new Vector2(1f, 0.045f), layout.RectTransform), isHorizontal: true, childAnchor: Anchor.CenterLeft)
            {
                Stretch = true,
                RelativeSpacing = ColumnSpacing,
                AbsoluteSpacing = GUI.IntScale(4)
            };
            new GUITextBlock(new RectTransform(new Vector2(ResultColumnRatio, 1f), headerRow.RectTransform), "Result", font: GUIStyle.SmallFont, textColor: GUIStyle.TextColorDim)
            { CanBeFocused = false };
            new GUITextBlock(new RectTransform(new Vector2(IngredientsColumnRatio, 1f), headerRow.RectTransform), "Ingredients", font: GUIStyle.SmallFont, textColor: GUIStyle.TextColorDim)
            { CanBeFocused = false };
            new GUITextBlock(new RectTransform(new Vector2(DeviceColumnRatio, 1f), headerRow.RectTransform), "Device", font: GUIStyle.SmallFont, textColor: GUIStyle.TextColorDim)
            { CanBeFocused = false };

            // Прокручиваемый список строк (таблица)
            var listRect = new RectTransform(new Vector2(1f, 0.82f), layout.RectTransform);
            _listBox = new GUIListBox(listRect, isHorizontal: false);
            _listBox.CurrentSelectMode = GUIListBox.SelectMode.None;
            RefreshList();
        }

        /// <summary> Поиск по всему видимому: результат, ингредиенты, устройство. </summary>
        private static bool RecipeMatchesFilter(RecipeBookMod.RecipeEntry entry, string filter)
            => GetFilterMatchPriority(entry, filter) >= 0;

        /// <summary> Приоритет совпадения для сортировки: 0 = по названию/id результата, 1 = по ингредиентам, 2 = по Device, -1 = нет совпадения. </summary>
        private static int GetFilterMatchPriority(RecipeBookMod.RecipeEntry entry, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return 0;
            string f = filter.Trim().ToLowerInvariant();
            if (entry.ResultDisplayName?.ToLowerInvariant().Contains(f) == true) return 0;
            if (entry.ResultName?.ToLowerInvariant().Contains(f) == true) return 0;
            foreach (var ing in entry.Ingredients)
                if (ing.Name?.ToLowerInvariant().Contains(f) == true) return 1;
            if (entry.DeviceName?.ToLowerInvariant().Contains(f) == true) return 2;
            return -1;
        }

        private static void RefreshList()
        {
            if (_listBox?.Content?.RectTransform == null || _recipes == null) return;

            string filter = _searchBox?.Text?.Trim() ?? "";
            List<RecipeBookMod.RecipeEntry> filtered = _recipes
                .Where(entry => RecipeMatchesFilter(entry, filter))
                .OrderBy(entry => GetFilterMatchPriority(entry, filter))
                .ThenBy(entry => string.IsNullOrWhiteSpace(entry.ResultDisplayName ?? entry.ResultName) ? 1 : 0) // рецепты без названия — в конец
                .ThenBy(entry => (entry.ResultDisplayName ?? entry.ResultName ?? "").ToLowerInvariant())
                .ToList();

            _listBox.Content.ClearChildren();
            int index = 0;
            foreach (var entry in filtered)
            {
                // Строка таблицы: чередующийся фон, перенос текста (подготовка к кликабельным названиям)
                bool alternate = (index % 2) == 1;
                var rowBg = new GUIFrame(new RectTransform(new Vector2(1f, 0.09f), _listBox.Content.RectTransform)
                {
                    MinSize = new Point(0, GUI.IntScale(36))
                }, style: null, color: alternate ? HelmodRowAlt : Color.Transparent)
                {
                    CanBeFocused = false
                };

                var rowLayout = new GUILayoutGroup(new RectTransform(new Vector2(0.98f, 0.92f), rowBg.RectTransform, Anchor.CenterLeft), isHorizontal: true, childAnchor: Anchor.TopLeft)
                {
                    Stretch = true,
                    RelativeSpacing = ColumnSpacing,
                    AbsoluteSpacing = GUI.IntScale(4)
                };

                // Результат: [иконка] название — одна кликабельная зона, иконка только из ItemPrefab (результат)
                var resultBtn = CreateSearchClickButton(
                    new RectTransform(new Vector2(ResultColumnRatio, 1f), rowLayout.RectTransform),
                    entry.ResultDisplayName ?? "",
                    GUIStyle.Green,
                    entry.ResultDisplayName ?? entry.ResultName ?? "");
                resultBtn.Font = GUIStyle.SmallFont;
                if (resultBtn.TextBlock != null) resultBtn.TextBlock.Wrap = true;
                resultBtn.HoverColor = new Color(1f, 1f, 1f, 0.08f);
                resultBtn.Frame.HoverColor = resultBtn.HoverColor;
                AddResultIconToButton(resultBtn, entry.ResultItemPrefab);

                // Ингредиенты: один GUITextBlock (список с переносом) + кнопка «Состав ▾» только если ингредиентов > 1; по клику — выпадающее меню
                int ingCount = entry.Ingredients.Count;
                string ingredientsListText = string.Join(", ", entry.Ingredients.Select(ing => $"{ing.Name} ×{ing.Amount}"));
                float textRatio = ingCount > 1 ? (1f - IngredientsDropdownButtonRatio) : 1f;

                var ingredientsColumn = new GUILayoutGroup(new RectTransform(new Vector2(IngredientsColumnRatio, 1f), rowLayout.RectTransform), isHorizontal: true, childAnchor: Anchor.CenterLeft)
                {
                    Stretch = true,
                    RelativeSpacing = 0.02f,
                    AbsoluteSpacing = GUI.IntScale(4)
                };

                var textRect = new RectTransform(new Vector2(textRatio, 1f), ingredientsColumn.RectTransform);
                var ingredientsText = new GUITextBlock(textRect, ingredientsListText, font: GUIStyle.SmallFont, textColor: GUIStyle.TextColorNormal)
                {
                    Wrap = true,
                    CanBeFocused = false
                };

                if (ingCount > 1)
                {
                    var dropdownRect = new RectTransform(new Vector2(IngredientsDropdownButtonRatio, 1f), ingredientsColumn.RectTransform)
                    { MinSize = new Point(GUI.IntScale(52), 0) };
                    var dropdownBtn = new GUIButton(dropdownRect, "Состав ▾", Alignment.Center, style: null)
                    {
                        Color = Color.Transparent,
                        HoverColor = new Color(1f, 1f, 1f, 0.12f),
                        TextColor = GUIStyle.TextColorDim,
                        Font = GUIStyle.SmallFont,
                        ToolTip = "Показать ингредиенты"
                    };
                    dropdownBtn.Frame.Color = Color.Transparent;
                    dropdownBtn.Frame.HoverColor = dropdownBtn.HoverColor;

                    var ingredientsForMenu = entry.Ingredients.ToList();
                    dropdownBtn.OnClicked = (_, __) =>
                    {
                        var options = ingredientsForMenu.Select(ing =>
                        {
                            string name = ing.Name ?? "";
                            string label = $"{ing.Name} ×{ing.Amount}";
                            return new ContextMenuOption(label, isEnabled: true, onSelected: () =>
                            {
                                if (_searchBox != null) { _searchBox.Text = name; RefreshList(); }
                            });
                        }).ToArray();
                        GUIContextMenu.CreateContextMenu(PlayerInput.MousePosition, "", null, options);
                        return true;
                    };
                }

                // Устройство — клик подставляет название устройства в поиск
                var deviceBtn = CreateSearchClickButton(
                    new RectTransform(new Vector2(DeviceColumnRatio, 1f), rowLayout.RectTransform),
                    entry.DeviceName ?? "",
                    GUIStyle.TextColorDim,
                    entry.DeviceName ?? "");
                deviceBtn.Font = GUIStyle.SmallFont;
                deviceBtn.RectTransform.MinSize = new Point(GUI.IntScale(50), 0);

                index++;
            }
        }
    }
}

#endif
