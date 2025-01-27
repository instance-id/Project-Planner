﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using LanguageData = ProjectPlanner.Language;

namespace ProjectPlanner
{
    [ExecuteInEditMode]
    [InitializeOnLoad]
    public static class FileManager
    {
        const string LocatorSearch = "t:ProjectPlannerLocator ProjectPlannerLocator";
        private static ProjectPlannerLocator locator;

        private static string RootFolder
        {
            get
            {
                // -- The key used to save the root folder location.
                string key = "root_folder";

                // -- Get the last location we found the locator file in.
                string path = Prefs.GetString(key);
                // if (!string.IsNullOrEmpty(path)) return path;

                // -- If locator is null, attempt to load it from last known location.
                // -- If it does not exist there, attempt to search the project for it.
                // -- If it is still not found, create a new ProjectPlannerLocator.
                if (!locator)
                {
                    if (!string.IsNullOrEmpty(path))
                        locator = AssetDatabase.LoadAssetAtPath<ProjectPlannerLocator>(path);

                    if (!locator) locator = LocatorSearch.FindAsset<ProjectPlannerLocator>();
                    if (!locator) CreateDefaultLocatorFile();
                }

                if (path == locator.AssetPath) return path;

                path = locator.AssetPath;
                Prefs.SetString(key, path);
                return path;
            }
        }

        private static readonly string locatorFileName = "ProjectPlannerLocator.asset";

        // -- Path.Combine automatically uses the correct path separator cross-platform
        public static readonly string DataFolder = Path.Combine(RootFolder, "Editor", "Data");
        private static readonly string IconsFolder = Path.Combine(locator.IsPackage ? locator.PackagePath : RootFolder, "Editor", "Icons");
        public static readonly string Manual = Path.Combine(locator.IsPackage ? locator.PackagePath : RootFolder, "Manual.pdf");

        public static string ProjectFolderPath
        {
            get
            {
                string path = Path.Combine(Environment.CurrentDirectory, "Project Planner");

                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                return path;
            }
        }

        public static string JsonFile => Path.Combine(ProjectFolderPath, "Data.json");

        public static string ScreenshotsFolder(string taskId)
        {
            string path = Path.Combine(ProjectFolderPath, "Screenshots", taskId);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            return path;
        }

        private static Dictionary<string, FileBase> files;
        private static Dictionary<string, Texture2D> screenshots;

        static FileManager()
        {
            LoadAllDataFromJson();
            EditorApplication.quitting += EditorApplication_quitting;
        }

        private static void EditorApplication_quitting()
        {
            ClearSceenshotTmp();
        }

        private static void ValidateFolder(string folder, bool isPackage = false)
        {
            try
            {
                int place = Application.dataPath.LastIndexOf("Assets");

                string fullPath = Application.dataPath.Remove(place, "Assets".Length).Insert(place, folder);
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                    AssetDatabase.Refresh();
                }
            }
            catch (Exception) { }
        }

        public static void CreateDefaultLocatorFile()
        {
            string filePath = PathTools.IsPackage 
                ? Path.Combine(PathTools.Path, PathTools.ProjectName, locatorFileName) 
                : Path.Combine(PathTools.Path, locatorFileName);

            if (filePath.StartsWith(Application.dataPath))
                filePath = "Assets/" + filePath.Substring(Application.dataPath.Length);

            locator = AssetTools.CreateAssetOfType<ProjectPlannerLocator>(locatorFileName, filePath);
        }

        public static T GetFile<T>(string name) where T : FileBase
        {
            if (files == null)
                files = new Dictionary<string, FileBase>();

            string filePath = Path.Combine(DataFolder, name + ".asset");
            T file = null;
            if (files.ContainsKey(filePath))
                file = (T) files[filePath];

            if (file == null)
            {
                ValidateFolder(Path.GetDirectoryName(filePath));
                file = (T) AssetDatabase.LoadAssetAtPath(filePath, typeof(T));

                bool makeNew = false;
                if (file == null)
                {
                    FixMissingScriptReference<T>(name);

                    file = (T) AssetDatabase.LoadAssetAtPath(filePath, typeof(T));
                    if (file == null)
                        makeNew = true;
                }

                if (makeNew)
                {
                    file = ScriptableObject.CreateInstance<T>();
                    file.Setup();

                    AssetDatabase.CreateAsset(file, filePath);
                }

                if (files.ContainsKey(filePath))
                    files[filePath] = file;
                else
                    files.Add(filePath, file);
            }

            return file;
        }

