using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ProjectPlanner
{
    // --------------------------------------------- Asset Tools
    // ---------------------------------------------------------
    public static class AssetTools
    {
        internal static T FindAsset<T>(this string searchString) where T : Object
        {
            return AssetDatabase.FindAssets(searchString)
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .ToList().FirstOrDefault();
        }

        public static T CreateAssetOfType<T>(string name, string path) where T : ScriptableObject
        {
            T asset = null;
            if (!Directory.Exists(Path.GetDirectoryName(path)))
                Directory.CreateDirectory(Path.GetDirectoryName(path));

#if UNITY_EDITOR
            asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            AssetDatabase.CreateAsset(asset, path);
#endif
            return asset;
        }
    }

    // ---------------------------------------------- Path Tools
    // ---------------------------------------------------------
    public static class PathTools
    {
        public const string ProjectName = "Project Planner";
        const string AsmdefPath = "t:ASMDEF ProjectPlanner.Editor";
        const string PackageName = "id.instance.projectplanner";
        const string AsmdefQuery = "a:all t:ASMDEF ProjectPlanner.Editor";


        public static bool IsPackage
        {
            get
            {
                string asmdefPath = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets(AsmdefQuery)[0]);
                return asmdefPath.StartsWith("Packages");
            }
        }

        public static string ResolvedPath
        {
            get
            {
                var asmdefPath = GetAsmdefPath();
                return asmdefPath != string.Empty ? asmdefPath : $"Packages/{PackageName}/";
            }
        }

        public static string GetAsmdefPath()
        {
            var p = Directory.GetParent(AssetDatabase.FindAssets(AsmdefPath)
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault() ?? string.Empty)?.Parent?.ToString();

            if (p.StartsWith(Application.dataPath))
            {
                return "Assets" + p.Substring(Application.dataPath.Length);
            }

            return string.Empty;
        }

        public static string Path
        {
            get
            {
                string rootPath = GetRootPath();
                return rootPath ?? Application.dataPath;
            }
        }

        public static string GetRootPath()
        {
            string asmdefPath = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets(AsmdefQuery)[0]);
            
            DirectoryVisitor directoryVisitor = new DirectoryVisitor(asmdefPath);
            directoryVisitor.Back();

            string rootPath = directoryVisitor.GetPathWithoutSplitChar();

            if (rootPath.StartsWith("Assets/"))
            {
                return rootPath;
            }

            return null;
        }
    }

    internal class DirectoryVisitor
    {
        string path;

        const char splitChar = '/';

        internal DirectoryVisitor(string path)
        {
            path = path.Replace('\\', splitChar);

            if (IsFolder(path) == false)
            {
                path = path.Substring(0, path.LastIndexOf(splitChar) + 1);
            }

            this.path = path.Replace('\\', splitChar);
        }

        internal DirectoryVisitor Enter(string folderName)
        {
            if (IsFolder(path) == false)
                throw new Exception("Path is not a folder.");

            path += folderName + splitChar;
            return this;
        }

        internal DirectoryVisitor Back()
        {
            if (path[path.Length - 1] != splitChar)
                throw new Exception("Path is not a folder.");

            path = path.Substring(0, path.Remove(path.Length - 1).LastIndexOf(splitChar) + 1);
            return this;
        }

        internal bool HasFolder(string folderName)
        {
            string checkPath = new DirectoryVisitor(path).Enter(folderName).GetPathWithoutSplitChar();
            return AssetDatabase.IsValidFolder(checkPath);
        }

        internal DirectoryVisitor CreateFolder(string folderName)
        {
            AssetDatabase.CreateFolder(GetPathWithoutSplitChar(), folderName);
            //Debug.Log($"Create Folder:{GetPathWithoutSplitChar()} , {folderName}");

            return this;
        }

        internal DirectoryVisitor CreateFolderIfNotExist(string folderName)
        {
            if (HasFolder(folderName) == false)
                CreateFolder(folderName);

            return this;
        }

        internal string GetPathWithoutSplitChar()
        {
            return path.Remove(path.Length - 1);
        }

        public override string ToString()
        {
            return path;
        }

        static bool IsFolder(string path)
        {
            return path[path.Length - 1] == splitChar;
        }
    }

    // ---------------------------------------------- Attributes
    // ---------------------------------------------------------
    /// <summary>
    /// Display a field as read-only in the inspector.
    /// CustomPropertyDrawers will not work when this attribute is used.
    /// </summary>
    /// <seealso cref="BeginReadOnlyGroupAttribute"/>
    /// <seealso cref="EndReadOnlyGroupAttribute"/>
    public class ReadOnlyAttribute : PropertyAttribute { }

    /// <summary>
    /// Display one or more fields as read-only in the inspector.
    /// Use <see cref="EndReadOnlyGroupAttribute"/> to close the group.
    /// Works with CustomPropertyDrawers.
    /// </summary>
    /// <seealso cref="EndReadOnlyGroupAttribute"/>
    /// <seealso cref="ReadOnlyAttribute"/>
    public class BeginReadOnlyGroupAttribute : PropertyAttribute { }

    /// <summary>
    /// Use with <see cref="BeginReadOnlyGroupAttribute"/>.
    /// Close the read-only group and resume editable fields.
    /// </summary>
    /// <seealso cref="BeginReadOnlyGroupAttribute"/>
    /// <seealso cref="ReadOnlyAttribute"/>
    public class EndReadOnlyGroupAttribute : PropertyAttribute { }
}
