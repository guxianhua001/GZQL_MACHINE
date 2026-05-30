using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Abstraction
{
    public interface IConfigurationProvider
    {
        T GetConfiguration<T>() where T : class, new();
        void SaveConfiguration<T>(T config) where T : class;
        bool ConfigurationExists { get; }
        void CreateDefaultConfiguration();
    }
}
