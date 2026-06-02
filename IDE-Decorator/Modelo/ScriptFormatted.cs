using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDE_Decorator.Modelo
{
    public class ScriptFormatted : ScriptDecorator
    {
        public ScriptFormatted(IScript inner) : base(inner) { }

        public override string GetContent()
        {
            string raw = _inner.GetContent();

            raw = raw.Replace("\r\n", "\n").Replace("\r", "\n");

            var lines = raw.Split('\n');
            var sb = new StringBuilder(raw.Length);
            foreach (var line in lines)
                sb.AppendLine(line.TrimEnd());

            return sb.ToString().TrimEnd('\r', '\n', ' ') + "\n";
        }
    }
}
