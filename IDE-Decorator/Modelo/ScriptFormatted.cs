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
            string raw = _inner.GetContent() ?? string.Empty;
            raw = raw.Replace("\r\n", "\n").Replace("\r", "\n");

            // Build a FlowDocument with colored Runs and serialize it to XAML so
            // the caller can set the RichTextBox.Document by parsing the XAML.
            var doc = new FlowDocument();

            string[] lines = raw.Split('\n');

            string[] keywords = new[] { "if","else","for","while","def","class","return","import","from","try","except","True","False","None","print" };
            var kwRegexes = keywords.Select(k => new System.Text.RegularExpressions.Regex($"\\b{System.Text.RegularExpressions.Regex.Escape(k)}\\b")).ToArray();

            var stringRegex = new System.Text.RegularExpressions.Regex("\"[^\"]*\"|'[^']*'");
            var commentRegex = new System.Text.RegularExpressions.Regex("#.*$", System.Text.RegularExpressions.RegexOptions.Multiline);

            foreach (var line in lines)
            {
                var paragraph = new Paragraph { Margin = new System.Windows.Thickness(0) };
                string currentLine = line ?? string.Empty;

                // find string and comment spans so keywords inside them are ignored
                var stringMatches = stringRegex.Matches(currentLine).Cast<System.Text.RegularExpressions.Match>().ToList();
                var commentMatch = commentRegex.Match(currentLine);
                int commentStart = commentMatch.Success ? commentMatch.Index : -1;

                // collect keyword matches not inside strings or comments
                var tokenMatches = new List<(int Index, int Length, Brush Brush, System.Windows.FontWeight Weight)>();

                if (commentStart >= 0)
                {
                    tokenMatches.Add((commentStart, currentLine.Length - commentStart, Brushes.DarkGreen, System.Windows.FontWeights.Normal));
                }

                foreach (System.Text.RegularExpressions.Match sm in stringMatches)
                {
                    tokenMatches.Add((sm.Index, sm.Length, Brushes.Orange, System.Windows.FontWeights.Normal));
                }

                for (int k = 0; k < kwRegexes.Length; k++)
                {
                    foreach (System.Text.RegularExpressions.Match km in kwRegexes[k].Matches(currentLine))
                    {
                        int idx = km.Index;
                        int len = km.Length;
                        // skip if inside a string
                        bool insideString = stringMatches.Any(s => idx >= s.Index && idx < s.Index + s.Length);
                        // skip if inside comment
                        bool insideComment = (commentStart >= 0 && idx >= commentStart);
                        if (!insideString && !insideComment)
                        {
                            tokenMatches.Add((idx, len, Brushes.Blue, System.Windows.FontWeights.Bold));
                        }
                    }
                }

                // build runs by walking the line
                int pos = 0;
                var ordered = tokenMatches.OrderBy(t => t.Index).ThenByDescending(t => t.Length).ToList();
                foreach (var t in ordered)
                {
                    if (t.Index > pos)
                    {
                        string plain = currentLine.Substring(pos, t.Index - pos);
                        paragraph.Inlines.Add(new Run(plain));
                    }
                    string tokenText = currentLine.Substring(t.Index, Math.Min(t.Length, Math.Max(0, currentLine.Length - t.Index)));
                    var run = new Run(tokenText) { Foreground = t.Brush };
                    if (t.Weight == System.Windows.FontWeights.Bold) run.FontWeight = System.Windows.FontWeights.Bold;
                    paragraph.Inlines.Add(run);
                    pos = t.Index + t.Length;
                    if (pos >= currentLine.Length) break;
                }

                if (pos < currentLine.Length)
                    paragraph.Inlines.Add(new Run(currentLine.Substring(pos)));

                // Preserve the newline by adding an explicit LineBreak at the end of paragraph if needed
                // Using a Paragraph per line preserves layout and makes serialization simpler.
                doc.Blocks.Add(paragraph);
            }

            // Ensure document ends with a paragraph (FlowDocument won't serialize empty trailing newlines well)
            string xaml = System.Windows.Markup.XamlWriter.Save(doc);
            return xaml;
        }
    }
}
