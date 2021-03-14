using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Crow.Coding
{
    public struct BreakPoint : IEquatable<BreakPoint>
    {
        public CSProjectItem File;
        public int Line;
        public bool IsEnabled;

        public BreakPoint (CSProjectItem file, int line, bool isEnabled = true) {
            File = file;
            Line = line;
            IsEnabled = isEnabled;
        }

        public BreakPoint WithIsEnabledToogled => new BreakPoint (File, Line, !IsEnabled);

        public bool Equals (BreakPoint other) =>
            File.FullPath == other.File.FullPath && Line == other.Line;
        public override int GetHashCode () => HashCode.Combine (File.FullPath, Line);        
        public override string ToString () => $"{File.FullPath}:{Line}";
    }
}
