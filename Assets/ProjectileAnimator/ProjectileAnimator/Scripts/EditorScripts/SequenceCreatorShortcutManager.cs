using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace SwarmSequencer
{
    namespace EditorTools
    {
        public static class SequenceCreatorShortcutManager
        {
            public static SequenceCreator activeSequenceCreator;

            [Shortcut("SequenceCreator/SelectNextFrame", KeyCode.D)]
            public static void SelectNextFrame()
            {
                if (activeSequenceCreator == null) return;
                activeSequenceCreator.SelectFrame(activeSequenceCreator.SelectedFrame + 1);
            }
            [Shortcut("SequenceCreator/SelectPreviousFrame", KeyCode.A)]
            public static void SelectPreviousFrame()
            {
                if (activeSequenceCreator == null) return;
                activeSequenceCreator.SelectFrame(activeSequenceCreator.SelectedFrame - 1);
            }

            [Shortcut("SequenceCreator/SelectEditPositionMode", KeyCode.Alpha1, ShortcutModifiers.Alt)]
            public static void SelectEditPositionMode()
            {
                if (activeSequenceCreator == null) return;
                activeSequenceCreator.SelectModificationMode(SequenceCreator.ModificationMode.Position);
            }

            [Shortcut("SequenceCreator/SelectEditBezierMode", KeyCode.Alpha2, ShortcutModifiers.Alt)]
            public static void SelectEditBezierMode()
            {
                Debug.Log("selecting bezier mode");
                if (activeSequenceCreator == null) return;
                activeSequenceCreator.SelectModificationMode(SequenceCreator.ModificationMode.Bezier);
            }

            [Shortcut("SequenceCreator/SelectEraserMode", KeyCode.Alpha3, ShortcutModifiers.Alt)]
            public static void SelectEraserMode()
            {
                if (activeSequenceCreator == null) return;
                activeSequenceCreator.SelectModificationMode(SequenceCreator.ModificationMode.Eraser);
            }
        }
    }
}

