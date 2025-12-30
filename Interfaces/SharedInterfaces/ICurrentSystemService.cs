using System;

namespace Interfaces.SharedInterfaces
{
    public interface ICurrentSystemService
    {
        IGantrySyncService CurrentSystem { get; }
        event EventHandler SystemChanged;

        void SelectSystem(int systemId);
    }
}
