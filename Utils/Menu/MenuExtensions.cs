using System;
using System.Windows.Forms;
using Rage;
using Rage.Native;
using RAGENativeUI.Elements;

namespace ReportsPlus.Utils.Menu{
    internal static class MenuExtensions{
        /// <summary>
        ///     Allows to input a new integer value by selecting the item.
        /// </summary>
        /// <param name="item">The numeric scroller item.</param>
        /// <param name="maxLength">Max characters for input.</param>
        /// <returns>The modified UIMenuNumericScrollerItem.</returns>
        public static void WithTextEditing(this UIMenuNumericScrollerItem<int> item, int maxLength = 32)
        {
            WithTextEditing(item, maxLength, int.TryParse, "integer number");
        }

        /// <summary>
        ///     Enables text editing on a menu item using the on-screen keyboard, with options for label truncation.
        /// </summary>
        /// <param name="item">The menu item to extend.</param>
        /// <param name="getter">Function to retrieve the current text value.</param>
        /// <param name="setter">Action to save the new text value.</param>
        /// <param name="maxLengthInItem">Maximum character length to display in the menu item's RightLabel.</param>
        /// <param name="maxLength">Maximum character length allowed in the on-screen keyboard input.</param>
        public static void WithTextEditing(this UIMenuItem item, Func<string> getter, Action<string> setter, int maxLengthInItem = 16, int maxLength = 32)
        {
            if (getter == null) throw new ArgumentNullException(nameof(getter));
            if (setter == null) throw new ArgumentNullException(nameof(setter));

            WithTextEditingBase(item, maxLength, getter, str =>
            {
                TrimAndSetRightLabel(item, str, maxLengthInItem);
                setter(str);
            });

            TrimAndSetRightLabel(item, getter(), maxLengthInItem);

            static void TrimAndSetRightLabel(UIMenuItem item, string str, int maxLength)
            {
                item.RightLabel = str.Length > maxLength ? str.Substring(0, maxLength) + "..." : str;
            }
        }

        /// <summary>
        ///     Enables key binding configuration on a menu item using text input.
        /// </summary>
        /// <param name="item">The menu item to extend.</param>
        /// <param name="getter">Function to retrieve the current key.</param>
        /// <param name="setter">Action to save the new key.</param>
        public static void WithKeyEditing(this UIMenuItem item, Func<Keys> getter, Action<Keys> setter)
        {
            item.RightLabel = getter().ToString();

            WithTextEditingBase(item, 20, () => getter().ToString(), str =>
            {
                if (Enum.TryParse(str, true, out Keys newKey))
                {
                    setter(newKey);
                    item.RightLabel = getter().ToString();
                }
                else
                {
                    Game.DisplayNotification($"~r~Invalid Key~s~: '{str}' is not a valid key name.");
                }
            });
        }

        /// <summary>
        ///     Helper method to attach text editing logic to a numeric scroller, including bounds checking.
        /// </summary>
        /// <typeparam name="T">The numeric type of the scroller.</typeparam>
        /// <param name="item">The numeric scroller item.</param>
        /// <param name="maxLength">Maximum character length for input.</param>
        /// <param name="tryParse">Delegate to parse the string input into the target numeric type.</param>
        /// <param name="name">Display name of the type for error notifications.</param>
        private static void WithTextEditing<T>(this UIMenuNumericScrollerItem<T> item, int maxLength, TryParseDelegate<T> tryParse, string name) where T : struct, IComparable, IFormattable, IConvertible, IComparable<T>, IEquatable<T>
        {
            WithTextEditingBase(item, maxLength, () => item.OptionText, str =>
            {
                if (tryParse(str, out var newValue))
                {
                    if (newValue.CompareTo(item.Maximum) > 0)
                        NotifyIncorrectInput($"the maximum value is {item.Formatter(item.Maximum)}.");
                    else if (newValue.CompareTo(item.Minimum) < 0)
                        NotifyIncorrectInput($"the minimum value is {item.Formatter(item.Minimum)}.");
                    else
                        item.Value = newValue;
                }
                else
                {
                    NotifyIncorrectInput($"'{str}' is not a valid {name}.");
                }

                return;

                static void NotifyIncorrectInput(string msg)
                {
                    Game.DisplayNotification($"~r~Incorrect input~s~: {msg}");
                }
            });
        }

        /// <summary>
        ///     Base method handling the on-screen keyboard lifecycle, thread blocking, and result callback.
        /// </summary>
        /// <typeparam name="T">The type of the UIMenuItem.</typeparam>
        /// <param name="item">The item to attach the activation event to.</param>
        /// <param name="maxLength">Maximum character length for input.</param>
        /// <param name="strGetter">Function to provide the initial text for the keyboard.</param>
        /// <param name="resultCallback">Action to execute with the user's input if successful.</param>
        private static void WithTextEditingBase<T>(T item, int maxLength, Func<string> strGetter, Action<string> resultCallback) where T : UIMenuItem
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (maxLength < 0) throw new ArgumentOutOfRangeException(nameof(maxLength), "Length cannot be negative");

            item.Activated += (m, s) =>
            {
                try
                {
                    Main.IsKeyboardOpen = true;

                    Main.Pool.Draw();

                    NativeFunction.Natives.DISPLAY_ONSCREEN_KEYBOARD(6, "", "", strGetter(), "", "", "", maxLength);
                    int state;
                    while ((state = NativeFunction.Natives.UPDATE_ONSCREEN_KEYBOARD<int>()) == 0)
                    {
                        GameFiber.Yield();
                        Main.Pool.Draw();
                    }

                    if (state != 1) return;
                    string str = NativeFunction.Natives.GET_ONSCREEN_KEYBOARD_RESULT<string>();
                    resultCallback(str);
                }
                finally
                {
                    Main.IsKeyboardOpen = false;
                }
            };
        }

        /// <summary>
        ///     Delegate definition for parsing a string into a generic type.
        /// </summary>
        /// <typeparam name="T">The target type to parse into.</typeparam>
        /// <param name="s">The input string.</param>
        /// <param name="result">The parsed result output.</param>
        /// <returns>True if parsing succeeded, otherwise false.</returns>
        private delegate bool TryParseDelegate<T>(string s, out T result);
    }
}