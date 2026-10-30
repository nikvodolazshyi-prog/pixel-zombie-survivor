using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PixelZombieSurvivor.Editor
{
    /// <summary>
    /// Creates ready-to-click shortcuts for the built Windows player so the game can be
    /// started without opening Unity. Uses the Windows Script Host COM object, therefore
    /// it only does something on Windows editor machines.
    /// </summary>
    public static class ShortcutCreator
    {
        public const string ShortcutName = "Pixel Zombie Survivor";

        private const BindingFlags InvokeFlags = BindingFlags.InvokeMethod;

        [MenuItem("Tools/Pixel Zombie Survivor/Create Game Shortcuts", priority = 40)]
        public static void CreateFromMenu()
        {
            int created = CreateShortcuts(out string message);
            if (created > 0)
            {
                EditorUtility.RevealInFinder(GetExecutablePath());
            }

            EditorUtility.DisplayDialog("Pixel Zombie Survivor", message, "ОК");
        }

        /// <summary>
        /// Creates desktop, Start Menu and project-folder shortcuts. Safe to call from
        /// -executeMethod after PlayerBuilder.BuildWindows64.
        /// </summary>
        public static void CreateShortcutsFromCommandLine()
        {
            int created = CreateShortcuts(out string message);
            Debug.Log($"[Pixel Zombie Survivor] Shortcuts created: {created}. {message}");
        }

        public static int CreateShortcuts(out string message)
        {
            string executable = GetExecutablePath();
            if (!File.Exists(executable))
            {
                message = $"Сборка не найдена:\n{executable}\n\n" +
                          "Сначала выполните Tools > Pixel Zombie Survivor > Build Windows Player.";
                Debug.LogWarning("[Pixel Zombie Survivor] " + message);
                return 0;
            }

#if !UNITY_EDITOR_WIN
            message = "Ярлыки создаются только на Windows.";
            Debug.LogWarning("[Pixel Zombie Survivor] " + message);
            return 0;
#else
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string[] targets =
            {
                Path.Combine(GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ShortcutName + ".lnk"),
                Path.Combine(GetFolderPath(Environment.SpecialFolder.Programs), ShortcutName + ".lnk"),
                Path.Combine(projectRoot, ShortcutName + ".lnk"),
                Path.Combine(Path.GetDirectoryName(executable) ?? projectRoot, "Играть.lnk")
            };

            int created = 0;
            foreach (string target in targets)
            {
                if (string.IsNullOrEmpty(target))
                {
                    continue;
                }

                if (TryCreateShortcut(target, executable))
                {
                    created++;
                    Debug.Log($"[Pixel Zombie Survivor] Shortcut: {target}");
                }
            }

            message = created > 0
                ? $"Готово: создано ярлыков — {created}.\n\n" +
                  "• рабочий стол\n• меню «Пуск»\n• папка проекта\n• папка сборки\n\n" +
                  $"Запускают: {executable}"
                : "Не удалось создать ярлыки. Подробности в Console.";
            return created;
#endif
        }

        public static string GetExecutablePath()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            return Path.GetFullPath(Path.Combine(projectRoot, PlayerBuilder.DefaultBuildFolder, PlayerBuilder.ExecutableName));
        }

        private static string GetFolderPath(Environment.SpecialFolder folder)
        {
            try
            {
                return Environment.GetFolderPath(folder);
            }
            catch
            {
                return string.Empty;
            }
        }

#if UNITY_EDITOR_WIN
        private static bool TryCreateShortcut(string linkPath, string executable)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                {
                    Debug.LogError("[Pixel Zombie Survivor] WScript.Shell is unavailable on this machine.");
                    return false;
                }

                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember(
                    "CreateShortcut",
                    InvokeFlags,
                    null,
                    shell,
                    new object[] { linkPath });

                if (shortcut == null)
                {
                    return false;
                }

                Type shortcutType = shortcut.GetType();
                SetProperty(shortcutType, shortcut, "TargetPath", executable);
                SetProperty(shortcutType, shortcut, "WorkingDirectory", Path.GetDirectoryName(executable));
                SetProperty(shortcutType, shortcut, "IconLocation", executable + ",0");
                SetProperty(shortcutType, shortcut, "Description", "Pixel Zombie Survivor — запуск игры");
                SetProperty(shortcutType, shortcut, "WindowStyle", 1);

                shortcutType.InvokeMember("Save", InvokeFlags, null, shortcut, null);
                return File.Exists(linkPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Pixel Zombie Survivor] Не удалось создать ярлык «{linkPath}»: {exception.Message}");
                return false;
            }
        }

        private static void SetProperty(Type type, object instance, string name, object value)
        {
            try
            {
                type.InvokeMember(name, BindingFlags.SetProperty, null, instance, new[] { value });
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Pixel Zombie Survivor] Shortcut property '{name}' was skipped: {exception.Message}");
            }
        }
#endif
    }
}
