﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace VRWorldToolkit
{
    public class Helper
    {
        public static string ReturnPlural(int counter)
        {
            return counter > 1 ? "s" : "";
        }

        public static bool CheckNameSpace(string namespace_name)
        {
            bool namespaceFound = (from assembly in AppDomain.CurrentDomain.GetAssemblies()
                                   from type in assembly.GetTypes()
                                   where type.Namespace == namespace_name
                                   select type).Any();
            return namespaceFound;
        }

        public static float GetBrightness(Color color)
        {
            float num = ((float)color.r);
            float num2 = ((float)color.g);
            float num3 = ((float)color.b);
            float num4 = num;
            float num5 = num;
            if (num2 > num4)
                num4 = num2;
            if (num3 > num4)
                num4 = num3;
            if (num2 < num5)
                num5 = num2;
            if (num3 < num5)
                num5 = num3;
            return (num4 + num5) / 2;
        }

        public static string Truncate(string text, int length)
        {
            if (text.Length > length)
            {
                text = text.Substring(0, length);
                text += "...";
            }
            return text;
        }

        public static string GetAllLayersFromMask(LayerMask layerMask)
        {
            List<string> layers = new List<string>();
            for (int i = 0; i < 32; i++)
            {
                if (layerMask == (layerMask | (1 << i)))
                {
                    layers.Add(LayerMask.LayerToName(i));
                }
            }
            return String.Join(", ", layers.ToArray());
        }

        public static bool LayerIncludedInMask(int layer, LayerMask layermask)
        {
            return layermask == (layermask | (1 << layer));
        }

        public static string FormatTime(System.TimeSpan t)
        {
            string formattedTime = "";
            if (t.TotalDays > 1)
            {
                formattedTime = String.Concat(formattedTime, t.Days + " days ");
            }
            if (t.TotalHours > 1)
            {
                formattedTime = String.Concat(formattedTime, t.Hours + " days ");
            }
            if (t.TotalMinutes > 1)
            {
                formattedTime = String.Concat(formattedTime, t.Minutes + " minutes ");
            }
            else
            {
                formattedTime = String.Concat(formattedTime, t.Seconds + " seconds");
            }

            return formattedTime;
        }

        public static RuntimePlatform BuildPlatform()
        {
#if UNITY_ANDROID
            return RuntimePlatform.Android;
#else
            return RuntimePlatform.WindowsPlayer;
#endif
        }

        public static Type GetTypeFromName(string typeName)
        {
            var type = Type.GetType(typeName);
            if (type != null) return type; foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = a.GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }
            return null;
        }

        public static string GetSteamVRCExecutablePath()
        {
            RegistryKey steamKey = Registry.LocalMachine.OpenSubKey("Software\\Valve\\Steam") ?? Registry.LocalMachine.OpenSubKey("Software\\Wow6432Node\\Valve\\Steam");

            if (steamKey != null)
            {
                string commonPath = "\\SteamApps\\common";
                string executablePath = "\\VRChat.exe";

                var steamPath = (string)steamKey.GetValue("InstallPath");

                var configFile = Path.Combine(steamPath, "config", "config.vdf");

                Regex regex = new Regex("(?<=BaseInstallFolder.*\".+?\").+?(?=\")");

                List<string> folders = new List<string>();

                folders.Add(steamPath + commonPath);

                string configText = File.ReadAllText(configFile);

                folders.AddRange(Regex.Matches(configText, "(?<=BaseInstallFolder.*\".+?\").+?(?=\")").Cast<Match>().Select(x => x.Value + commonPath));

                foreach (var folder in folders)
                {
                    try
                    {
                        var matches = Directory.GetDirectories(folder, "VRChat");
                        if (matches.Length >= 1)
                        {
                            string finalPath = matches[0] + executablePath;

                            if (File.Exists(finalPath)) return finalPath;
                        }
                    }
                    catch (DirectoryNotFoundException)
                    {
                        continue;
                    }
                }
            }

            return null;
        }
    }
}