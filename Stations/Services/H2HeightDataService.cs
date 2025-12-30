using System;
using System.Collections.Generic;

namespace Stations.Services
{
    public interface IH2HeightDataService
    {
        double? GetH2Height(int tabIndex);
        void UpdateH2Height(int tabIndex, double height);
        Dictionary<int, double> GetAllH2Heights();
        event Action<int, double> H2HeightUpdated;
    }

    public class H2HeightDataService : IH2HeightDataService
    {
        private readonly Dictionary<int, double> _h2Heights = new Dictionary<int, double>();
        private readonly object _lock = new object();

        public event Action<int, double> H2HeightUpdated;

        public double? GetH2Height(int tabIndex)
        {
            lock (_lock)
            {
                return _h2Heights.ContainsKey(tabIndex) ? _h2Heights[tabIndex] : (double?)null;
            }
        }

        public void UpdateH2Height(int tabIndex, double height)
        {
            lock (_lock)
            {
                _h2Heights[tabIndex] = height;
            }

            H2HeightUpdated?.Invoke(tabIndex, height);
        }

        public Dictionary<int, double> GetAllH2Heights()
        {
            lock (_lock)
            {
                return new Dictionary<int, double>(_h2Heights);
            }
        }
    }
}
