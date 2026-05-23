using System;
using UnityEngine;

namespace EndLink.Ally
{
    /// <summary>
    /// 队友调试日志分类。
    /// Editor 窗口用它过滤状态、AI 决策、助战流程和战斗执行日志。
    /// </summary>
    [Flags]
    public enum AllyDebugCategory
    {
        None = 0,
        State = 1 << 0,
        Brain = 1 << 1,
        Assist = 1 << 2,
        Combat = 1 << 3,
        Follow = 1 << 4,
        All = State | Brain | Assist | Combat | Follow
    }

    /// <summary>
    /// 单条队友调试日志。
    /// </summary>
    public readonly struct AllyDebugEntry
    {
        public AllyDebugEntry(GameObject ally, AllyDebugCategory category, string message)
        {
            TimeStamp = Time.time;
            Frame = Time.frameCount;
            Ally = ally;
            Category = category;
            Message = message;
        }

        public float TimeStamp { get; }

        public int Frame { get; }

        public GameObject Ally { get; }

        public AllyDebugCategory Category { get; }

        public string Message { get; }
    }

    /// <summary>
    /// 队友调试事件流。
    /// 运行时代码只向这里发事件，显示和过滤由 Editor 窗口负责。
    /// </summary>
    public static class AllyDebugLog
    {
        /// <summary>是否采集队友调试事件。</summary>
        public static bool CaptureEnabled { get; set; } = true;

        /// <summary>是否同时镜像到 Unity Console。默认关闭，避免刷屏。</summary>
        public static bool MirrorToConsole { get; set; }

        /// <summary>队友调试事件。</summary>
        public static event Action<AllyDebugEntry> Raised;

        /// <summary>
        /// 发出一条队友调试事件。
        /// </summary>
        public static void Raise(GameObject ally, AllyDebugCategory category, string message)
        {
            if (!CaptureEnabled || ally == null || category == AllyDebugCategory.None)
            {
                return;
            }

            AllyDebugEntry entry = new(ally, category, message);
            Raised?.Invoke(entry);

            if (MirrorToConsole)
            {
                Debug.Log($"[AllyDebug][{entry.Category}][{ally.name}] t={entry.TimeStamp:F2} f={entry.Frame} {entry.Message}", ally);
            }
        }
    }
}
