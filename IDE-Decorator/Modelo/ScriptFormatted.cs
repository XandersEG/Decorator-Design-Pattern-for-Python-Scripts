using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

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

        public static void HighlightPython(RichTextBox editor)
        {
            if (editor == null) return;

            TextRange documentRange = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd);
            documentRange.ClearAllProperties();

            string text = documentRange.Text;

            string[] keywords = new[] { "if","else","for","while","def","class","return","import","from","try","except","print","True","False","None"};
            foreach (string keyword in keywords)
            {
                Regex regex = new Regex($"\\b{Regex.Escape(keyword)}\\b");
                foreach (Match match in regex.Matches(text))
                {
                    TextPointer start = GetTextPositionAtOffset(editor.Document.ContentStart, match.Index);
                    TextPointer end = start != null ? GetTextPositionAtOffset(start, match.Length) : null;
                    if (start != null && end != null)
                    {
                        TextRange range = new TextRange(start, end);
                        range.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Blue);
                        range.ApplyPropertyValue(TextElement.FontWeightProperty, System.Windows.FontWeights.Bold);
                    }
                }
            }

            foreach (Match match in Regex.Matches(text, "\"[^\"]*\"|\'[^\']*\'"))
            {
                TextPointer start = GetTextPositionAtOffset(editor.Document.ContentStart, match.Index);
                TextPointer end = start != null ? GetTextPositionAtOffset(start, match.Length) : null;
                if (start != null && end != null)
                {
                    TextRange range = new TextRange(start, end);
                    range.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Brown);
                }
            }

            foreach (Match match in Regex.Matches(text, "#.*$", RegexOptions.Multiline))
            {
                TextPointer start = GetTextPositionAtOffset(editor.Document.ContentStart, match.Index);
                TextPointer end = start != null ? GetTextPositionAtOffset(start, match.Length) : null;
                if (start != null && end != null)
                {
                    TextRange range = new TextRange(start, end);
                    range.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Green);
                }
            }
        }

        private static TextPointer GetTextPositionAtOffset(TextPointer start, int offset)
        {
            TextPointer current = start;
            int count = 0;

            while (current != null)
            {
                if (current.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    string textRun = current.GetTextInRun(LogicalDirection.Forward);
                    if (count + textRun.Length >= offset)
                    {
                        return current.GetPositionAtOffset(offset - count);
                    }
                    count += textRun.Length;
                }

                current = current.GetNextContextPosition(LogicalDirection.Forward);
            }

            return null;
        }
    }
}
