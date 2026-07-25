using System;
using UnityEditor;

namespace EndLink.Editor
{
    /// <summary>
    /// 监听美术临时导入区的资源变化，并在导入结束后请求一次延迟校验。
    /// </summary>
    internal sealed class EndLinkIncomingAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (!ContainsIncomingPath(importedAssets)
                && !ContainsIncomingPath(deletedAssets)
                && !ContainsIncomingPath(movedAssets)
                && !ContainsIncomingPath(movedFromAssetPaths))
            {
                return;
            }

            EndLinkAssetValidation.ScheduleIncomingScan();
        }

        private static bool ContainsIncomingPath(string[] assetPaths)
        {
            if (assetPaths == null)
            {
                return false;
            }

            foreach (string assetPath in assetPaths)
            {
                if (EndLinkAssetValidation.IsIncomingPath(assetPath))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
