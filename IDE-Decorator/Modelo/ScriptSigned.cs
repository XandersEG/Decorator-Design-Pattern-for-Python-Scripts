using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace IDE_Decorator.Modelo
{
    internal class ScriptSigned : ScriptDecorator
    {
        private readonly string _hash;
        private readonly DateTime _signedAt;

        public ScriptSigned(IScript inner) : base(inner)
        {
            _signedAt = DateTime.Now;
            _hash = ComputeSha256(inner.GetContent());
        }
        public string Hash => _hash;

        public override string GetContent()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# ============================================================");
            sb.AppendLine($"# Fecha   : {_signedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"# Hash    : {_hash}");
            sb.AppendLine("# ============================================================");
            sb.Append(_inner.GetContent());
            return sb.ToString();
        }

        public static string ComputeSha256(string input)
        {
            if (string.IsNullOrEmpty(input)) input = string.Empty;
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                var hash = sha.ComputeHash(bytes);
                var hex = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) hex.AppendFormat("{0:x2}", b);
                return hex.ToString();
            }
        }

    }
}
