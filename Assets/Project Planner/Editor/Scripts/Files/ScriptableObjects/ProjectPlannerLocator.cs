using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectPlanner
{
    public class ProjectPlannerLocator : ScriptableObject
    {
        private bool isPackage;
        [SerializeField, ReadOnly] private string assetPath;
        [SerializeField, ReadOnly] private string assetFullPath;
        [SerializeField, ReadOnly] private string packagePath;
        
        public bool IsPackage => isPackage;
        public string AssetPath
        {
            get
            {
                if (string.IsNullOrEmpty(assetPath))
                    assetPath = GetPath();
                return assetPath;
            }
        }

        public string AssetFullPath
        {
            get
            {
                if (string.IsNullOrEmpty(assetFullPath))
                    assetFullPath = GetPath(fullPath: true);
                return assetFullPath;
            }
        }

        public string PackagePath
        {
            get
            {
                if (string.IsNullOrEmpty(packagePath))
                    packagePath = PathTools.ResolvedPath;
                return packagePath;
            }
        }
        
        public string GetPath(bool fullPath = false)
        {
            isPackage =  PathTools.IsPackage;
            var tmpPath = AssetDatabase.GetAssetPath(this);
            
            assetFullPath = Directory.GetParent(tmpPath).FullName; 

            if (assetFullPath.StartsWith(Application.dataPath))
                assetPath = tmpPath.Replace($"{nameof(ProjectPlannerLocator)}.asset", "");

            if (fullPath) return assetFullPath;
            else return assetPath;
        }
    }
}
