using System;

namespace Core.Abstraction
{
    public interface IConfigurationService
    {
        void SaveConfiguration(string sectionName, string format, object config);
        T LoadConfiguration<T>(string sectionName) where T : new();
    }
}
