using System.Diagnostics;
using System.Text;

namespace Arch.System.SourceGenerator
{
    public class Logger
    {
        private readonly StringBuilder mSb = new();

        [Conditional("DEBUG")]
        public void Log(string s)
        {
            mSb.AppendLine(s);
        }

        [Conditional("DEBUG")]
        public void Clear()
        {
            mSb.Clear();
        }

        [Conditional("DEBUG")]
        public void Save(string path)
        {
            File.WriteAllText(path, mSb.ToString());
        }
    }
}