using System.IO;
using UnityEditor.PackageManager;

// Where CrabSystem itself sits. It may be a folder in Assets, an embedded package or a git package in the
// package cache, so tools ask here instead of naming a path.

namespace CrabSystem
{
    public static class CrabPackage
    {
        static readonly PackageInfo info = PackageInfo.FindForAssembly(typeof(CrabPackage).Assembly);

        // As the AssetDatabase sees it ("Packages/com.crxb.crabsystem" or "Assets/CrabSystem").
        public static string AssetRoot => info != null ? info.assetPath : "Assets/CrabSystem";

        // On disk, for reading files directly.
        public static string DiskRoot => info != null ? info.resolvedPath : Path.GetFullPath(AssetRoot);

        public static string ToAssetPath(string diskPath) =>
            AssetRoot + diskPath.Replace('\\', '/').Substring(DiskRoot.Replace('\\', '/').Length);
    }
}
