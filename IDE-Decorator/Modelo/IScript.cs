using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDE_Decorator.Modelo
{
    public interface IScript
    {
        string GetContent();
        string GetFileName();
    }
}
