using UnityEngine;

namespace EndLink.Editor
{
    internal enum SceneValidationSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    /// <summary>
    /// 场景体检的一条只读结果。第一版不携带修复委托，避免工具意外改写场景。
    /// </summary>
    internal sealed class SceneValidationIssue
    {
        public SceneValidationIssue(
            SceneValidationSeverity severity,
            string code,
            string category,
            string message,
            Object context,
            string contextPath)
        {
            Severity = severity;
            Code = code;
            Category = category;
            Message = message;
            Context = context;
            ContextPath = contextPath;
        }

        public SceneValidationSeverity Severity { get; }

        public string Code { get; }

        public string Category { get; }

        public string Message { get; }

        public Object Context { get; }

        public string ContextPath { get; }
    }
}
