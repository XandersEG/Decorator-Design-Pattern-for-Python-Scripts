using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace IDE_Decorator.Modelo
{
    internal class ScriptSigned : ScriptDecorator
    {
        private string _hash;
        private static readonly string CsvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".local_scripts", "firmas.csv");

        public ScriptSigned(IScript inner) : base(inner)
        {
            _hash = ComputeSha256(NormalizeToLf(inner.GetContent()).TrimEnd('\n'));
        }

        public override string GetContent()
        {
            var sb = new StringBuilder();
            sb.AppendLine("#" + _hash);
            var body = NormalizeToLf(_inner.GetContent()).TrimEnd('\n').Replace("\n", "\r\n");
            sb.Append(body);
            return sb.ToString();
        }

        public void RegistrarEnCsv(string nombreArchivo)
        {
            string directorio = Path.GetDirectoryName(CsvPath);
            if (!Directory.Exists(directorio)) Directory.CreateDirectory(directorio);

            if (ExisteEnCsv(nombreArchivo, _hash)) return;

            using (var sw = new StreamWriter(CsvPath, true, Encoding.UTF8))
            {
                sw.WriteLine($"{nombreArchivo},{_hash},{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }
        }

        public void RegenerarFirma(string nombreArchivo)
        {
            _hash = ComputeSha256(_inner.GetContent().TrimEnd());
            string directorio = Path.GetDirectoryName(CsvPath);
            if (!Directory.Exists(directorio)) Directory.CreateDirectory(directorio);

            using (var sw = new StreamWriter(CsvPath, true, Encoding.UTF8))
            {
                sw.WriteLine($"{nombreArchivo},{_hash},{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }
        }

        private static bool ExisteEnCsv(string nombreArchivo, string hash)
        {
            if (!File.Exists(CsvPath)) return false;
            return File.ReadLines(CsvPath)
                       .Any(line => line.StartsWith($"{nombreArchivo},{hash}"));
        }

        public static bool IsAlreadySigned(string rawDiskContent)
        {
            if (string.IsNullOrWhiteSpace(rawDiskContent)) return false;

            string[] lines = rawDiskContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            if (lines.Length > 0)
            {
                string firstLine = lines[0].Trim();
                if (firstLine.StartsWith("#")) firstLine = firstLine.Substring(1);
                if (firstLine.Length == 64 && firstLine.All(c => "0123456789abcdefABCDEF".Contains(c)))
                {
                    return true;
                }
            }
            return false;
        }


        public static bool VerificarFirma(string rawDiskContent)
        {
            if (string.IsNullOrWhiteSpace(rawDiskContent)) return false;

            string[] lines = rawDiskContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            if (lines.Length < 2) return false;

            string firstLine = lines[0].Trim();
            if (firstLine.StartsWith("#")) firstLine = firstLine.Substring(1);


            string contenido = string.Join("\n", lines.Skip(1));
            contenido = NormalizeToLf(contenido).TrimEnd('\n');
            return string.Equals(firstLine, ComputeSha256(contenido), StringComparison.OrdinalIgnoreCase);
        }

        public static string ComputeSha256(string input)
        {
            if (string.IsNullOrEmpty(input)) input = string.Empty;
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                var hash = sha.ComputeHash(bytes);
                var hex = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                    hex.AppendFormat("{0:x2}", b);
                return hex.ToString();
            }
        }
        public static string LeerHashDesdeCsv(string nombreArchivo)
        {
            if (!File.Exists(CsvPath)) return null;
            // El CSV guarda: nombreArchivo,hash,fecha
            // Si hay múltiples entradas para el mismo archivo, tomamos la última (la más reciente)
            string ultimaLinea = File.ReadLines(CsvPath)
                                     .Where(l => l.StartsWith(nombreArchivo + ","))
                                     .LastOrDefault();
            if (ultimaLinea == null) return null;
            var partes = ultimaLinea.Split(',');
            return partes.Length >= 2 ? partes[1] : null;
        }

        private static string NormalizeToLf(string s)
        {
            if (s == null) return string.Empty;
            if (s.TrimStart().StartsWith("<FlowDocument", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var doc = System.Windows.Markup.XamlReader.Parse(s) as System.Windows.Documents.FlowDocument;
                    if (doc != null)
                    {
                        var tr = new System.Windows.Documents.TextRange(doc.ContentStart, doc.ContentEnd);
                        return tr.Text.Replace("\r\n", "\n").Replace("\r", "\n");
                    }
                }
                catch { }
            }
            return s.Replace("\r\n", "\n").Replace("\r", "\n");
        }
    }
}