        public static Texture2D GetScreenshotTexture2D(Screenshot screenshot)
        {
            if (screenshots == null)
                screenshots = new Dictionary<string, Texture2D>();

            Texture2D texture2D = null;

            if (screenshot != null && !string.IsNullOrEmpty(screenshot.TaskID))
            {
                Task task = Tasks.I.Get(screenshot.TaskID);
                if (task != null)
                {
                    if (screenshots.ContainsKey(screenshot.ID))
                    {
                        texture2D = screenshots[screenshot.ID];

                        if (texture2D == null)
                        {
                            screenshots.Remove(screenshot.ID);
                        }
                    }
                    else
                    {
                        string filePath = Path.Combine(ScreenshotsFolder(task.ID), screenshot.ID + ".png");
                        if (File.Exists(filePath))
                        {
                            texture2D = new Texture2D(1, 1);
                            texture2D.LoadImage(File.ReadAllBytes(filePath));
                            texture2D.Apply();

                            screenshots.Add(screenshot.ID, texture2D);
                        }
                    }
                }
            }

            return texture2D;
        }

        public static void DeleteSceenshotTexture2D(Screenshot screenshot)
        {
            if (screenshot != null)
            {
                Task task = Tasks.I.Get(screenshot.TaskID);
                if (task != null)
                {
                    string filePath = Path.Combine(ScreenshotsFolder(task.ID), screenshot.ID + ".png");

                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);

                        if (Directory.GetFiles(ScreenshotsFolder(task.ID)).Length == 0)
                        {
                            Directory.Delete(ScreenshotsFolder(task.ID));
                        }

                        if (screenshots == null)
                            screenshots = new Dictionary<string, Texture2D>();
                        if (screenshots.ContainsKey(screenshot.ID))
                        {
                            screenshots.Remove(screenshot.ID);
                        }
                    }
                }
            }
        }

        public static void MoveSceenshotToTmp(Screenshot screenshot)
        {
            if (screenshot != null)
            {
                Task task = Tasks.I.Get(screenshot.TaskID);
                if (task != null)
                {
                    string filePath = Path.Combine(ScreenshotsFolder(task.ID), screenshot.ID + ".png");

                    if (File.Exists(filePath))
                    {
                        string tmpFolder = ScreenshotsFolder("tmp");
                        if (!Directory.Exists(tmpFolder))
                        {
                            Directory.CreateDirectory(tmpFolder);
                        }

                        string tmpFlePath = Path.Combine(ScreenshotsFolder("tmp"), screenshot.ID + ".png");
                        File.Move(filePath, tmpFlePath);

                        if (screenshots == null)
                            screenshots = new Dictionary<string, Texture2D>();
                        if (screenshots.ContainsKey(screenshot.ID))
                        {
                            screenshots.Remove(screenshot.ID);
                        }
                    }
                }
            }
            //FileManager.DeleteSceenshotTexture2D(screenshot);
        }

        public static void MoveSceenshotToCopy(Screenshot screenshot, string newId)
        {
            if (screenshot != null)
            {
                Task task = Tasks.I.Get(screenshot.TaskID);
                if (task != null)
                {
                    string filePath = Path.Combine(ScreenshotsFolder(task.ID), screenshot.ID + ".png");

                    if (File.Exists(filePath))
                    {
                        string copyFolder = ScreenshotsFolder("copy");
                        if (!Directory.Exists(copyFolder))
                        {
                            Directory.CreateDirectory(copyFolder);
                        }

                        string copyFlePath = Path.Combine(ScreenshotsFolder("copy"), newId + ".png");
                        File.Copy(filePath, copyFlePath);
                    }
                }
            }
            //FileManager.DeleteSceenshotTexture2D(screenshot);
        }

        public static void MoveSceenshotFromCopy(Screenshot copyScreenshot, Screenshot newScreenshot)
        {
            if (copyScreenshot != null && newScreenshot != null)
            {
                Task task = Tasks.I.Get(newScreenshot.TaskID);
                if (task != null)
                {
                    string copyFilePath = Path.Combine(ScreenshotsFolder("copy"), copyScreenshot.ID + ".png");

                    if (File.Exists(copyFilePath))
                    {
                        string taskFolder = ScreenshotsFolder(task.ID);
                        if (!Directory.Exists(taskFolder))
                        {
                            Directory.CreateDirectory(taskFolder);
                        }

                        string filePath = Path.Combine(ScreenshotsFolder(task.ID), newScreenshot.ID + ".png");
                        File.Copy(copyFilePath, filePath);
                    }
                }
            }
            //FileManager.DeleteSceenshotTexture2D(screenshot);
        }

        private static void ClearSceenshotTmp()
        {
            foreach (var folder in Directory.GetDirectories(Path.Combine(ProjectFolderPath, "Screenshots")))
            {
                foreach (var file in Directory.GetFiles(folder))
                {
                    if (Screenshots.I.All.Count(o => o.ID == Path.GetFileNameWithoutExtension(file)) == 0)
                    {
                        File.Delete(file);
                    }
                }

                if (Directory.GetFiles(folder).Length == 0)
                {
                    Directory.Delete(folder);
                }
            }
        }

        public static void FixMissingScriptReference<T>(string name) where T : FileBase
        {
            //DLL 2017.1+
            UnityEngine.Object[] scripts = AssetDatabase.LoadAllAssetRepresentationsAtPath(Path.Combine(RootFolder, "Editor", "Project Planner.dll"));

            foreach (UnityEngine.Object script in scripts)
            {
                if (script.name == name)
                {
                    string assetFilePath = Path.Combine(DataFolder, script.name + ".asset");
                    string assetFilePathMeta = Path.Combine(DataFolder, script.name + ".asset.meta");
                    if (File.Exists(assetFilePath) && File.Exists(assetFilePathMeta))
                    {
                        string[] metaInfo = File.ReadAllLines(assetFilePathMeta);
                        if (metaInfo[0] == "fileFormatVersion: 2")
                        {
                            //DLL
                            string guid;
#if UNITY_2018_2_OR_NEWER
                            long file;
#else
                            int file;
#endif
                            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out guid, out file))
                            {
                                string[] assetLines = File.ReadAllLines(assetFilePath);
                                for (int i = 0; i < assetLines.Length; i++)
                                {
                                    if (assetLines[i].Contains("m_Script"))
                                    {
                                        assetLines[i] = "  m_Script: {fileID: " + file + ", guid: " + guid + ", type: 3}";
                                    }
                                }

                                File.Delete(assetFilePath);
                                File.Delete(assetFilePathMeta);
                                AssetDatabase.SaveAssets();
                                AssetDatabase.Refresh();
                                File.WriteAllLines(assetFilePath, assetLines);
                                AssetDatabase.SaveAssets();
                                AssetDatabase.Refresh();
                            }
                        }
                    }
                }
            }
        }

        public static string GetFileContent(string name)
        {
            string filePath = Path.Combine(DataFolder, name + ".asset");

            if (File.Exists(filePath))
            {
                return File.ReadAllText(filePath);
            }
            else
            {
                return "";
            }
        }

        public static void SaveAll(bool hard = false)
        {
            foreach (var file in files)
            {
                if (file.Value != null)
                {
                    EditorUtility.SetDirty(file.Value);
                    if (hard)
                        AssetDatabase.SaveAssets();
                }
            }

            SaveAllDataToJson();
        }

        public static void WriteLog(string name, string data)
        {
            string assetsFolder = Application.dataPath;
            string logFolder = assetsFolder.Substring(0, assetsFolder.Length - "assets".Length) + "Logs/";

            string filePath = logFolder + name + ".zrlog";

            if (!Directory.Exists(Path.GetDirectoryName(filePath)))
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

            if (!File.Exists(logFolder + ".gitignore"))
            {
                using (var sw = File.CreateText(logFolder + ".gitignore"))
                {
                    sw.WriteLine("*.zrlog");
                }
            }

            using (var sw = File.CreateText(filePath))
            {
                sw.WriteLine(data);
            }
        }

        private static Dictionary<string, Texture2D> icons;

        public static Texture2D GetIcon(string name)
        {
            if (icons == null)
                icons = new Dictionary<string, Texture2D>();

            string filePath = null;

            filePath = Path.Combine(IconsFolder, name + ".png");

            Texture2D icon = null;
            if (icons.ContainsKey(filePath))
                icon = icons[filePath];

            if (icon == null)
            {
                if (!locator.IsPackage) ValidateFolder(Path.GetDirectoryName(filePath));
                icon = (Texture2D) AssetDatabase.LoadAssetAtPath(filePath, typeof(Texture2D));

                if (icons.ContainsKey(filePath))
                    icons[filePath] = icon;
                else
                    icons.Add(filePath, icon);
            }

            return icon;
        }

        public static void SaveAllDataToJson()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("00").Append(EditorJsonUtility.ToJson(Boards.I, false)).Append(Environment.NewLine);
            builder.Append("01").Append(EditorJsonUtility.ToJson(Groups.I, false)).Append(Environment.NewLine);
            builder.Append("02").Append(EditorJsonUtility.ToJson(Tasks.I, false)).Append(Environment.NewLine);
            builder.Append("03").Append(EditorJsonUtility.ToJson(Tags.I, false)).Append(Environment.NewLine);
            builder.Append("04").Append(EditorJsonUtility.ToJson(Colors.I, false)).Append(Environment.NewLine);
            builder.Append("05").Append(EditorJsonUtility.ToJson(Screenshots.I, false));

            File.WriteAllText(JsonFile, builder.ToString());
        }

        public static void LoadAllDataFromJson()
        {
            if (File.Exists(JsonFile))
            {
                string[] data = File.ReadAllLines(JsonFile);
                foreach (var line in data)
                {
                    switch (line[0].ToString() + line[1])
                    {
                        case "00":
                            EditorJsonUtility.FromJsonOverwrite(line.Substring(2), Boards.I);
                            break;
                        case "01":
                            EditorJsonUtility.FromJsonOverwrite(line.Substring(2), Groups.I);
                            break;
                        case "02":
                            EditorJsonUtility.FromJsonOverwrite(line.Substring(2), Tasks.I);
                            break;
                        case "03":
                            EditorJsonUtility.FromJsonOverwrite(line.Substring(2), Tags.I);
                            break;
                        case "04":
                            EditorJsonUtility.FromJsonOverwrite(line.Substring(2), Colors.I);
                            break;
                        case "05":
                            EditorJsonUtility.FromJsonOverwrite(line.Substring(2), Screenshots.I);
                            break;
                    }
                }
            }
        }

        private static int CountLines(string s)
        {
            int n = 0;
            foreach (var c in s)
            {
                if (c == '\n') n++;
            }

            return n + 1;
        }

        public static class Language
        {
            private static readonly string LanguageFolder = Path.Combine(RootFolder, "Editor", "Languages");
            private static readonly string DefaultLanguageFile = Path.Combine(LanguageFolder, "English.asset");

            private static readonly Dictionary<string, LanguageData> languageFiles = new Dictionary<string, LanguageData>();

            public static LanguageData Get(string name)
            {
                if (languageFiles.ContainsKey(name))
                {
                    if (languageFiles[name] == null)
                    {
                        languageFiles.Remove(name);
                        return null;
                    }
                    else
                    {
                        return languageFiles[name];
                    }
                }
                else
                {
                    ValidateFolder(LanguageFolder);

                    LanguageData language = (LanguageData) AssetDatabase.LoadAssetAtPath(Path.Combine(LanguageFolder, name + ".asset"), typeof(LanguageData));

                    if (language != null)
                    {
                        languageFiles.Add(name, language);
                        return language;
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            public static LanguageData Default()
            {
                ValidateFolder(LanguageFolder);

                LanguageData language = (LanguageData) AssetDatabase.LoadAssetAtPath(DefaultLanguageFile, typeof(LanguageData));

                if (language == null)
                {
                    language = ScriptableObject.CreateInstance<LanguageData>();

                    AssetDatabase.CreateAsset(language, DefaultLanguageFile);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }

                return language;
            }

            public static List<string> LanguageNames()
            {
                //remove the last \ from the folder path
                //the reason we do this is because AssetDatabase.FindAssets() can not handle a path ending with \
                string folder = LanguageFolder.Remove(LanguageFolder.Length - 1);

                ValidateFolder(folder);

                List<string> filesNames = new List<string>();

                foreach (var guid in AssetDatabase.FindAssets(string.Format("t:{0}", typeof(LanguageData)), new string[] {folder}))
                {
                    filesNames.Add(Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)));
                }

                return filesNames;
            }
        }
    }

    public class FileManagerAssetModificationProcessor : UnityEditor.AssetModificationProcessor
    {
        static string[] OnWillSaveAssets(string[] paths)
        {
            foreach (string path in paths)
            {
                if (path.Contains(".unity"))
                {
                    FileManager.SaveAllDataToJson();
                    return paths;
                }
            }

            return paths;
        }
    }
}
