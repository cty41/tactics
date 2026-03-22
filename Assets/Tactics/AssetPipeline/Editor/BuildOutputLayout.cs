using System.IO;

namespace Tactics.AssetPipeline.Editor
{
    /// <summary>
    /// Default project-relative build output layout under <c>Output/</c> (platform subfolders for bundles are appended by callers).
    /// </summary>
    public static class BuildOutputLayout
    {
        public const string OutputFolderName = "Output";
        public const string AssetBundlesFolderName = "AssetBundles";
        public const string PlayerFolderName = "Player";

        public static string GetDefaultBundleBuildRoot()
        {
            return Path.Combine(Directory.GetCurrentDirectory(), OutputFolderName, AssetBundlesFolderName);
        }

        public static string GetDefaultPlayerOutputFolder()
        {
            return Path.Combine(Directory.GetCurrentDirectory(), OutputFolderName, PlayerFolderName);
        }
    }
}
