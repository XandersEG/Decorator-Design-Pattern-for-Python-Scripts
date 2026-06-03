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
            _hash = ComputeSha256(inner.GetContent().TrimEnd());
        }

        public override string GetContent()
        {
            var sb = new StringBuilder();
            sb.AppendLine("#" + _hash);
            sb.Append(_inner.GetContent().TrimEnd());
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


            string contenido = string.Join("\n", lines.Skip(1)).TrimEnd();
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
    }
}
